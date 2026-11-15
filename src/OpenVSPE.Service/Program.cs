using OpenVSPE.Core;
using OpenVSPE.Core.Configuration;
using OpenVSPE.Core.Logging;
using OpenVSPE.Service;

// ===== OpenVSPE Windows Service =====
// Este é o ponto de entrada do serviço Windows.
// Pode rodar como serviço (via SCM) ou como console (para debug).

// Um segundo motor encerra sem exibir aviso.
using var mutexMotor = new Mutex(true, Vigia.MutexMotor, out bool motorNovo);
if (!motorNovo)
    return;

var builder = Host.CreateApplicationBuilder(args);

// Configura como Windows Service
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "OpenVSPE";
});

// Registra dependências
builder.Services.AddSingleton<LogManager>();
builder.Services.AddSingleton<ConfigManager>();
builder.Services.AddHostedService<ChannelManagerService>();
builder.Services.AddHostedService<VigiaTelaService>();

var host = builder.Build();
host.Run();
