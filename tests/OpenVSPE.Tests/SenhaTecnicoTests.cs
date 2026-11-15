using OpenVSPE.Core.Configuration;

namespace OpenVSPE.Tests;

public class SenhaTecnicoTests
{
    [Fact]
    public void GerarEConferir_SenhaCerta_True()
    {
        Assert.True(SenhaTecnico.Conferir("1234", SenhaTecnico.Gerar("1234")));
    }

    [Fact]
    public void Conferir_SenhaErrada_False()
    {
        Assert.False(SenhaTecnico.Conferir("4321", SenhaTecnico.Gerar("1234")));
    }

    [Fact]
    public void Gerar_DuasVezes_ResultadosDiferentes()
    {
        const string senha = "técnico-1234";
        var primeira = SenhaTecnico.Gerar(senha);
        var segunda = SenhaTecnico.Gerar(senha);

        Assert.NotEqual(primeira, segunda);
        Assert.DoesNotContain(senha, primeira);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("lixo")]
    [InlineData("a:b")]
    [InlineData("@@:@@")]
    [InlineData("YQ==:Yg==")]
    public void Conferir_GuardadaCorrompida_FalseSemExcecao(string? guardada)
    {
        Assert.False(SenhaTecnico.Conferir("1234", guardada));
    }

    [Fact]
    public void TemSenha_IndicaSeExisteSenhaGuardada()
    {
        Assert.False(SenhaTecnico.TemSenha(null));
        Assert.False(SenhaTecnico.TemSenha(""));
        Assert.True(SenhaTecnico.TemSenha(SenhaTecnico.Gerar("1")));
    }
}
