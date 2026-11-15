using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using OpenVSPE.Core;
using OpenVSPE.Core.Configuration;
using OpenVSPE.Core.Models;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace OpenVSPE.UI
{
    public partial class ConfiguracoesDialog : Window
    {
        private const string FiltroJson = "Arquivos JSON (*.json)|*.json";
        private readonly AppConfig _config;
        private readonly ConfigManager _configManager;
        private readonly string? _atalhoInicializar;
        private bool _reiniciandoMotor;

        public ConfiguracoesDialog(AppConfig config, ConfigManager configManager)
        {
            InitializeComponent();
            _config = config;
            _configManager = configManager;

            var pastaInicializar = Environment.GetFolderPath(
                Environment.SpecialFolder.Startup, Environment.SpecialFolderOption.DoNotVerify);
            if (!string.IsNullOrEmpty(pastaInicializar))
                _atalhoInicializar = Path.Combine(pastaInicializar, "OpenVSPE.lnk");

            chkAbrirComWindows.IsChecked = File.Exists(_atalhoInicializar);
            chkReabrirSozinho.IsChecked = _config.ReabrirSozinho;
            chkRemoverSenha.IsEnabled = SenhaTecnico.TemSenha(_config.SenhaTecnico);
        }

        public bool CanaisImportados { get; private set; }

        private void btnSalvar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var senha = _config.SenhaTecnico;
                var novaSenha = txtNovaSenha.Password;
                if (chkRemoverSenha.IsChecked == true)
                    senha = null;
                else if (novaSenha.Length > 0)
                    senha = SenhaTecnico.Gerar(novaSenha);

                SalvarOpcoes(chkAbrirComWindows.IsChecked == true, chkReabrirSozinho.IsChecked == true, senha);
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MostrarErro($"Não foi possível salvar as configurações.\n\n{ex.Message}");
            }
        }

        private void SalvarOpcoes(bool abrirComWindows, bool reabrirSozinho, string? senha)
        {
            var caminhoAtalho = _atalhoInicializar
                ?? throw new IOException("A pasta Inicializar do Windows não foi encontrada.");
            var atalhoAnterior = File.Exists(caminhoAtalho) ? File.ReadAllBytes(caminhoAtalho) : null;
            var reabrirAnterior = _config.ReabrirSozinho;
            var senhaAnterior = _config.SenhaTecnico;

            try
            {
                AtualizarAtalho(caminhoAtalho, abrirComWindows);
                _config.ReabrirSozinho = reabrirSozinho;
                _config.SenhaTecnico = senha;
                _configManager.Salvar(_config);
            }
            catch (Exception ex)
            {
                _config.ReabrirSozinho = reabrirAnterior;
                _config.SenhaTecnico = senhaAnterior;
                try
                {
                    if (atalhoAnterior is null)
                        File.Delete(caminhoAtalho);
                    else
                        File.WriteAllBytes(caminhoAtalho, atalhoAnterior);
                }
                catch (Exception erroAtalho)
                {
                    throw new IOException(
                        $"{ex.Message}\nNão foi possível restaurar o atalho de Inicializar. {erroAtalho.Message}", ex);
                }

                throw;
            }
        }

        private static void AtualizarAtalho(string caminhoAtalho, bool abrirComWindows)
        {
            if (!abrirComWindows)
            {
                if (File.Exists(caminhoAtalho))
                    File.Delete(caminhoAtalho);
                return;
            }

            var executavel = Environment.ProcessPath
                ?? throw new IOException("Não foi possível localizar o executável do OpenVSPE.");
            var pastaExecutavel = Path.GetDirectoryName(executavel)!;
            Directory.CreateDirectory(Path.GetDirectoryName(caminhoAtalho)!);

            var tipoShell = Type.GetTypeFromProgID("WScript.Shell")
                ?? throw new InvalidOperationException("O componente WScript.Shell não está disponível.");
            var shell = Activator.CreateInstance(tipoShell)
                ?? throw new InvalidOperationException("Não foi possível abrir o componente WScript.Shell.");
            dynamic? atalho = null;
            try
            {
                atalho = ((dynamic)shell).CreateShortcut(caminhoAtalho);
                atalho!.TargetPath = executavel;
                atalho.WorkingDirectory = pastaExecutavel;
                atalho.IconLocation = Path.Combine(pastaExecutavel, "app.ico") + ",0";
                atalho.Save();
            }
            finally
            {
                if (atalho is not null)
                    Marshal.ReleaseComObject((object)atalho);
                Marshal.ReleaseComObject(shell);
            }
        }

        private async void btnReiniciarMotor_Click(object sender, RoutedEventArgs e)
        {
            _reiniciandoMotor = true;
            conteudo.IsEnabled = false;
            btnReiniciarMotor.Content = "Reiniciando…";
            try
            {
                await EncerrarMotoresAsync();

                if (!_config.ReabrirSozinho
                    && !Vigia.MotorAberto()
                    && !Vigia.AbrirMotor(AppDomain.CurrentDomain.BaseDirectory))
                {
                    throw new InvalidOperationException("Não foi possível abrir o motor do OpenVSPE.");
                }

                using var prazo = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                while (!Vigia.MotorAberto())
                    await Task.Delay(100, prazo.Token);

                MessageBox.Show(this, "Motor reiniciado.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (OperationCanceledException)
            {
                MostrarErro("O tempo para reiniciar o motor se esgotou.");
            }
            catch (Exception ex)
            {
                MostrarErro($"Não foi possível reiniciar o motor.\n\n{ex.Message}");
            }
            finally
            {
                _reiniciandoMotor = false;
                btnReiniciarMotor.Content = "Reiniciar motor";
                conteudo.IsEnabled = true;
            }
        }

        private static async Task EncerrarMotoresAsync()
        {
            var motores = Process.GetProcessesByName(Vigia.ProcessoMotor);
            try
            {
                foreach (var motor in motores)
                {
                    try
                    {
                        motor.Kill();
                    }
                    catch (Exception) when (motor.HasExited)
                    {
                        // O motor já encerrou.
                    }
                }

                using var prazo = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await Task.WhenAll(motores.Select(motor => motor.WaitForExitAsync(prazo.Token)));
            }
            finally
            {
                foreach (var motor in motores)
                    motor.Dispose();
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            if (_reiniciandoMotor)
                e.Cancel = true;
        }

        private void btnExportarCanais_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var data = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                var arquivo = new SaveFileDialog
                {
                    Filter = FiltroJson,
                    DefaultExt = ".json",
                    FileName = $"OpenVSPE-canais-{Environment.MachineName}-{data}.json"
                };
                if (arquivo.ShowDialog(this) == true)
                    File.WriteAllText(arquivo.FileName, ConfigManager.ExportarCanais(_config));
            }
            catch (Exception ex)
            {
                MostrarErro($"Não foi possível exportar os canais.\n\n{ex.Message}");
            }
        }

        private void btnImportarCanais_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var arquivo = new OpenFileDialog { Filter = FiltroJson };
                if (arquivo.ShowDialog(this) != true)
                    return;

                var canais = ConfigManager.LerCanaisExportados(File.ReadAllText(arquivo.FileName));
                var canaisAnteriores = _config.Canais;
                var resposta = MessageBox.Show(this,
                    $"Isso troca os {canaisAnteriores.Count} canais atuais pelos {canais.Count} do arquivo. Continuar?",
                    "Importar canais", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
                if (resposta != MessageBoxResult.Yes)
                    return;

                _config.Canais = canais;
                try
                {
                    _configManager.Salvar(_config);
                }
                catch
                {
                    _config.Canais = canaisAnteriores;
                    throw;
                }

                CanaisImportados = true;
            }
            catch (InvalidDataException ex)
            {
                MostrarErro(ex.Message);
            }
            catch (Exception ex)
            {
                MostrarErro($"Não foi possível importar os canais.\n\n{ex.Message}");
            }
        }

        private void MostrarErro(string mensagem)
        {
            MessageBox.Show(this, mensagem, Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
