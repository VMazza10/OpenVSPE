# ===================================================================
# OpenVSPE - Script de Instalacao do Servico Windows
# Execute este script como Administrador (uma unica vez).
# ===================================================================

param(
    [string]$ServiceName = "OpenVSPE",
    [string]$DisplayName = "OpenVSPE - Gerenciador de Canais Serial TCP",
    [string]$Description = "Expoe portas seriais COM e impressoras USB como servidores TCP de rede.",
    [string]$InstallDir = "$env:ProgramFiles\OpenVSPE"
)

# Verifica se esta rodando como admin
$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "ERRO: Este script precisa ser executado como Administrador." -ForegroundColor Red
    Write-Host "Clique com botao direito no PowerShell e selecione Executar como administrador." -ForegroundColor Yellow
    exit 1
}

$ErrorActionPreference = "Stop"

Write-Host "============================================" -ForegroundColor Cyan
Write-Host " OpenVSPE - Instalacao do Servico" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# 1. Publica o servico
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Join-Path $scriptDir "..\src\OpenVSPE.Service"
$publishDir = Join-Path $InstallDir "service"

Write-Host "[1/5] Publicando o servico..." -ForegroundColor Yellow
dotnet publish $projectDir -c Release -o $publishDir --self-contained false
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERRO: Falha ao publicar o servico." -ForegroundColor Red
    exit 1
}
Write-Host "  Publicado em: $publishDir" -ForegroundColor Green

# 2. Publica a UI
$uiProjectDir = Join-Path $scriptDir "..\src\OpenVSPE.UI"
$uiPublishDir = Join-Path $InstallDir "ui"

Write-Host "[2/5] Publicando a interface..." -ForegroundColor Yellow
dotnet publish $uiProjectDir -c Release -o $uiPublishDir --self-contained false
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERRO: Falha ao publicar a interface." -ForegroundColor Red
    exit 1
}
Write-Host "  Publicado em: $uiPublishDir" -ForegroundColor Green

# 3. Cria pasta de dados
$dataDir = Join-Path $env:ProgramData "OpenVSPE"
$logsDir = Join-Path $dataDir "logs"
Write-Host "[3/5] Criando pastas de dados..." -ForegroundColor Yellow
New-Item -ItemType Directory -Force -Path $dataDir | Out-Null
New-Item -ItemType Directory -Force -Path $logsDir | Out-Null
Write-Host "  Dados em: $dataDir" -ForegroundColor Green

# 4. Registra a fonte de log no Event Viewer
Write-Host "[4/5] Registrando fonte de log no Event Viewer..." -ForegroundColor Yellow
try {
    if (-not [System.Diagnostics.EventLog]::SourceExists($ServiceName)) {
        [System.Diagnostics.EventLog]::CreateEventSource($ServiceName, "Application")
        Write-Host "  Fonte registrada no Event Viewer." -ForegroundColor Green
    }
    else {
        Write-Host "  Fonte ja existe no Event Viewer." -ForegroundColor Green
    }
}
catch {
    Write-Host "  Aviso: Nao foi possivel registrar no Event Viewer: $_" -ForegroundColor Yellow
}

# 5. Registra o servico Windows
$exePath = Join-Path $publishDir "OpenVSPE.Service.exe"
Write-Host "[5/5] Registrando servico Windows..." -ForegroundColor Yellow

# Remove servico existente se houver
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existingService) {
    Write-Host "  Parando servico existente..." -ForegroundColor Yellow
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

sc.exe create $ServiceName binPath= "`"$exePath`"" start= auto DisplayName= "$DisplayName"
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERRO: Falha ao registrar o servico." -ForegroundColor Red
    exit 1
}

# Configura descricao e recovery
sc.exe description $ServiceName "$Description" | Out-Null
sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/10000/restart/30000 | Out-Null

# Inicia o servico
Write-Host ""
Write-Host "Iniciando servico..." -ForegroundColor Yellow
Start-Service -Name $ServiceName
Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host " Instalacao concluida com sucesso!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host ""
Write-Host "  Servico: $ServiceName" -ForegroundColor White
Write-Host "  Status:  $(Get-Service $ServiceName | Select-Object -ExpandProperty Status)" -ForegroundColor White
Write-Host "  Inicio:  Automatico" -ForegroundColor White
Write-Host "  Dados:   $dataDir" -ForegroundColor White
Write-Host "  Logs:    $logsDir" -ForegroundColor White
Write-Host "  UI:      $uiPublishDir\OpenVSPE.UI.exe" -ForegroundColor White
Write-Host ""
Write-Host "O servico ira iniciar automaticamente com o Windows." -ForegroundColor Cyan
Write-Host "Use a interface OpenVSPE.UI.exe para configurar os canais." -ForegroundColor Cyan
