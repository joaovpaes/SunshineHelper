using Shouldly;
using SunshineHelper.Application.Settings;
using SunshineHelper.Domain.Settings;
using Xunit;

namespace SunshineHelper.Application.Tests.Settings;

public class CurrentSettingsHolderTests
{
    [Fact]
    public void Current_comeca_com_o_valor_inicial()
    {
        var initial = AppSettings.CreateDefault();

        new CurrentSettingsHolder(initial).Current.ShouldBeSameAs(initial);
    }

    [Fact]
    public void Publish_troca_o_valor_lido_por_quem_consulta()
    {
        var holder = new CurrentSettingsHolder(AppSettings.CreateDefault());
        var edited = AppSettings.CreateDefault() with { SunshineLogPath = @"C:\caminho\sunshine.log" };

        holder.Publish(edited);

        holder.Current.ShouldBeSameAs(edited);
    }

    [Fact]
    public void Recusa_configuracao_nula()
    {
        Should.Throw<ArgumentNullException>(() => new CurrentSettingsHolder(null!));
        Should.Throw<ArgumentNullException>(() => new CurrentSettingsHolder(AppSettings.CreateDefault()).Publish(null!));
    }
}
