using Shouldly;
using SunshineHelper.Domain.Session;
using SunshineHelper.Domain.Settings;
using Xunit;

namespace SunshineHelper.Domain.Tests.Settings;

public class ClientProfileSelectorTests
{
    [Fact]
    public void Select_casa_pelo_nome_do_cliente_sem_diferenciar_maiusculas()
    {
        var tv = Profile("tv-4k", "TV 4K", "XBOX", "Xbox Series X");
        var settings = Settings(defaultProfileId: null, tv);

        var selected = ClientProfileSelector.Select(settings, new ClientRequest { Name = "xbox" });

        selected.ShouldBe(tv);
    }

    [Fact]
    public void Select_sem_nome_de_cliente_que_bata_usa_o_perfil_padrao()
    {
        var tv = Profile("tv-4k", "TV 4K", "XBOX");
        var celular = Profile("celular", "Celular", "Celular do João");
        var settings = Settings(defaultProfileId: "celular", tv, celular);

        var selected = ClientProfileSelector.Select(settings, new ClientRequest { Name = "Nome desconhecido" });

        selected.ShouldBe(celular);
    }

    [Fact]
    public void Select_sem_nome_nenhum_usa_o_perfil_padrao()
    {
        var tv = Profile("tv-4k", "TV 4K", "XBOX");
        var settings = Settings(defaultProfileId: "tv-4k", tv);

        var selected = ClientProfileSelector.Select(settings, new ClientRequest());

        selected.ShouldBe(tv);
    }

    [Fact]
    public void Select_sem_perfil_padrao_e_sem_nome_que_bata_devolve_nulo()
    {
        var tv = Profile("tv-4k", "TV 4K", "XBOX");
        var settings = Settings(defaultProfileId: null, tv);

        var selected = ClientProfileSelector.Select(settings, new ClientRequest { Name = "Nome desconhecido" });

        selected.ShouldBeNull();
    }

    [Fact]
    public void Select_perfil_padrao_que_nao_existe_mais_devolve_nulo()
    {
        var tv = Profile("tv-4k", "TV 4K", "XBOX");
        var settings = Settings(defaultProfileId: "perfil-apagado", tv);

        var selected = ClientProfileSelector.Select(settings, new ClientRequest());

        selected.ShouldBeNull();
    }

    [Fact]
    public void Select_nome_do_cliente_tem_prioridade_sobre_o_perfil_padrao()
    {
        var tv = Profile("tv-4k", "TV 4K", "XBOX");
        var celular = Profile("celular", "Celular", "Celular do João");
        var settings = Settings(defaultProfileId: "celular", tv, celular);

        var selected = ClientProfileSelector.Select(settings, new ClientRequest { Name = "XBOX" });

        selected.ShouldBe(tv);
    }

    private static ClientProfile Profile(string id, string name, params string[] clientNames) => new()
    {
        Id = id,
        Name = name,
        Match = new ClientMatchRule { ClientNames = clientNames },
        Display = new DisplayProfileSettings
        {
            Resolution = new ProfileValue<Resolution> { Mode = ProfileValueMode.Leave },
            Hdr = new ProfileValue<bool> { Mode = ProfileValueMode.Leave },
            FpsLimit = new ProfileValue<int> { Mode = ProfileValueMode.Leave },
        },
    };

    private static AppSettings Settings(string? defaultProfileId, params ClientProfile[] profiles) => new()
    {
        SchemaVersion = SettingsSchema.CurrentVersion,
        DefaultProfileId = defaultProfileId,
        Profiles = profiles,
    };
}
