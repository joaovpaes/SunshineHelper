using System.Text.Json;
using System.Threading;
using SunshineHelper.Application.Diagnostics;
using SunshineHelper.Application.Settings;
using SunshineHelper.Domain.Devices;

namespace SunshineHelper.Application.Devices;

/// <summary>
/// The devices that have connected, kept in <c>devices.json</c> next to settings.json
/// (docs/adr/0014). It exists for the Devices screen only: nothing in the stream path
/// depends on it, so every failure here is written to the log and swallowed. A broken or
/// unreadable file simply starts an empty list.
/// </summary>
public sealed class DeviceRegistry : IDisposable
{
    public const int SchemaVersion = 1;

    private static readonly JsonSerializerOptions _json = SettingsJsonOptions.Create();

    private readonly ISettingsFileStore _store;
    private readonly string _path;
    private readonly TimeProvider _timeProvider;
    private readonly IDiagnosticLog _log;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile IReadOnlyList<KnownDevice> _devices = [];

    public DeviceRegistry(
        ISettingsFileStore store,
        string path,
        TimeProvider? timeProvider = null,
        IDiagnosticLog? log = null)
    {
        _store = store;
        _path = path;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _log = log ?? NullDiagnosticLog.Instance;
    }

    /// <summary>Most recently seen first.</summary>
    public IReadOnlyList<KnownDevice> Devices => _devices;

    public void Dispose() => _gate.Dispose();

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        string? text;
        try
        {
            text = await _store.ReadAllTextAsync(_path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Write(DiagnosticLevel.Warning, "devices", "could not read devices.json: " + ex.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        try
        {
            var file = JsonSerializer.Deserialize<DeviceFile>(text, _json);
            _devices = KnownDevices.Normalize(file?.Devices ?? []);
        }
        catch (JsonException ex)
        {
            // Starts empty; the next connection writes a fresh, valid file.
            _log.Write(DiagnosticLevel.Warning, "devices", "devices.json is not valid, starting an empty list: " + ex.Message);
        }
    }

    /// <summary>Notes one connection. A blank name (older Sunshine, or a client that sends
    /// none) is ignored.</summary>
    public async Task RecordConnectionAsync(string? name, string? profileName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await SaveAsync(KnownDevices.Record(_devices, name, profileName, _timeProvider.GetUtcNow()), cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Removes one device from the list. It only deletes the record: profiles are
    /// not touched, and the device is listed again on its next connection.</summary>
    public async Task ForgetAsync(string name, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await SaveAsync(KnownDevices.Forget(_devices, name), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task SaveAsync(IReadOnlyList<KnownDevice> updated, CancellationToken cancellationToken)
    {
        // The list in memory is already right for the screen even if the disk write fails.
        _devices = updated;
        try
        {
            string text = JsonSerializer.Serialize(
                new DeviceFile { SchemaVersion = SchemaVersion, Devices = [.. updated] },
                _json);
            await _store.WriteAllTextAtomicAsync(_path, text, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Write(DiagnosticLevel.Warning, "devices", "could not write devices.json: " + ex.Message);
        }
    }

    private sealed record DeviceFile
    {
        public int SchemaVersion { get; init; }

        public List<KnownDevice> Devices { get; init; } = [];
    }
}
