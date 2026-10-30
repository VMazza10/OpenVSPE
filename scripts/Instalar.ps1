# ===================================================================
# INSTALADOR UNIFICADO - OpenVSPE
# ===================================================================

# 1. Verificar privilégios de administrador
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "ERRO: Rode este script como ADMINISTRADOR." -ForegroundColor Red
    Pause
    exit
}

$InstallDir = "$env:ProgramFiles\OpenVSPE"
$SourceDir = $PSScriptRoot
$UiPath = Join-Path $InstallDir "ui\OpenVSPE.UI.exe"
$IconPath = Join-Path $InstallDir "ui\app.ico"

Write-Host "============================" -ForegroundColor Cyan
Write-Host " Instalando OpenVSPE... " -ForegroundColor Cyan
Write-Host "============================" -ForegroundColor Cyan

# 2. Parar serviço e UI se estiverem rodando
Stop-Service OpenVSPE -ErrorAction SilentlyContinue
Stop-Process -Name OpenVSPE.UI -ErrorAction SilentlyContinue

# 3. Criar diretórios
New-Item -Path $InstallDir -ItemType Directory -Force | Out-Null
New-Item -Path (Join-Path $InstallDir "config") -ItemType Directory -Force | Out-Null
New-Item -Path (Join-Path $InstallDir "logs") -ItemType Directory -Force | Out-Null

# 4. Copiar arquivos (da pasta de publicação)
Write-Host "Copiando arquivos..." -ForegroundColor Yellow
# Assume que os arquivos foram publicados em TEST_UI (pode ser ajustado)
Copy-Item -Path "$SourceDir\TEST_UI\*" -Destination (Join-Path $InstallDir "ui") -Recurse -Force
# Assume que o serviço está em src/OpenVSPE.Service/... (ou pasta similar)
# Vou copiar da pasta de publicação do serviço se disponível
$ServiceSource = "$env:ProgramFiles\OpenVSPE\service"
if (!(Test-Path $ServiceSource)) {
    Write-Host "AVISO: Pasta do serviço não encontrada em ProgramFiles. Tentando compilar..." -ForegroundColor Gray
    # Se não existe, tentamos localizar na pasta do projeto
}

# 5. Instalar o Serviço
Write-Host "Registrando servico no Windows..." -ForegroundColor Yellow
$ServiceExe = Join-Path $InstallDir "service\OpenVSPE.Service.exe"
if (Test-Path $ServiceExe) {
    if (Get-Service OpenVSPE -ErrorAction SilentlyContinue) {
        sc.exe delete OpenVSPE | Out-Null
        Start-Sleep -Seconds 2
    }
    New-Service -Name "OpenVSPE" -BinaryPathName "`"$ServiceExe`"" -DisplayName "OpenVSPE Emulation Service" -Description "Ponte TCP/Serial/Printer." -StartupType Automatic
    Start-Service OpenVSPE
}

# 6. Configurar Firewall
Write-Host "Configurando Firewall..." -ForegroundColor Yellow
New-NetFirewallRule -DisplayName "OpenVSPE TCP" -Direction Inbound -LocalPort 9100-9110 -Protocol TCP -Action Allow -ErrorAction SilentlyContinue

# 7. Criar Atalho na Área de Trabalho
Write-Host "Criando atalho na Area de Trabalho..." -ForegroundColor Yellow
$WshShell = New-Object -ComObject WScript.Shell
$DesktopPath = [System.Environment]::GetFolderPath("Desktop")
$Shortcut = $WshShell.CreateShortcut((Join-Path $DesktopPath "OpenVSPE.lnk"))
$Shortcut.TargetPath = $UiPath
$Shortcut.WorkingDirectory = (Join-Path $InstallDir "ui")
$Shortcut.IconLocation = "$IconPath,0"
$Shortcut.Description = "OpenVSPE - Serial/TCP Bridge"
$Shortcut.Save()

Write-Host ""
Write-Host "================================" -ForegroundColor Green
Write-Host " INSTALACAO CONCLUIDA! 🚀 " -ForegroundColor Green
Write-Host "================================" -ForegroundColor Green
Write-Host "O atalho foi criado na sua Area de Trabalho." -ForegroundColor White
Write-Host ""
Pause
