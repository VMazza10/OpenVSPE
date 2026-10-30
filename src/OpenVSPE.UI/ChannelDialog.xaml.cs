using RJCP.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Windows;

using OpenVSPE.Core.Models;

namespace OpenVSPE.UI
{
    /// <summary>
    /// Dialogo de configuracao de canal - estilo VSPE "Specify device characteristics".
    /// </summary>
    public partial class ChannelDialog : Window
    {
        // Configuracoes seriais (definidas pelo dialogo Settings)
        private int _speed = 9600;
        private OpenVSPE.Core.Models.Parity _parity = OpenVSPE.Core.Models.Parity.None;
        private int _dataBits = 8;
        private OpenVSPE.Core.Models.StopBits _stopBits = OpenVSPE.Core.Models.StopBits.One;
        private OpenVSPE.Core.Models.Handshake _handshake = OpenVSPE.Core.Models.Handshake.None;
        private int _readTimeout = 100;
        private readonly string _ipLocal;

        public ChannelConfig? Resultado { get; private set; }

        public ChannelDialog(ChannelConfig? configExistente = null)
        {
            InitializeComponent();

            _ipLocal = ObterIpLocal();

            // Preenche IP local automaticamente
            txtIpEscuta.Text = _ipLocal;
            txtIpEscutaPrinter.Text = _ipLocal;

            CarregarPortasCom();
            CarregarImpressoras();

            // Se editando, preenche os campos
            if (configExistente != null)
                PreencherComConfig(configExistente);
        }

        private void PreencherComConfig(ChannelConfig config)
        {
            txtNome.Text = config.Nome;

            if (config.Tipo == ChannelType.Printer)
            {
                rbPrinter.IsChecked = true;
                txtPortaTcpPrinter.Text = config.PortaTcp.ToString();
                txtIpEscutaPrinter.Text = config.IpEscuta;
                SelecionarCombo(cmbImpressora, config.NomeImpressora ?? "");
            }
            else
            {
                rbSerial.IsChecked = true;
                txtPortaTcp.Text = config.PortaTcp.ToString();
                txtIpEscuta.Text = config.IpEscuta;
                SelecionarCombo(cmbPortaCom, config.PortaCom ?? "");
                _speed = config.BaudRate;
                _parity = config.Paridade;
                _dataBits = config.DataBits;
                _stopBits = config.StopBits;
                _handshake = config.ControleFluxo;
                _readTimeout = config.TimeoutLeitura;
                chkIsBalanca.IsChecked = config.IsBalanca;
            }
        }

