# REPARO / REMOÇÃO DE DISPOSITIVO POR PORTA COM
# Execute como ADMINISTRADOR

Write-Host "--- BUSCAR DISPOSITIVO POR PORTA COM ---" -ForegroundColor Cyan

$inputPort = Read-Host "Digite a porta (ex: 6 ou COM6)"

# Normaliza a entrada
if ($inputPort -match '^\d+$') {
    $port = "COM$inputPort"
}
elseif ($inputPort -match '^COM\d+$') {
    $port = $inputPort.ToUpper()
}
else {
    Write-Host "Formato invalido! Use por exemplo: 6 ou COM6" -ForegroundColor Red
    pause
    exit
}

Write-Host "Procurando dispositivo na porta $port..." -ForegroundColor Yellow

$dev = Get-CimInstance Win32_PnPEntity |
Where-Object { $_.Caption -match "\($port\)" } |
Select-Object -First 1

if ($dev) {
    $id = $dev.DeviceID

    Write-Host "`nDispositivo encontrado: $($dev.Caption)" -ForegroundColor Green
    Write-Host "ID do Hardware: $id"

    Write-Host "`nRemovendo dispositivo..." -ForegroundColor Yellow
    $pnputilPath = if (Test-Path "$env:windir\sysnative\pnputil.exe") { "$env:windir\sysnative\pnputil.exe" } else { "$env:windir\System32\pnputil.exe" }
    & $pnputilPath /remove-device "$id" /force

    Write-Host "`nAguardando 3 segundos..."
    Start-Sleep -Seconds 3

    Write-Host "Reescaneando hardware..." -ForegroundColor Cyan
    & $pnputilPath /scan-devices

    Write-Host "`nAguardando 5 segundos..."
    Start-Sleep -Seconds 5

    Write-Host "--- PROCESSO FINALIZADO ---" -ForegroundColor Green
}
else {
    Write-Host "`nERRO: Nenhum dispositivo encontrado na porta $port!" -ForegroundColor Red
    Write-Host "`nPortas disponíveis no momento:" -ForegroundColor Cyan

    Get-CimInstance Win32_PnPEntity |
    Where-Object { $_.Caption -match 'COM\d+' } |
    Select-Object -ExpandProperty Caption
}

Write-Host "`nPressione qualquer tecla para sair..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
