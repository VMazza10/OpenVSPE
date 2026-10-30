
using System.Text.Json.Serialization;

namespace OpenVSPE.Core.Models
{
    /// <summary>
    /// Configuração base de um canal. Contém propriedades comuns a todos os tipos.
    /// </summary>
    public class ChannelConfig
    {
        /// <summary>
        /// Identificador único do canal.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// Nome amigável do canal (ex: "Balança PDV", "Impressora Caixa 1").
        /// </summary>
        public string Nome { get; set; } = string.Empty;

        /// <summary>
        /// Tipo do canal: Serial ou Printer.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ChannelType Tipo { get; set; } = ChannelType.Serial;

        /// <summary>
        /// Se o canal está habilitado para iniciar automaticamente.
        /// </summary>
        public bool Habilitado { get; set; } = true;

        /// <summary>
        /// IP local para escuta TCP (ex: "192.168.1.50" ou "0.0.0.0" para todos).
        /// </summary>
        public string IpEscuta { get; set; } = "0.0.0.0";

        /// <summary>
        /// Porta TCP para escuta (ex: 4001, 9100).
        /// </summary>
        public int PortaTcp { get; set; } = 4001;

        /// <summary>
        /// Lista de IPs permitidos. Se vazia, aceita qualquer IP.
        /// </summary>
        public List<string> IpsPermitidos { get; set; } = new();

        // ===== Propriedades Serial (usadas quando Tipo == Serial) =====

        /// <summary>
        /// Porta COM (ex: "COM3", "COM8").
        /// </summary>
        public string? PortaCom { get; set; }

        /// <summary>
        /// Velocidade serial em bps (ex: 9600, 115200).
        /// </summary>
        public int BaudRate { get; set; } = 9600;

        /// <summary>
        /// Bits de dados (5, 6, 7 ou 8).
        /// </summary>
        public int DataBits { get; set; } = 8;

        /// <summary>
        /// Bits de parada.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public StopBits StopBits { get; set; } = StopBits.One;

        /// <summary>
        /// Paridade serial.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public Parity Paridade { get; set; } = Parity.None;

        /// <summary>
        /// Controle de fluxo (hardware, software ou nenhum).
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public Handshake ControleFluxo { get; set; } = Handshake.None;

        /// <summary>
        /// Indica se o dispositivo conectado é uma balança (Modo Simples).
        /// Desativa hardware handshakes restritivos e ajusta timeouts para leitura contínua.
        /// </summary>
        public bool IsBalanca { get; set; } = false;

        /// <summary>
        /// Timeout de leitura serial em milissegundos.
        /// </summary>
        public int TimeoutLeitura { get; set; } = 100;

        /// <summary>
        /// Timeout de escrita serial em milissegundos.
        /// </summary>
        public int TimeoutEscrita { get; set; } = 500;

        // ===== Propriedades Printer (usadas quando Tipo == Printer) =====

        /// <summary>
        /// Nome da impressora instalada no Windows (ex: "EPSON TM-T20II Receipt").
        /// </summary>
        public string? NomeImpressora { get; set; }
    }
}
