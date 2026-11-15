using System.Windows;

namespace OpenVSPE.UI
{
    public partial class SenhaDialog : Window
    {
        public SenhaDialog()
        {
            InitializeComponent();
        }

        public string Senha => txtSenha.Password;

        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
