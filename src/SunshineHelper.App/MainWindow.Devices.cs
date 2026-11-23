using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using SunshineHelper.Domain.Devices;

namespace SunshineHelper.App;

/// <summary>The Devices screen (docs/adr/0014-registro-de-aparelhos.md): the devices that
/// have connected, as Moonlight names them. The list itself lives in
/// <see cref="Application.Devices.DeviceRegistry"/>; this file only shows it.</summary>
public partial class MainWindow
{
    /// <summary>The registry builds a new list on every change, so comparing the reference is
    /// enough to know whether the screen is already up to date.</summary>
    private IReadOnlyList<KnownDevice>? _devicesShown;

    private void LoadDevices(bool force)
    {
        var devices = _host.Devices.Devices;
        if (!force && ReferenceEquals(devices, _devicesShown))
        {
            return;
        }

        _devicesShown = devices;
        string? selected = (DevicesList.SelectedItem as ListBoxItem)?.Tag as string;

        DevicesList.Items.Clear();
        foreach (var device in devices)
        {
            string profile = device.LastProfileName ?? Text("Devices.NoProfile");
            string last = device.LastSeen.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
            var item = new ListBoxItem
            {
                Tag = device.Name,
                Content = string.Format(CultureInfo.CurrentCulture, Text("Devices.Item"), device.Name, device.ConnectionCount, last, profile),
            };
            DevicesList.Items.Add(item);
            if (string.Equals(device.Name, selected, StringComparison.Ordinal))
            {
                DevicesList.SelectedItem = item;
            }
        }

        bool empty = devices.Count == 0;
        DevicesEmptyText.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        DevicesList.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        ForgetDeviceButton.IsEnabled = DevicesList.SelectedItem is not null;
    }

    /// <summary>Called by the one-second timer: a device that connects while the screen is
    /// open shows up by itself.</summary>
    private void RefreshDevicesIfVisible()
    {
        if (IsVisible && DevicesCard.Visibility == Visibility.Visible)
        {
            LoadDevices(force: false);
        }
    }

    private void OnDeviceSelected(object sender, SelectionChangedEventArgs e) =>
        ForgetDeviceButton.IsEnabled = DevicesList.SelectedItem is not null;

    private async void OnForgetDevice(object sender, RoutedEventArgs e)
    {
        if (DevicesList.SelectedItem is not ListBoxItem { Tag: string name })
        {
            return;
        }

        await _host.Devices.ForgetAsync(name, CancellationToken.None);
        DevicesMessage.Text = Text("Devices.Forgotten");
        LoadDevices(force: true);
    }
}
