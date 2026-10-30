# ============================================================
# Build Script OpenVSPE
# Uso: .\build.ps1              (usa versão do csproj)
#      .\build.ps1 -Versao 1.2.0 (força versão específica)
# ============================================================
param([string]$Versao = "")

$CurrentScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$base = (Get-Item "$CurrentScriptDir\..").FullName
$csproj = "$base\src\OpenVSPE.UI\OpenVSPE.UI.csproj"
$buildRoot = "$base\build"
$deploy = "$buildRoot\releases\DEPLOY"
$temp = "$buildRoot\temp"
$logFile = "$base\logs\build_$(Get-Date -Format 'yyyyMMdd_HHmm').log"
New-Item -ItemType Directory -Path "$base\logs" -Force | Out-Null

function Write-Log($msg, $color = "White") {
    $ts = Get-Date -Format "HH:mm:ss"
    Write-Host "[$ts] $msg" -ForegroundColor $color
    "[$ts] $msg" | Out-File -FilePath $logFile -Append -Encoding UTF8
}

# === Detecta versão atual do csproj ===
$xml = [xml](Get-Content $csproj -Encoding UTF8)
$verAtual = $xml.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1 -ExpandProperty Version

if ([string]::IsNullOrEmpty($Versao)) {
    # Auto-bump: incrementa o patch (ex: 1.1.8 -> 1.1.9)
    $partes = $verAtual -split '\.'
    $novoPatch = [int]$partes[2] + 1
    $Versao = "$($partes[0]).$($partes[1]).$novoPatch"
    Write-Log "Auto-bump: $verAtual -> $Versao" "Magenta"
}
else {
    Write-Log "Versao forcada: $Versao" "Magenta"
}

$nomeExe = "OpenVSPE_V$Versao"

# === Atualiza o csproj de forma segura (apenas via XmlDocument) ===
Write-Log "Atualizando versao no csproj para $Versao..." "Cyan"
$grupos = $xml.Project.PropertyGroup | Where-Object { $_.Version }
foreach ($g in $grupos) {
    $g.Version = $Versao
    $g.AssemblyVersion = "$Versao.0"
    $g.FileVersion = "$Versao.0"
    $g.AssemblyName = $nomeExe
}
$xml.Save($csproj)
Write-Log "csproj atualizado: AssemblyName=$nomeExe" "Green"

# === Limpeza ===
Write-Log "Iniciando Build OpenVSPE $Versao..." "Cyan"
Write-Log "Limpando processos antigos..." "Cyan"
Stop-Process -Name OpenVSPE* -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

Write-Log "Limpando pastas de build..." "Cyan"
if (Test-Path $deploy) { Remove-Item -Path $deploy -Recurse -Force }
if (Test-Path $temp) { Remove-Item -Path $temp   -Recurse -Force }

New-Item -ItemType Directory -Path "$deploy\service" -Force | Out-Null
New-Item -ItemType Directory -Path "$deploy\ui"      -Force | Out-Null
New-Item -ItemType Directory -Path $temp             -Force | Out-Null

$publishArgs = "-c Release -r win-x86 --self-contained true -p:PublishSingleFile=true"

# === 1. SERVICE ===
Write-Log "Publicando SERVICE..." "Yellow"
$out = dotnet publish "$base\src\OpenVSPE.Service\OpenVSPE.Service.csproj" $publishArgs.Split() -o "$temp\service" 2>&1
$out | Out-File -FilePath $logFile -Append -Encoding UTF8
if (Test-Path "$temp\service\OpenVSPE.Service.exe") {
    Copy-Item -Path "$temp\service\*" -Destination "$deploy\service" -Recurse -Force
    Write-Log "OK: Service publicado" "Green"
}
else {
    Write-Log "ERRO: Falha ao gerar OpenVSPE.Service.exe - veja o log em $logFile" "Red"
    $out | Write-Host
}

# === 2. UI ===
Write-Log "Publicando UI ($nomeExe.exe)..." "Yellow"
$out = dotnet publish "$base\src\OpenVSPE.UI\OpenVSPE.UI.csproj" $publishArgs.Split() -o "$temp\ui" 2>&1
$out | Out-File -FilePath $logFile -Append -Encoding UTF8

$uiExe = Get-ChildItem -Path "$temp\ui" -Filter "OpenVSPE*.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
if ($uiExe) {
    Copy-Item -Path "$temp\ui\*" -Destination "$deploy\ui" -Recurse -Force
    Write-Log "OK: UI publicada -> $($uiExe.Name)" "Green"
}
else {
    Write-Log "ERRO: Falha ao gerar exe da UI - veja o log em $logFile" "Red"
    $out | Select-Object -Last 20 | Write-Host
}

# === 3. SETUP ===
Write-Log "Publicando SETUP..." "Yellow"
$out = dotnet publish "$base\src\OpenVSPE.Setup\OpenVSPE.Setup.csproj" $publishArgs.Split() -o "$temp\setup" 2>&1
$out | Out-File -FilePath $logFile -Append -Encoding UTF8
if (Test-Path "$temp\setup\OpenVSPE.Setup.exe") {
    # Renomeia o setup com a versão
    Copy-Item -Path "$temp\setup\*" -Destination $deploy -Recurse -Force
    $setupDest = "$deploy\OpenVSPE.Setup.exe"
    $setupFinal = "$deploy\OpenVSPE_V$Versao.exe"
    if (Test-Path $setupDest) { Rename-Item $setupDest $setupFinal -Force }
    Write-Log "OK: Setup publicado -> OpenVSPE_V$Versao.exe" "Green"
}
else {
    Write-Log "ERRO: Falha ao gerar OpenVSPE.Setup.exe - veja o log em $logFile" "Red"
}

# === Resumo ===
Write-Host ""
Write-Host "--- CONTEUDO FINAL DO DEPLOY ---" -ForegroundColor Cyan
Get-ChildItem -Path $deploy -Recurse -File | Select-Object @{N = 'Pasta'; E = { Split-Path $_.DirectoryName -Leaf } }, Name, @{N = 'KB'; E = { [math]::Round($_.Length / 1KB, 1) } }
Write-Host ""
Write-Log "Build $Versao concluido! Instalador: $deploy\OpenVSPE_V$Versao.exe" "Green"
