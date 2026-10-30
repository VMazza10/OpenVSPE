using System.Windows;
using OpenVSPE.Core.Models;

namespace OpenVSPE.UI
{
    /// <summary>
    /// Dialogo Serial port settings - identico ao VSPE.
    /// </summary>
    public partial class SerialSettingsDialog : Window
    {
        public int Speed { get; set; } = 9600;
        public string DtrRts { get; set; } = "NO";
        public string ParityValue { get; set; } = "no";
        public int Bits { get; set; } = 8;
        public string StopBitsValue { get; set; } = "1";
        public int ReadIntervalTimeout { get; set; } = 100;

        public SerialSettingsDialog()
        {
            InitializeComponent();
        }

        public void CarregarValores(int speed, OpenVSPE.Core.Models.Parity parity, int dataBits, OpenVSPE.Core.Models.StopBits stopBits, OpenVSPE.Core.Models.Handshake handshake, int readTimeout)
        {
            Speed = speed;
            ReadIntervalTimeout = readTimeout;
            Bits = dataBits;

            SelecionarCombo(cmbSpeed, speed.ToString());
            SelecionarCombo(cmbBits, dataBits.ToString());
            txtReadTimeout.Text = readTimeout.ToString();

            // DTR/RTS
            SelecionarCombo(cmbDtrRts, handshake == OpenVSPE.Core.Models.Handshake.RequestToSend ? "YES" : "NO");

            // Parity
            SelecionarCombo(cmbParity, parity switch
            {
                Parity.None => "no",
                Parity.Odd => "odd",
                Parity.Even => "even",
                Parity.Mark => "mark",
                Parity.Space => "space",
                _ => "no"
            });

            // Stop bits
            SelecionarCombo(cmbStopBits, stopBits switch
            {
                StopBits.One => "1",
                StopBits.OnePointFive => "1.5",
                StopBits.Two => "2",
                _ => "1"
            });
        }

        public Parity GetParity()
        {
            var sel = ObterTextoCombo(cmbParity);
            return sel switch
            {
                "odd" => Parity.Odd,
                "even" => Parity.Even,
                "mark" => Parity.Mark,
                "space" => Parity.Space,
                _ => Parity.None
            };
        }

        public StopBits GetStopBits()
        {
            var sel = ObterTextoCombo(cmbStopBits);
            return sel switch
            {
                "1.5" => StopBits.OnePointFive,
                "2" => StopBits.Two,
                _ => StopBits.One
            };
        }

        public OpenVSPE.Core.Models.Handshake GetHandshake()
        {
            return ObterTextoCombo(cmbDtrRts) == "YES" ? OpenVSPE.Core.Models.Handshake.RequestToSend : OpenVSPE.Core.Models.Handshake.None;
        }

        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            var speedText = cmbSpeed.Text ?? ObterTextoCombo(cmbSpeed);
            Speed = int.TryParse(speedText, out var s) ? s : 9600;
            Bits = int.TryParse(ObterTextoCombo(cmbBits), out var b) ? b : 8;
            ReadIntervalTimeout = int.TryParse(txtReadTimeout.Text, out var rt) ? rt : 100;

            DialogResult = true;
        }

        private void btnCancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
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
            if (combo.IsEditable) combo.Text = texto;
        }

        private static string ObterTextoCombo(System.Windows.Controls.ComboBox combo)
        {
            if (combo.SelectedItem is System.Windows.Controls.ComboBoxItem item)
                return item.Content?.ToString() ?? "";
            return combo.Text ?? "";
        }
    }
}
