using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using OpenVSPE.Core.Logging;
using OpenVSPE.Core.Models;
using OpenVSPE.Core.Interfaces;

namespace OpenVSPE.Serial
{
    public class NativeWin32SerialChannel : IChannel
    {
        private readonly ChannelConfig _config;
        private readonly LogManager _log;
        private TcpListener? _tcpListener;
        private CancellationTokenSource? _cts;
        private Task? _serverTask;
        private Task? _serialReaderTask;
        private IntPtr _hSerial = new IntPtr(INVALID_HANDLE_VALUE);
        private readonly ConcurrentDictionary<Guid, TcpClient> _clients = new();

        // --- Win32 API Definitions ---
        private const uint GENERIC_READ = 0x80000000;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint OPEN_EXISTING = 3;
        private const int INVALID_HANDLE_VALUE = -1;
        private const uint PURGE_RXCLEAR = 0x0008;
        private const uint PURGE_TXCLEAR = 0x0004;

        [StructLayout(LayoutKind.Sequential)]
        private struct DCB
        {
            public int DCBlength;
            public uint BaudRate;
            public uint Flags;
            public ushort wReserved;
            public ushort XonLim;
            public ushort XoffLim;
            public byte ByteSize;
            public byte Parity;
            public byte StopBits;
            public sbyte XonChar;
            public sbyte XoffChar;
            public sbyte ErrorChar;
            public sbyte EofChar;
            public sbyte EvtChar;
            public ushort wReserved1;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct COMMTIMEOUTS
        {
            public uint ReadIntervalTimeout;
            public uint ReadTotalTimeoutMultiplier;
            public uint ReadTotalTimeoutConstant;
            public uint WriteTotalTimeoutMultiplier;
            public uint WriteTotalTimeoutConstant;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetCommState(IntPtr hFile, ref DCB lpDCB);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetCommState(IntPtr hFile, ref DCB lpDCB);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool BuildCommDCB(string lpDef, ref DCB lpDCB);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetCommTimeouts(IntPtr hFile, ref COMMTIMEOUTS lpCommTimeouts);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool PurgeComm(IntPtr hFile, uint dwFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadFile(IntPtr hFile, byte[] lpBuffer, uint nNumberOfBytesToRead, out uint lpNumberOfBytesRead, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteFile(IntPtr hFile, byte[] lpBuffer, uint nNumberOfBytesToWrite, out uint lpNumberOfBytesWritten, IntPtr lpOverlapped);

        public ChannelConfig Config => _config;
        public ChannelStatus Status { get; private set; } = ChannelStatus.Parado;
        public string? UltimoErro { get; private set; }
        public int ConexoesAtivas => _clients.Count;
        public string Nome => _config.Nome;

        public NativeWin32SerialChannel(ChannelConfig config, LogManager logManager)
        {
            _config = config;
            _log = logManager;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            Status = ChannelStatus.Iniciando;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            
            try
            {
                await TryInitializeInternalAsync();
            }
            catch (Exception ex)
            {
                Status = ChannelStatus.Erro;
                UltimoErro = ex.Message;
                _log.Erro($"Falha ao iniciar canal nativo: {ex.Message}", _config.Nome);
                if (ex.Message.Contains("31"))
                {
                    _log.Aviso("DICA: Use o botao 'Reparar Balanca' na interface para destravar o driver CH340.", _config.Nome);
                }
                LimparRecursos();
            }
        }

        private async Task TryInitializeInternalAsync()
        {
            string portName = $@"\\.\{_config.PortaCom}";
            _hSerial = CreateFile(portName, GENERIC_READ | GENERIC_WRITE, 0, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
            
            if (_hSerial.ToInt64() == INVALID_HANDLE_VALUE)
            {
                int err = Marshal.GetLastWin32Error();
                throw new Exception($"Erro {err}: Nao foi possivel abrir a porta {portName}.");
            }

            string parityStr = _config.Paridade switch {
                OpenVSPE.Core.Models.Parity.Odd => "O",
                OpenVSPE.Core.Models.Parity.Even => "E",
                OpenVSPE.Core.Models.Parity.Mark => "M",
                OpenVSPE.Core.Models.Parity.Space => "S",
                _ => "N"
            };
            string stopBitsStr = _config.StopBits == OpenVSPE.Core.Models.StopBits.Two ? "2" : "1";
            string def = $"baud={_config.BaudRate} parity={parityStr} data={_config.DataBits} stop={stopBitsStr}";
            
            DCB dcb = new DCB();
            dcb.DCBlength = Marshal.SizeOf(dcb);
            if (BuildCommDCB(def, ref dcb))
            {
                // fBinary é sempre necessário no Windows COM
                dcb.Flags |= 1; 
                
                // Modo Bematech (Padrao): Tenta forcar Handshake de Hardware (DTR/RTS) se nao for balanca
                if (!_config.IsBalanca && _config.ControleFluxo == OpenVSPE.Core.Models.Handshake.None)
                {
                    // fDtrControl = DTR_CONTROL_ENABLE (bits 4-5) = 1 (0x10)
                    dcb.Flags = (dcb.Flags & ~0x30u) | 0x10u;
                    // fRtsControl = RTS_CONTROL_ENABLE (bits 12-13) = 1 (0x1000)
                    dcb.Flags = (dcb.Flags & ~0x3000u) | 0x1000u;
                }

                if (!SetCommState(_hSerial, ref dcb))
                {
                    int err1 = Marshal.GetLastWin32Error();
                    throw new Exception($"Erro {err1}: O hardware da porta {portName} rejeitou a configuracao.");
                }
            }

            COMMTIMEOUTS timeouts;
            if (_config.IsBalanca)
            {
                // Modo 'Classico' (Balancas): com limites de intervallo pra nao travar o buffer CH340
                timeouts = new COMMTIMEOUTS { ReadIntervalTimeout = 50, ReadTotalTimeoutConstant = 100 };
            }
            else
            {
                // Modo 'Retorno Imediato' (Impressoras Bematech): latencia zero
                timeouts = new COMMTIMEOUTS { 
                    ReadIntervalTimeout = uint.MaxValue, 
                    ReadTotalTimeoutMultiplier = 0, 
                    ReadTotalTimeoutConstant = 0,
                    WriteTotalTimeoutMultiplier = 0,
                    WriteTotalTimeoutConstant = 500
                };
            }
            SetCommTimeouts(_hSerial, ref timeouts);
            PurgeComm(_hSerial, PURGE_RXCLEAR | PURGE_TXCLEAR);

            _tcpListener = new TcpListener(IPAddress.Parse(_config.IpEscuta), _config.PortaTcp);
            _tcpListener.Start();
            
            Status = ChannelStatus.Ativo;
            _log.Info($"Motor Nativo Estabilizado ({_config.PortaCom}).", _config.Nome);

            _serialReaderTask = Task.Run(() => SerialReaderLoop(_cts!.Token));
            _serverTask = Task.Run(() => AcceptTcpLoop(_cts!.Token));
        }

        private async Task AcceptTcpLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var client = await _tcpListener!.AcceptTcpClientAsync(token);
                    
                    // Otimizacao de Performance TCP: Desabilita Algoritmo de Nagle
                    // Essencial para comandos curtos de impressao ESC/POS (evita delay de 200ms)
                    client.NoDelay = true; 
                    
                    var id = Guid.NewGuid();
                    _clients.TryAdd(id, client);
                    _ = Task.Run(() => TcpToSerialPump(id, client, token));
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    if (!token.IsCancellationRequested) _log.Erro($"Erro no TCP: {ex.Message}", _config.Nome);
                }
            }
        }

        private void SerialReaderLoop(CancellationToken token)
        {
            // Eleva prioridade para reduzir latencia de agendamento do Windows
            Thread.CurrentThread.Priority = ThreadPriority.Highest;
            
            byte[] buffer = new byte[2048];
            while (!token.IsCancellationRequested && _hSerial.ToInt64() != INVALID_HANDLE_VALUE)
            {
                uint bytesRead;
                if (ReadFile(_hSerial, buffer, (uint)buffer.Length, out bytesRead, IntPtr.Zero) && bytesRead > 0)
                {
                    foreach (var client in _clients.Values)
                    {
                        try { if (client.Connected) client.GetStream().Write(buffer, 0, (int)bytesRead); } catch {}
                    }
                }
                
                if (_config.IsBalanca)
                {
                    // Previne flooding de CPU no CH340 em pollings continuos de balanca (funcionalidade original)
                    Thread.Sleep(10);
                }
            }
        }

        private async Task TcpToSerialPump(Guid id, TcpClient client, CancellationToken token)
        {
            // Tambem eleva prioridade do pump de escrita
            Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;

            try
            {
                using var stream = client.GetStream();
                byte[] buffer = new byte[1024];

                while (!token.IsCancellationRequested && client.Connected)
                {
                    // ReadAsync bloqueia até chegar dado (zero latência vs polling anterior)
                    int n;
                    try { n = await stream.ReadAsync(buffer, 0, buffer.Length, token); }
                    catch (OperationCanceledException) { break; }
                    catch (IOException) { break; }

                    if (n == 0) break; // Conexão encerrada pelo cliente

                    uint written;
                    WriteFile(_hSerial, buffer, (uint)n, out written, IntPtr.Zero);
                }
            }
            catch { }
            finally
            {
                _clients.TryRemove(id, out _);
                if (client.Connected) client.Close();
            }
        }

        public async Task StopAsync()
        {
            Status = ChannelStatus.Parado;
            _cts?.Cancel();
            LimparRecursos();
            if (_serverTask != null) await Task.WhenAny(_serverTask, Task.Delay(1000));
        }

        private void LimparRecursos()
        {
            try { _tcpListener?.Stop(); } catch {}
            try 
            {
                if (_hSerial.ToInt64() != INVALID_HANDLE_VALUE)
                {
                    CloseHandle(_hSerial);
                    _hSerial = new IntPtr(INVALID_HANDLE_VALUE);
                }
            } catch {}
            foreach (var c in _clients.Values) try { c.Close(); } catch {}
            _clients.Clear();
        }

        public async ValueTask DisposeAsync() => await StopAsync();
        public void Dispose() { _cts?.Cancel(); LimparRecursos(); }
    }
}
