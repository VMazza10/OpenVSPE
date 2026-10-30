using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using OpenVSPE.Core.Configuration;
using OpenVSPE.Core.Models;
using OpenVSPE.UI.Services;
using Path = System.IO.Path;
using MessageBox = System.Windows.MessageBox;

using System.Collections.ObjectModel;
using System.Text.Json;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OpenVSPE.UI
{
    /// <summary>
    /// Janela principal do OpenVSPE - estilo VSPE com toolbar, lista e logs.
    /// Minimiza para o system tray (bandeja do sistema).
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ConfigManager _configManager;
        private AppConfig _config;
        private readonly DispatcherTimer _statusTimer;
        private System.Windows.Forms.NotifyIcon? _trayIcon;
        private readonly ObservableCollection<CanalItem> _canalItems = new();

        public MainWindow()
        {
            InitializeComponent();
            _configManager = new ConfigManager();
            _config = _configManager.Carregar();

            // Título dinâmico — lê versão do assembly automaticamente
            var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            this.Title = $"OpenVSPE v{ver?.Major}.{ver?.Minor}.{ver?.Build}";

            AtualizarLista();
            lstCanais.ItemsSource = _canalItems;

            // Mostra IP local no status bar
            txtIpLocal.Text = "IP: " + ObterIpLocal();

            // Configura o system tray icon
            ConfigurarTrayIcon();

            // Timer para status do servico
            _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _statusTimer.Tick += async (s, e) => await SincronizarTudoAsync();
            _statusTimer.Start();
            _ = SincronizarTudoAsync();

            // Inicia minimizado na bandeja
            this.WindowState = WindowState.Minimized;
            this.ShowInTaskbar = false;
            this.Hide();
        }

        private bool _isSyncing;

        private async Task SincronizarTudoAsync()
        {
            if (_isSyncing) return;
            _isSyncing = true;
            try
            {
                await VerificarStatusServicoAsync();
                await AtualizarStatusCanaisAsync();
            }
            finally
            {
                _isSyncing = false;
            }
        }

        private void ConfigurarTrayIcon()
        {
            // Carrega o icone do recurso embutido
            System.Drawing.Icon? icon = null;
            var icoPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
            if (System.IO.File.Exists(icoPath))
            {
                icon = new System.Drawing.Icon(icoPath);
            }
            else
            {
                // Fallback: usa icone padrao da aplicacao
                icon = System.Drawing.SystemIcons.Application;
            }

            _trayIcon = new System.Windows.Forms.NotifyIcon
            {
                Icon = icon,
                Text = "OpenVSPE - Serial/TCP Bridge",
                Visible = true
            };

            // Menu de contexto do tray
            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("Abrir OpenVSPE", null, (s, e) => MostrarJanela());
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            menu.Items.Add("Sair", null, (s, e) => FecharAplicacao());
            _trayIcon.ContextMenuStrip = menu;

            // Duplo clique abre a janela
            _trayIcon.DoubleClick += (s, e) => MostrarJanela();
        }

        private void MostrarJanela()
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.ShowInTaskbar = true;
            this.Activate();
        }

        private void FecharAplicacao()
        {
            _trayIcon?.Dispose();
            _trayIcon = null;
            System.Windows.Application.Current.Shutdown();
        }

        // Minimizar para tray ao fechar a janela (X)
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true; // Nao fecha, minimiza
            this.WindowState = WindowState.Minimized;
            this.ShowInTaskbar = false;
            this.Hide();
        }

        // Minimizar para tray ao minimizar
        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            if (WindowState == WindowState.Minimized)
            {
                this.ShowInTaskbar = false;
                this.Hide();
            }
        }

        private void AtualizarLista()
        {
            _canalItems.Clear();
            foreach (var c in _config.Canais)
            {
                _canalItems.Add(new CanalItem
                {
                    Id = c.Id,
                    Titulo = c.Nome,
                    Tipo = c.Tipo == ChannelType.Serial ? "TcpServer" : "PrinterRAW",
                    Status = "Loading...",
                    ColorStatus = System.Windows.Media.Brushes.Gray,
                    Conexoes = 0
                });
            }
        }

        private async Task AtualizarStatusCanaisAsync()
        {
            try
            {
                var json = await PipeClient.EnviarComandoAsync("status");
                if (string.IsNullOrEmpty(json) || json.StartsWith("ERRO")) return;

                // Debug: para vermos o que o servico retorna
                // AdicionarLog($"Debug Status: {json}"); 

                var statuses = JsonSerializer.Deserialize<Dictionary<string, ChannelStatusInfo>>(json);
                if (statuses == null) return;

                foreach (var item in _canalItems)
                {
                    if (statuses.TryGetValue(item.Id, out var info))
                    {
                        item.Status = info.Status switch
                        {
                            ChannelStatus.Ativo => "Ready",
                            ChannelStatus.Parado => "Pausado",
                            ChannelStatus.Erro => "Erro",
                            ChannelStatus.Iniciando => "Iniciando",
                            ChannelStatus.Reconectando => "Reconectando",
                            _ => "Unknown"
                        };

                        item.ColorStatus = info.Status switch
                        {
                            ChannelStatus.Ativo => System.Windows.Media.Brushes.LimeGreen,
                            ChannelStatus.Parado => System.Windows.Media.Brushes.OrangeRed,
                            ChannelStatus.Erro => System.Windows.Media.Brushes.Orange,
                            ChannelStatus.Iniciando => System.Windows.Media.Brushes.Yellow,
                            ChannelStatus.Reconectando => System.Windows.Media.Brushes.Cyan,
                            _ => System.Windows.Media.Brushes.Gray
                        };

                        item.Conexoes = info.Conexoes;
                    }
                    else
                    {
                        item.Status = "Pausado";
                        item.ColorStatus = System.Windows.Media.Brushes.OrangeRed;
                    }
                }
            }
            catch { }
        }

        // ===== TOOLBAR =====

        private void btnNovo_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new ChannelDialog { Owner = this };
            if (dlg.ShowDialog() == true && dlg.Resultado != null)
            {
                _config.Canais.Add(dlg.Resultado);
                _configManager.Salvar(_config);
                AtualizarLista();
                AdicionarLog($"Canal criado: {dlg.Resultado.Nome}");
                NotificarServico("reload");
            }
        }

        private void btnEditar_Click(object sender, RoutedEventArgs e)
        {
            if (lstCanais.SelectedItem is not CanalItem item) return;

            var config = _config.Canais.FirstOrDefault(c => c.Id == item.Id);
            if (config == null) return;

            var dlg = new ChannelDialog(config) { Owner = this };
            if (dlg.ShowDialog() == true && dlg.Resultado != null)
            {
                dlg.Resultado.Id = config.Id;
                var idx = _config.Canais.FindIndex(c => c.Id == config.Id);
                if (idx >= 0) _config.Canais[idx] = dlg.Resultado;
                _configManager.Salvar(_config);
                AtualizarLista();
                AdicionarLog($"Canal editado: {dlg.Resultado.Nome}");
                NotificarServico("reload");
            }
        }

        private void btnRemover_Click(object sender, RoutedEventArgs e)
        {
            if (lstCanais.SelectedItem is not CanalItem item) return;

            var config = _config.Canais.FirstOrDefault(c => c.Id == item.Id);
            if (config == null) return;

            if (System.Windows.MessageBox.Show($"Remover canal '{config.Nome}'?", "Confirmar",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _config.Canais.Remove(config);
                _configManager.Salvar(_config);
                AtualizarLista();
                AdicionarLog($"Canal removido: {config.Nome}");
                NotificarServico("reload");
            }
        }

        private void btnTestar_Click(object sender, RoutedEventArgs e)
        {
            if (lstCanais.SelectedItem is not CanalItem item) return;

            var config = _config.Canais.FirstOrDefault(c => c.Id == item.Id);
            if (config == null) return;

            if (config.Tipo != ChannelType.Printer)
            {
                System.Windows.MessageBox.Show("O teste de impressao esta disponivel apenas para canais do tipo Printer.", "Atencao", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (string.IsNullOrEmpty(config.NomeImpressora))
            {
                System.Windows.MessageBox.Show("O nome da impressora nao esta configurado neste canal.", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                var text = $"--- TESTE DE IMPRESSAO OpenVSPE ---\n\nCanal: {config.Nome}\nData: {DateTime.Now:dd/MM/yyyy HH:mm:ss}\n\nImpressora configurada corretamente!\n\n\n\n\n\x1B\x69"; 
                var data = System.Text.Encoding.ASCII.GetBytes(text); 

                if (config.NomeImpressora == "SCREEN")
                {
                    var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"openvspe_teste_{Guid.NewGuid():N}.txt");
                    System.IO.File.WriteAllBytes(tempPath, data);
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "notepad.exe",
                        Arguments = tempPath,
                        UseShellExecute = true
                    });
                    AdicionarLog($"Teste de impressao enviado para a TELA (Notepad).");
                }
                else
                {
                    bool sucesso = OpenVSPE.Printing.RawPrinterHelper.SendRawData(config.NomeImpressora, data, "Teste OpenVSPE");
                    if (sucesso)
                    {
                        AdicionarLog($"Teste enviado com sucesso para {config.NomeImpressora}.");
                        System.Windows.MessageBox.Show($"Teste de impressao enviado com sucesso para a impressora:\n{config.NomeImpressora}\n\nVerifique se o papel foi impresso.", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                AdicionarLog($"Erro no teste de impressao: {ex.Message}");
                System.Windows.MessageBox.Show($"Erro ao enviar teste de impressao:\n\n{ex.Message}\n\nVerifique se o nome da impressora esta EXATAMENTE igual ao Painel de Controle.", "Erro de Impressao", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void btnReparar_Click(object sender, RoutedEventArgs e)
        {
            if (lstCanais.SelectedItem is not CanalItem item)
            {
                System.Windows.MessageBox.Show("Selecione um canal de balança para reparar.", "Atenção", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var config = _config.Canais.FirstOrDefault(c => c.Id == item.Id);
            if (config == null || config.Tipo != ChannelType.Serial)
            {
                System.Windows.MessageBox.Show("Esta função está disponível apenas para canais de Balança (Serial).", "Atenção", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (System.Windows.MessageBox.Show($"Deseja realizar o REPARO FORÇADO na porta {config.PortaCom}?\n\nIsso irá remover o dispositivo do Windows e forçar uma nova instalação automática.", "Confirmar Reparo Pesado",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            Cursor = System.Windows.Input.Cursors.Wait;
            try
            {
                string port = config.PortaCom;
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string batPath = System.IO.Path.Combine(baseDir, "reparar_balanca.bat");
                
                // Tenta encontrar em subpastas se necessario
                if (!System.IO.File.Exists(batPath))
                    batPath = System.IO.Path.Combine(baseDir, "ui", "reparar_balanca.bat");

                AdicionarLog($"[DEBUG] Caminho do script: {batPath}");

                if (!System.IO.File.Exists(batPath))
                {
                    throw new Exception($"Script de reparo nao encontrado em: {batPath}");
                }

                // COMANDO NUCLEAR FINAL: Usa o 'start' do CMD que forca a abertura da janela
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c start \"Reparo de Balanca\" /d \"{System.IO.Path.GetDirectoryName(batPath)}\" reparar_balanca.bat {port}",
                    UseShellExecute = true,
                    Verb = "runas"
                };

                AdicionarLog($"[REPARO] Disparando script externo para {port}...");
                var proc = System.Diagnostics.Process.Start(psi);
                if (proc != null)
                {
                    await proc.WaitForExitAsync();
                    AdicionarLog($"[REPARO] Processo de reparo finalizado.");
                    System.Windows.MessageBox.Show($"O procedimento de reparo foi enviado.\n\nVerifique se a janela do PowerShell abriu e se a balança voltou a funcionar.", "Informação", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                AdicionarLog($"[ERRO REPARO] {ex.Message}");
                System.Windows.MessageBox.Show($"Falha no reparo pesado: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Cursor = System.Windows.Input.Cursors.Arrow;
            }
        }

        private void btnLogs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var logPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "OpenVSPE", "logs");
                if (!System.IO.Directory.Exists(logPath)) System.IO.Directory.CreateDirectory(logPath);
                System.Diagnostics.Process.Start("explorer.exe", logPath);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Erro ao abrir pasta de logs: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnSalvar_Click(object sender, RoutedEventArgs e)
        {
            _configManager.Salvar(_config);
            AdicionarLog("Configuracao salva.");
        }

        private async void btnIniciarTodos_Click(object sender, RoutedEventArgs e)
        {
            AdicionarLog("Iniciando todos os canais...");
            await NotificarServico("start");
        }

        private async void btnPararTodos_Click(object sender, RoutedEventArgs e)
        {
            AdicionarLog("Parando todos os canais...");
            await NotificarServico("stop");
        }

        private void lstCanais_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            btnEditar_Click(sender, e);
        }

        // ===== SERVICO =====

        private async Task VerificarStatusServicoAsync()
        {
            try
            {
                var disponivel = await PipeClient.ServicoDisponivelAsync();
                statusDot.Fill = disponivel ? System.Windows.Media.Brushes.LimeGreen : System.Windows.Media.Brushes.OrangeRed;
                txtStatus.Text = disponivel ? "Servico ativo" : "Servico inativo";
            }
            catch
            {
                statusDot.Fill = System.Windows.Media.Brushes.Gray;
                txtStatus.Text = "Erro ao verificar servico";
            }
        }

        private async Task NotificarServico(string comando)
        {
            try
            {
                var resp = await PipeClient.EnviarComandoAsync(comando);
                AdicionarLog($"Servico: {resp}");
            }
            catch (Exception ex)
            {
                AdicionarLog($"Erro ao comunicar com servico: {ex.Message}");
            }
        }

        // ===== LOG =====

        private void AdicionarLog(string msg)
        {
            var ts = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
            txtLog.AppendText($"({ts}) {msg}\n");
            txtLog.ScrollToEnd();
        }

        // ===== HELPERS =====

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
            return "0.0.0.0";
        }
    }

    public class CanalItem : INotifyPropertyChanged
    {
        public string Id { get; set; } = "";
        public string Titulo { get; set; } = "";
        public string Tipo { get; set; } = "";

        private string _status = "";
        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        private System.Windows.Media.Brush _colorStatus = System.Windows.Media.Brushes.Gray;
        public System.Windows.Media.Brush ColorStatus
        {
            get => _colorStatus;
            set { _colorStatus = value; OnPropertyChanged(); }
        }

        private int _conexoes;
        public int Conexoes
        {
            get => _conexoes;
            set { _conexoes = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}