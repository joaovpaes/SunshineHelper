using Shouldly;
using SunshineHelper.Domain.Session;
using SunshineHelper.Domain.Settings;
using Xunit;

namespace SunshineHelper.Domain.Tests.Session;

public class DesiredEnvironmentResolverTests
{
    [Fact]
    public void Resolve_modo_leave_nao_mexe_em_nada()
    {
        var desired = DesiredEnvironmentResolver.Resolve(
            Profile(ProfileValueMode.Leave, ProfileValueMode.Leave, ProfileValueMode.Leave),
            FullRequest());

        desired.Resolution.ShouldBeNull();
        desired.Hdr.ShouldBeNull();
        desired.FpsLimit.ShouldBeNull();
        desired.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Resolve_modo_follow_client_usa_o_que_o_cliente_pediu()
    {
        var desired = DesiredEnvironmentResolver.Resolve(
            Profile(ProfileValueMode.FollowClient, ProfileValueMode.FollowClient, ProfileValueMode.FollowClient),
            FullRequest());

        desired.Resolution.ShouldBe(new Resolution(3840, 2160));
        desired.Hdr.ShouldBe(true);
        desired.FpsLimit.ShouldBe(60);
        desired.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public void Resolve_modo_follow_client_sem_dados_do_cliente_deixa_quieto()
    {
        var desired = DesiredEnvironmentResolver.Resolve(
            Profile(ProfileValueMode.FollowClient, ProfileValueMode.FollowClient, ProfileValueMode.FollowClient),
            new ClientRequest { Name = "Sala" });

        desired.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Resolve_modo_follow_client_so_com_a_largura_deixa_a_resolucao_quieta()
    {
        var desired = DesiredEnvironmentResolver.Resolve(
            Profile(ProfileValueMode.FollowClient, ProfileValueMode.Leave, ProfileValueMode.Leave),
            new ClientRequest { Width = 3840 });

        desired.Resolution.ShouldBeNull();
    }

    [Fact]
    public void Resolve_modo_fixed_usa_os_valores_do_perfil()
    {
        var profile = Profile(ProfileValueMode.Fixed, ProfileValueMode.Fixed, ProfileValueMode.Fixed) with
        {
            Display = new DisplayProfileSettings
            {
                Resolution = new ProfileValue<Resolution>
                {
                    Mode = ProfileValueMode.Fixed,
                    FixedValue = new Resolution(1920, 1080),
                },
                Hdr = new ProfileValue<bool> { Mode = ProfileValueMode.Fixed, FixedValue = true },
                FpsLimit = new ProfileValue<int> { Mode = ProfileValueMode.Fixed, FixedValue = 120 },
            },
        };

        var desired = DesiredEnvironmentResolver.Resolve(profile, FullRequest());

        desired.Resolution.ShouldBe(new Resolution(1920, 1080));
        desired.Hdr.ShouldBe(true);
        desired.FpsLimit.ShouldBe(120);
    }

    [Fact]
    public void Resolve_hdr_fixo_em_falso_desliga_o_hdr_em_vez_de_deixar_quieto()
    {
        var profile = Profile(ProfileValueMode.Leave, ProfileValueMode.Fixed, ProfileValueMode.Leave);

        var desired = DesiredEnvironmentResolver.Resolve(profile, FullRequest());

        desired.Hdr.ShouldBe(false);
        desired.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public void Resolve_resolucao_fixa_sem_valor_deixa_a_tela_quieta()
    {
        var profile = Profile(ProfileValueMode.Fixed, ProfileValueMode.Leave, ProfileValueMode.Leave);

        var desired = DesiredEnvironmentResolver.Resolve(profile, FullRequest());

        desired.Resolution.ShouldBeNull();
    }

    [Fact]
    public void Resolve_fps_fixo_sem_valor_deixa_o_limite_quieto()
    {
        var profile = Profile(ProfileValueMode.Leave, ProfileValueMode.Leave, ProfileValueMode.Fixed);

        var desired = DesiredEnvironmentResolver.Resolve(profile, FullRequest());

        desired.FpsLimit.ShouldBeNull();
    }

    [Fact]
    public void Resolve_fps_do_cliente_em_zero_deixa_o_limite_quieto()
    {
        var profile = Profile(ProfileValueMode.Leave, ProfileValueMode.Leave, ProfileValueMode.FollowClient);

        var desired = DesiredEnvironmentResolver.Resolve(profile, new ClientRequest { Fps = 0 });

        desired.FpsLimit.ShouldBeNull();
    }

    [Fact]
    public void Resolve_perfil_nulo_lanca_ArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => DesiredEnvironmentResolver.Resolve(null!, FullRequest()));

    [Fact]
    public void Resolve_pedido_nulo_lanca_ArgumentNullException() =>
        Should.Throw<ArgumentNullException>(
            () => DesiredEnvironmentResolver.Resolve(Profile(ProfileValueMode.Leave, ProfileValueMode.Leave, ProfileValueMode.Leave), null!));

    [Fact]
    public void Resolve_leva_o_atraso_do_launcher_do_perfil_sem_contar_como_mudanca()
    {
        var withDelay = Profile(ProfileValueMode.Leave, ProfileValueMode.Leave, ProfileValueMode.Leave) with { LauncherDelayMs = 3_000 };
        var withoutDelay = Profile(ProfileValueMode.Leave, ProfileValueMode.Leave, ProfileValueMode.Leave);

        var desired = DesiredEnvironmentResolver.Resolve(withDelay, FullRequest());

        desired.LauncherDelayMs.ShouldBe(3_000);
        desired.IsEmpty.ShouldBeTrue();
        DesiredEnvironmentResolver.Resolve(withoutDelay, FullRequest()).LauncherDelayMs.ShouldBeNull();
    }

    private static ClientRequest FullRequest() => new()
    {
        Name = "Sala",
        Width = 3840,
        Height = 2160,
        Fps = 60,
        Hdr = true,
    };

    private static ClientProfile Profile(ProfileValueMode resolution, ProfileValueMode hdr, ProfileValueMode fps) => new()
    {
        Id = "tv-4k",
        Name = "TV 4K",
        Display = new DisplayProfileSettings
        {
            Resolution = new ProfileValue<Resolution> { Mode = resolution },
            Hdr = new ProfileValue<bool> { Mode = hdr },
            FpsLimit = new ProfileValue<int> { Mode = fps },
        },
    };
}
