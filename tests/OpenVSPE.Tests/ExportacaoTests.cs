using System.Text.Json;
using OpenVSPE.Core.Configuration;
using OpenVSPE.Core.Models;

namespace OpenVSPE.Tests;

public class ExportacaoTests
{
    [Fact]
    public void ExportarEImportar_IdaEVolta()
    {
        var serial = new ChannelConfig { Nome = "Balança", Tipo = ChannelType.Serial, PortaCom = "COM3", PortaTcp = 4001 };
        var impressora = new ChannelConfig { Nome = "Impressora", Tipo = ChannelType.Printer, PortaTcp = 9100 };
        var config = new AppConfig { Canais = [serial, impressora] };

        var canais = ConfigManager.LerCanaisExportados(ConfigManager.ExportarCanais(config));

        Assert.Collection(canais,
            canal => ConferirCanal(serial, canal),
            canal => ConferirCanal(impressora, canal));
    }

    [Fact]
    public void Exportar_LevaSoOsCanais()
    {
        var config = new AppConfig
        {
            ReabrirSozinho = false,
            SenhaTecnico = SenhaTecnico.Gerar("1"),
            Canais = [new ChannelConfig()]
        };

        using var documento = JsonDocument.Parse(ConfigManager.ExportarCanais(config));

        var campo = Assert.Single(documento.RootElement.EnumerateObject());
        Assert.Equal("canais", campo.Name);
        Assert.Single(campo.Value.EnumerateArray());
    }

    [Theory]
    [InlineData("isso não é json")]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("""{"canais":{}}""")]
    public void Importar_JsonInvalido_Recusa(string json)
    {
        Assert.Throws<InvalidDataException>(() => ConfigManager.LerCanaisExportados(json));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"canais":null}""")]
    [InlineData("""{"canais":[]}""")]
    public void Importar_SemCanais_Recusa(string json)
    {
        var erro = Assert.Throws<InvalidDataException>(() => ConfigManager.LerCanaisExportados(json));

        Assert.Equal("O arquivo não tem nenhum canal.", erro.Message);
    }

    [Theory]
    [InlineData("""{"canais":[null]}""")]
    [InlineData("""{"canais":[{"id":null}]}""")]
    [InlineData("""{"canais":[{"id":""}]}""")]
    [InlineData("""{"canais":[{"ipsPermitidos":null}]}""")]
    public void Importar_CanalInvalido_Recusa(string json)
    {
        Assert.Throws<InvalidDataException>(() => ConfigManager.LerCanaisExportados(json));
    }

    [Fact]
    public void Importar_ChannelsJsonCompleto_Aceita()
    {
        const string json = """
            {
              "canais": [{ "nome": "Balança", "tipo": "Serial", "portaCom": "COM3", "portaTcp": 4001 }],
              "reabrirSozinho": false,
              "senhaTecnico": "sal:hash"
            }
            """;

        var canal = Assert.Single(ConfigManager.LerCanaisExportados(json));

        Assert.Equal("Balança", canal.Nome);
        Assert.Equal("COM3", canal.PortaCom);
    }

    [Fact]
    public void Importar_ConflitoDePorta_Recusa()
    {
        var config = new AppConfig
        {
            Canais =
            [
                new ChannelConfig { Nome = "Balança 1", PortaCom = "COM3", PortaTcp = 4001 },
                new ChannelConfig { Nome = "Balança 2", PortaCom = "COM3", PortaTcp = 4001 }
            ]
        };
        var json = ConfigManager.ExportarCanais(config);

        var erro = Assert.Throws<InvalidDataException>(() => ConfigManager.LerCanaisExportados(json));

        Assert.Contains("Conflito de porta TCP", erro.Message);
        Assert.Contains(Environment.NewLine + "Conflito de porta COM", erro.Message);
    }

    private static void ConferirCanal(ChannelConfig esperado, ChannelConfig atual)
    {
        Assert.Equal(esperado.Id, atual.Id);
        Assert.Equal(esperado.Nome, atual.Nome);
        Assert.Equal(esperado.Tipo, atual.Tipo);
        Assert.Equal(esperado.PortaTcp, atual.PortaTcp);
        Assert.Equal(esperado.PortaCom, atual.PortaCom);
    }
}
