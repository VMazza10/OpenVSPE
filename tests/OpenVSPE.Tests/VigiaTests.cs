using OpenVSPE.Core;

namespace OpenVSPE.Tests;

public class VigiaTests : IDisposable
{
    private readonly DirectoryInfo _pasta = Directory.CreateTempSubdirectory("openvspe_vigia-");

    public void Dispose()
    {
        foreach (var arquivo in _pasta.EnumerateFiles())
            arquivo.Delete();
        _pasta.Delete();
    }

    [Fact]
    public void AcharExeTela_PrefereVersionado()
    {
        CriarArquivo("OpenVSPE.UI.exe");
        CriarArquivo("OpenVSPE_Zzz.exe");
        var exec = CriarArquivo("OpenVSPE_V2.0.8.exe");

        Assert.Equal(exec, Vigia.AcharExeTela(_pasta.FullName));
    }

    [Fact]
    public void AcharExeTela_PegaMaisNovo()
    {
        CriarArquivo("OpenVSPE_V2.0.7.exe");
        var exec = CriarArquivo("OpenVSPE_V2.0.8.exe");

        Assert.Equal(exec, Vigia.AcharExeTela(_pasta.FullName));
    }

    [Fact]
    public void AcharExeTela_SemVersionado_PegaPrimeiroEmOrdemDecrescente()
    {
        CriarArquivo("OpenVSPE_A.exe");
        var exec = CriarArquivo("OpenVSPE_B.exe");

        Assert.Equal(exec, Vigia.AcharExeTela(_pasta.FullName));
    }

    [Fact]
    public void AcharExeTela_SemExe_Null()
    {
        Assert.Null(Vigia.AcharExeTela(_pasta.FullName));
        CriarArquivo("reparar_balanca.bat");

        Assert.Null(Vigia.AcharExeTela(_pasta.FullName));
        Assert.Null(Vigia.AcharExeTela(Path.Combine(_pasta.FullName, "inexistente")));
    }

    private string CriarArquivo(string nome)
    {
        var caminho = Path.Combine(_pasta.FullName, nome);
        File.WriteAllText(caminho, "");
        return caminho;
    }
}
