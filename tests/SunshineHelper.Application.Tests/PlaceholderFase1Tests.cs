using Shouldly;
using Xunit;

namespace SunshineHelper.Application.Tests;

public class PlaceholderFase1Tests
{
    [Fact]
    public void Fase_identifica_a_fase_atual()
    {
        PlaceholderFase1.Fase.ShouldBe("Fase 1: estrutura da solução");
    }
}
