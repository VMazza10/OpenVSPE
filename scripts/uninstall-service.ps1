# ===================================================================
# OpenVSPE — Script de Desinstalação do Serviço Windows
# Execute este script como Administrador.
# ===================================================================

param(
    [string]$ServiceName = "OpenVSPE",
    [string]$InstallDir = "$env:ProgramFiles\OpenVSPE"
)

# Verifica se está rodando como admin
$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "ERRO: Este script precisa ser executado como Administrador." -ForegroundColor Red
    exit 1
}

Write-Host "============================================" -ForegroundColor Cyan
Write-Host " OpenVSPE — Desinstalação do Serviço" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# 1. Para e remove o serviço
$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($service) {
    Write-Host "[1/3] Parando servico '$ServiceName'..." -ForegroundColor Yellow
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    
    Write-Host "[2/3] Removendo servico..." -ForegroundColor Yellow
    sc.exe delete $ServiceName | Out-Null
    Write-Host "  Servico removido." -ForegroundColor Green
} else {
    Write-Host "  Servico '$ServiceName' nao encontrado." -ForegroundColor Yellow
}

# 2. Remove arquivos (opcional)
Write-Host "[3/3] Remover arquivos de programa?" -ForegroundColor Yellow
$resposta = Read-Host "  Deseja remover os arquivos em '$InstallDir'? (S/N)"
if ($resposta -eq "S" -or $resposta -eq "s") {
    Remove-Item -Recurse -Force $InstallDir -ErrorAction SilentlyContinue
    Write-Host "  Arquivos removidos." -ForegroundColor Green
} else {
    Write-Host "  Arquivos mantidos." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Desinstalacao concluida." -ForegroundColor Green
Write-Host "Os dados de configuracao em '$env:ProgramData\OpenVSPE' foram mantidos." -ForegroundColor Cyan