        private void CarregarPortasCom()
        {
            cmbPortaCom.Items.Clear();
            try
            {
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DEVICEMAP\SERIALCOMM"))
                {
                    if (key != null)
                    {
                        foreach (var name in key.GetValueNames())
                        {
                            var port = key.GetValue(name)?.ToString();
                            if (!string.IsNullOrEmpty(port))
                                cmbPortaCom.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = port });
                        }
                    }
                }
            }
            catch { }
            if (cmbPortaCom.Items.Count > 0)
                cmbPortaCom.SelectedIndex = 0;
        }

        private void CarregarImpressoras()
        {
            cmbImpressora.Items.Clear();
            try
            {
                // Adiciona a impressora virtual de depuração
                cmbImpressora.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = "SCREEN (Visualizar no Bloco de Notas)" });

                foreach (var p in System.Drawing.Printing.PrinterSettings.InstalledPrinters.Cast<string>().OrderBy(x => x))
                    cmbImpressora.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = p });
            }
            catch { }
            if (cmbImpressora.Items.Count > 0)
                cmbImpressora.SelectedIndex = 0;
        }

        private void rbTipo_Changed(object sender, RoutedEventArgs e)
        {
            if (painelSerial == null || painelImpressora == null) return;
            var isSerial = rbSerial.IsChecked == true;
            painelSerial.Visibility = isSerial ? Visibility.Visible : Visibility.Collapsed;
            painelImpressora.Visibility = isSerial ? Visibility.Collapsed : Visibility.Visible;
        }

        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SerialSettingsDialog { Owner = this };
            dlg.CarregarValores(_speed, _parity, _dataBits, _stopBits, _handshake, _readTimeout);

            if (dlg.ShowDialog() == true)
            {
                _speed = dlg.Speed;
                _parity = dlg.GetParity();
                _dataBits = dlg.Bits;
                _stopBits = dlg.GetStopBits();
                _handshake = dlg.GetHandshake();
                _readTimeout = dlg.ReadIntervalTimeout;
            }
        }

        private void btnAtualizarPrinters_Click(object sender, RoutedEventArgs e)
        {
            CarregarImpressoras();
        }

        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            var isSerial = rbSerial.IsChecked == true;

            if (isSerial)
            {
                var com = ObterTextoCombo(cmbPortaCom);
                if (string.IsNullOrEmpty(com))
                {
                    System.Windows.MessageBox.Show("Selecione a porta COM.", "Validacao", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var porta = int.TryParse(txtPortaTcp.Text, out var p) ? p : 9100;
                var ip = string.IsNullOrWhiteSpace(txtIpEscuta.Text) ? _ipLocal : txtIpEscuta.Text.Trim();

                // Gera titulo estilo VSPE: "TCP COM8 : 9100"
                var nome = string.IsNullOrWhiteSpace(txtNome.Text) ? $"TCP {com} : {porta}" : txtNome.Text.Trim();

                Resultado = new ChannelConfig
                {
                    Nome = nome,
                    Tipo = ChannelType.Serial,
                    Habilitado = true,
                    IpEscuta = ip,
                    PortaTcp = porta,
                    PortaCom = com,
                    BaudRate = _speed,
                    DataBits = _dataBits,
                    Paridade = _parity,
                    StopBits = _stopBits,
                    ControleFluxo = _handshake,
                    TimeoutLeitura = _readTimeout,
                    TimeoutEscrita = 500,
                    IsBalanca = chkIsBalanca.IsChecked == true
                };
            }
            else
            {
                var impressora = ObterTextoCombo(cmbImpressora);
                if (string.IsNullOrEmpty(impressora))
                {
                    System.Windows.MessageBox.Show("Selecione a impressora.", "Validacao", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var porta = int.TryParse(txtPortaTcpPrinter.Text, out var p) ? p : 9100;
                var ip = string.IsNullOrWhiteSpace(txtIpEscutaPrinter.Text) ? _ipLocal : txtIpEscutaPrinter.Text.Trim();
                
                // Trata a impressora SCREEN
                if (impressora.StartsWith("SCREEN"))
                    impressora = "SCREEN";

                var nome = string.IsNullOrWhiteSpace(txtNome.Text) ? $"PRN {impressora} : {porta}" : txtNome.Text.Trim();

                Resultado = new ChannelConfig
                {
                    Nome = nome,
                    Tipo = ChannelType.Printer,
                    Habilitado = true,
                    IpEscuta = ip,
                    PortaTcp = porta,
                    NomeImpressora = impressora
                };
            }

            DialogResult = true;
        }

        private void btnCancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        /// <summary>
        /// Obtem o IP local da maquina (primeiro IPv4 que nao seja loopback).
        /// </summary>
        private static string ObterIpLocal()
        {
            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.Connect("8.8.8.8", 80);
                if (socket.LocalEndPoint is IPEndPoint ep)
                    return ep.Address.ToString();
            }
            catch { }

            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                        return ip.ToString();
                }
            }
            catch { }

            return "0.0.0.0";
        }

        private static void SelecionarCombo(System.Windows.Controls.ComboBox combo, string texto)
        {
            for (int i = 0; i < combo.Items.Count; i++)
            {
                if (combo.Items[i] is System.Windows.Controls.ComboBoxItem item && item.Content?.ToString() == texto)
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
        }

        private static string ObterTextoCombo(System.Windows.Controls.ComboBox combo)
        {
            return (combo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "";
        }
    }
}
