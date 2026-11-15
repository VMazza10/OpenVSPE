using OpenVSPE.Core.Configuration;
using OpenVSPE.Core.Models;

namespace OpenVSPE.Tests;

public class AppConfigTests : IDisposable
{
    private readonly string _pasta = Path.Combine(Path.GetTempPath(), "openvspe-tests-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_pasta)) Directory.Delete(_pasta, true);
    }

    [Fact]
    public void JsonAntigo_SemCampoNovo_VigiaLigado()
    {
        var mgr = new ConfigManager(_pasta);
        File.WriteAllText(mgr.ConfigPath, "{\"canais\":[]}");

        var config = mgr.Carregar();

        Assert.True(config.ReabrirSozinho);
        Assert.Null(config.SenhaTecnico);
    }

    [Fact]
    public void SalvarECarregar_MantemOpcoes()
    {
        var mgr = new ConfigManager(_pasta);

        mgr.Salvar(new AppConfig { ReabrirSozinho = false, SenhaTecnico = "x" });
        var config = mgr.Carregar();

        Assert.False(config.ReabrirSozinho);
        Assert.Equal("x", config.SenhaTecnico);
    }

    [Fact]
    public void SemSenha_NaoGravaCampo()
    {
        var mgr = new ConfigManager(_pasta);

        mgr.Salvar(new AppConfig { SenhaTecnico = null });

        Assert.DoesNotContain("senhaTecnico", File.ReadAllText(mgr.ConfigPath));
    }
}
