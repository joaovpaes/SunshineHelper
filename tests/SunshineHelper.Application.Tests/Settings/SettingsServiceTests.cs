using Shouldly;
using SunshineHelper.Application.Settings;
using SunshineHelper.Domain.Settings;
using Xunit;

namespace SunshineHelper.Application.Tests.Settings;

public class SettingsServiceTests
{
    private const string _settingsPath = "settings.json";

    [Fact]
    public async Task LoadAsync_arquivo_inexistente_retorna_padroes_sem_escrever_nada()
    {
        var store = new FakeSettingsFileStore();
        var service = new SettingsService(store);

        var result = await service.LoadAsync(_settingsPath, TestContext.Current.CancellationToken);

        result.Created.ShouldBeTrue();
        result.Migrated.ShouldBeFalse();
        result.Validation.IsValid.ShouldBeTrue();
        result.Settings.Profiles.ShouldBeEmpty();
        store.WriteCount.ShouldBe(0);
    }

    [Fact]
    public async Task LoadAsync_json_invalido_retorna_padroes_com_erro_de_validacao()
    {
        var store = new FakeSettingsFileStore();
        store.Seed(_settingsPath, "{ não é json");
        var service = new SettingsService(store);

        var result = await service.LoadAsync(_settingsPath, TestContext.Current.CancellationToken);

        result.Created.ShouldBeFalse();
        result.Validation.IsValid.ShouldBeFalse();
        result.Settings.Profiles.ShouldBeEmpty();
    }

    [Fact]
    public async Task LoadAsync_schema_antigo_migra_e_salva_de_volta()
    {
        var store = new FakeSettingsFileStore();
        store.Seed(_settingsPath, "{}");
        var service = new SettingsService(store);

        var result = await service.LoadAsync(_settingsPath, TestContext.Current.CancellationToken);

        result.Migrated.ShouldBeTrue();
        result.Settings.SchemaVersion.ShouldBe(SettingsSchema.CurrentVersion);
        store.WriteCount.ShouldBe(1);

        var written = store.Get(_settingsPath);
        written.ShouldNotBeNullOrEmpty();
        written!.ShouldContain("schemaVersion");
    }

    [Fact]
    public async Task LoadAsync_schema_atual_nao_reescreve_o_arquivo()
    {
        var store = new FakeSettingsFileStore();
        var service = new SettingsService(store);
        await service.SaveAsync(_settingsPath, AppSettings.CreateDefault(), TestContext.Current.CancellationToken);
        store.ResetCounters();

        var result = await service.LoadAsync(_settingsPath, TestContext.Current.CancellationToken);

        result.Migrated.ShouldBeFalse();
        store.WriteCount.ShouldBe(0);
    }

    [Fact]
    public async Task SaveAsync_primeira_vez_nao_faz_backup()
    {
        var store = new FakeSettingsFileStore();
        var service = new SettingsService(store);

        await service.SaveAsync(_settingsPath, AppSettings.CreateDefault(), TestContext.Current.CancellationToken);

        store.CopyCount.ShouldBe(0);
        store.WriteCount.ShouldBe(1);
    }

    [Fact]
    public async Task SaveAsync_faz_backup_uma_vez_por_dia()
    {
        var store = new FakeSettingsFileStore();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 22, 10, 0, 0, TimeSpan.Zero));
        var service = new SettingsService(store, clock);
        await service.SaveAsync(_settingsPath, AppSettings.CreateDefault(), TestContext.Current.CancellationToken);

        await service.SaveAsync(_settingsPath, AppSettings.CreateDefault(), TestContext.Current.CancellationToken);
        clock.Now = clock.Now.AddHours(2);
        await service.SaveAsync(_settingsPath, AppSettings.CreateDefault(), TestContext.Current.CancellationToken);

        store.CopyCount.ShouldBe(1);
    }

    [Fact]
    public async Task SaveAsync_faz_novo_backup_no_dia_seguinte()
    {
        var store = new FakeSettingsFileStore();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 22, 10, 0, 0, TimeSpan.Zero));
        var service = new SettingsService(store, clock);
        await service.SaveAsync(_settingsPath, AppSettings.CreateDefault(), TestContext.Current.CancellationToken);
        await service.SaveAsync(_settingsPath, AppSettings.CreateDefault(), TestContext.Current.CancellationToken);

        clock.Now = clock.Now.AddDays(1);
        await service.SaveAsync(_settingsPath, AppSettings.CreateDefault(), TestContext.Current.CancellationToken);

        store.CopyCount.ShouldBe(2);
    }

    [Fact]
    public async Task SaveAsync_depois_de_LoadAsync_preserva_os_perfis()
    {
        var store = new FakeSettingsFileStore();
        var service = new SettingsService(store);
        var settings = AppSettings.CreateDefault() with
        {
            Profiles =
            [
                new ClientProfile
                {
                    Id = "tv-4k",
                    Name = "TV 4K",
                    Display = new DisplayProfileSettings
                    {
                        Resolution = new ProfileValue<Resolution> { Mode = ProfileValueMode.Fixed, FixedValue = new Resolution(3840, 2160) },
                        Hdr = new ProfileValue<bool> { Mode = ProfileValueMode.FollowClient },
                        FpsLimit = new ProfileValue<int> { Mode = ProfileValueMode.Fixed, FixedValue = 60 },
                    },
                },
            ],
        };

        await service.SaveAsync(_settingsPath, settings, TestContext.Current.CancellationToken);
        var result = await service.LoadAsync(_settingsPath, TestContext.Current.CancellationToken);

        result.Validation.IsValid.ShouldBeTrue();
        result.Settings.Profiles.Count.ShouldBe(1);
        result.Settings.Profiles[0].Id.ShouldBe("tv-4k");
        result.Settings.Profiles[0].Display.Resolution.FixedValue.ShouldBe(new Resolution(3840, 2160));
    }
}
