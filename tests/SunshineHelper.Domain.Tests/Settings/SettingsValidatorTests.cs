using Shouldly;
using SunshineHelper.Domain.Settings;
using Xunit;

namespace SunshineHelper.Domain.Tests.Settings;

public class SettingsValidatorTests
{
    private static ClientProfile ValidProfile(string id = "tv-4k") => new()
    {
        Id = id,
        Name = "TV 4K",
        Match = new ClientMatchRule { ClientNames = ["XBOX"] },
        Display = new DisplayProfileSettings
        {
            Resolution = new ProfileValue<Resolution> { Mode = ProfileValueMode.Fixed, FixedValue = new Resolution(3840, 2160) },
            Hdr = new ProfileValue<bool> { Mode = ProfileValueMode.FollowClient },
            FpsLimit = new ProfileValue<int> { Mode = ProfileValueMode.Fixed, FixedValue = 60 },
        },
    };

    private static AppSettings ValidSettings() => new()
    {
        SchemaVersion = SettingsSchema.CurrentVersion,
        DefaultProfileId = "tv-4k",
        Profiles = [ValidProfile()],
    };

    [Fact]
    public void Validate_configuracao_valida_nao_tem_erros()
    {
        var result = SettingsValidator.Validate(ValidSettings());

        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_versao_de_schema_errada_falha()
    {
        var settings = ValidSettings() with { SchemaVersion = SettingsSchema.CurrentVersion + 1 };

        var result = SettingsValidator.Validate(settings);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("schemaVersion"));
    }

    [Fact]
    public void Validate_sem_perfis_eh_valido_instalacao_nova()
    {
        var settings = ValidSettings() with { Profiles = [], DefaultProfileId = null };

        var result = SettingsValidator.Validate(settings);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ids_de_perfil_duplicados_falha()
    {
        var settings = ValidSettings() with { Profiles = [ValidProfile("a"), ValidProfile("a")] };

        var result = SettingsValidator.Validate(settings);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("duplicado"));
    }

    [Fact]
    public void Validate_defaultProfileId_sem_perfil_correspondente_falha()
    {
        var settings = ValidSettings() with { DefaultProfileId = "nao-existe" };

        var result = SettingsValidator.Validate(settings);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("defaultProfileId"));
    }

    [Fact]
    public void Validate_defaultProfileId_nulo_eh_permitido()
    {
        var settings = ValidSettings() with { DefaultProfileId = null };

        var result = SettingsValidator.Validate(settings);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0, 20_000, 55_000)]
    [InlineData(10_000, 5_000, 55_000)]
    [InlineData(10_000, 20_000, 10_000)]
    public void Validate_orcamento_de_tempo_fora_de_ordem_falha(int applyTargetMs, int barrierDefaultMs, int barrierCeilingMs)
    {
        var settings = ValidSettings() with
        {
            Timing = new TimingSettings
            {
                ApplyTargetMs = applyTargetMs,
                BarrierDefaultMs = barrierDefaultMs,
                BarrierCeilingMs = barrierCeilingMs,
            },
        };

        var result = SettingsValidator.Validate(settings);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validate_resolucao_fixa_invalida_falha()
    {
        var profile = ValidProfile() with
        {
            Display = ValidProfile().Display with
            {
                Resolution = new ProfileValue<Resolution> { Mode = ProfileValueMode.Fixed, FixedValue = new Resolution(0, 2160) },
            },
        };
        var settings = ValidSettings() with { Profiles = [profile] };

        var result = SettingsValidator.Validate(settings);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("resolução"));
    }

    [Fact]
    public void Validate_fpsLimit_fixo_zero_ou_negativo_falha()
    {
        var profile = ValidProfile() with
        {
            Display = ValidProfile().Display with
            {
                FpsLimit = new ProfileValue<int> { Mode = ProfileValueMode.Fixed, FixedValue = 0 },
            },
        };
        var settings = ValidSettings() with { Profiles = [profile] };

        var result = SettingsValidator.Validate(settings);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("fpsLimit"));
    }

    [Fact]
    public void Validate_launcherDelayMs_negativo_no_perfil_falha()
    {
        var profile = ValidProfile() with { LauncherDelayMs = -1 };
        var settings = ValidSettings() with { Profiles = [profile] };

        var result = SettingsValidator.Validate(settings);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("launcherDelayMs"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_logPollIntervalMs_nao_positivo_falha(int logPollIntervalMs)
    {
        var settings = ValidSettings() with
        {
            Timing = new TimingSettings { LogPollIntervalMs = logPollIntervalMs },
        };

        var result = SettingsValidator.Validate(settings);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("logPollIntervalMs"));
    }
}
