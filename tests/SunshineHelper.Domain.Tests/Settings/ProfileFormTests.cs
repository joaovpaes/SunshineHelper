using Shouldly;
using SunshineHelper.Domain.Settings;
using Xunit;

namespace SunshineHelper.Domain.Tests.Settings;

public class ProfileFormTests
{
    private static ProfileFormInput Valid() => new()
    {
        Name = "Sala TV 4K",
        ClientNamesText = "Sala\r\n sala , Quarto;",
        ResolutionMode = ProfileValueMode.Fixed,
        ResolutionText = "3840 x 2160",
        HdrMode = ProfileValueMode.Fixed,
        HdrOn = true,
        FpsMode = ProfileValueMode.Fixed,
        FpsText = "60",
        RevertOnDisconnect = true,
        LauncherDelayText = "3000",
    };

    private static List<string> NoIds() => [];

    [Fact]
    public void Formulario_valido_vira_perfil_que_passa_no_validador()
    {
        var result = ProfileForm.Build(Valid(), null, NoIds());

        result.IsValid.ShouldBeTrue();
        var profile = result.Profile!;
        profile.Id.ShouldBe("sala-tv-4k");
        profile.Name.ShouldBe("Sala TV 4K");
        profile.Match.ClientNames.Count.ShouldBe(2);
        profile.Display.Resolution.FixedValue.ShouldBe(new Resolution(3840, 2160));
        profile.Display.Hdr.FixedValue.ShouldBe(true);
        profile.Display.FpsLimit.FixedValue.ShouldBe(60);
        profile.LauncherDelayMs.ShouldBe(3000);
        profile.RevertOnDisconnect.ShouldBeTrue();

        var settings = AppSettings.CreateDefault() with { Profiles = new List<ClientProfile> { profile } };
        SettingsValidator.Validate(settings).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Nome_vazio_e_recusado()
    {
        var result = ProfileForm.Build(Valid() with { Name = "   " }, null, NoIds());

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(ProfileFormError.NameEmpty);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1920")]
    [InlineData("0x1080")]
    [InlineData("1920x0")]
    [InlineData("axb")]
    [InlineData("1920x1080x3")]
    [InlineData("99999x1080")]
    public void Resolucao_fixa_invalida_e_recusada(string text)
    {
        var result = ProfileForm.Build(Valid() with { ResolutionText = text }, null, NoIds());

        result.Errors.ShouldContain(ProfileFormError.ResolutionInvalid);
    }

    [Theory]
    [InlineData("1920x1080")]
    [InlineData("1920 x 1080")]
    [InlineData("1920X1080")]
    [InlineData("1920×1080")]
    public void Resolucao_aceita_varias_grafias(string text)
    {
        ProfileForm.TryParseResolution(text, out var resolution).ShouldBeTrue();
        resolution.ShouldBe(new Resolution(1920, 1080));
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("abc")]
    [InlineData("1001")]
    public void Fps_fixo_invalido_e_recusado(string text)
    {
        var result = ProfileForm.Build(Valid() with { FpsText = text }, null, NoIds());

        result.Errors.ShouldContain(ProfileFormError.FpsInvalid);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("-1")]
    [InlineData("30001")]
    public void Atraso_invalido_e_recusado(string text)
    {
        var result = ProfileForm.Build(Valid() with { LauncherDelayText = text }, null, NoIds());

        result.Errors.ShouldContain(ProfileFormError.DelayInvalid);
    }

    [Fact]
    public void Atraso_vazio_usa_o_global()
    {
        var result = ProfileForm.Build(Valid() with { LauncherDelayText = " " }, null, NoIds());

        result.Profile!.LauncherDelayMs.ShouldBeNull();
    }

    [Fact]
    public void Texto_de_campos_que_nao_estao_em_modo_fixo_e_ignorado()
    {
        var input = Valid() with
        {
            ResolutionMode = ProfileValueMode.FollowClient,
            ResolutionText = "lixo",
            FpsMode = ProfileValueMode.Leave,
            FpsText = "lixo",
            HdrMode = ProfileValueMode.Leave,
            HdrOn = true,
        };

        var result = ProfileForm.Build(input, null, NoIds());

        result.IsValid.ShouldBeTrue();
        result.Profile!.Display.Resolution.FixedValue.ShouldBeNull();
        result.Profile.Display.Hdr.Mode.ShouldBe(ProfileValueMode.Leave);
        result.Profile.Display.Hdr.FixedValue.ShouldBeFalse();
    }

    [Fact]
    public void Id_novo_nao_repete_um_existente()
    {
        var taken = new List<string> { "sala-tv-4k", "SALA-TV-4K-2" };

        var result = ProfileForm.Build(Valid(), null, taken);

        result.Profile!.Id.ShouldBe("sala-tv-4k-3");
    }

    [Fact]
    public void Editar_mantem_o_id_e_os_intervalos_de_ip()
    {
        var existing = new ClientProfile
        {
            Id = "antigo",
            Name = "Antigo",
            Match = new ClientMatchRule { IpRanges = new List<string> { "192.168.0.0/24" } },
            Display = ProfileForm.Build(Valid(), null, NoIds()).Profile!.Display,
        };

        var result = ProfileForm.Build(Valid() with { Name = "Renomeado" }, existing, NoIds());

        result.Profile!.Id.ShouldBe("antigo");
        result.Profile.Name.ShouldBe("Renomeado");
        result.Profile.Match.IpRanges.ShouldBe(existing.Match.IpRanges);
    }

    [Fact]
    public void Ida_e_volta_preserva_os_campos()
    {
        var profile = ProfileForm.Build(Valid(), null, NoIds()).Profile!;

        var again = ProfileForm.Build(ProfileForm.FromProfile(profile), profile, NoIds()).Profile!;

        ProfileForm.FromProfile(again).ShouldBe(ProfileForm.FromProfile(profile));
        again.Id.ShouldBe(profile.Id);
    }

    [Theory]
    [InlineData("Sala TV 4K", "sala-tv-4k")]
    [InlineData("  Água & Sol!! ", "agua-sol")]
    [InlineData("---", "profile")]
    [InlineData("", "profile")]
    [InlineData("Quarto_2", "quarto-2")]
    public void Slugify_gera_ids_simples(string name, string expected)
    {
        ProfileForm.Slugify(name).ShouldBe(expected);
    }
}
