# Script para Teste de Balança (Serial COM)
# Autor: Antigravity

$porta = "COM5" # Altere para a porta correta da sua balança
$baud = 2400   # Velocidade padrão (9600 ou 2400 são comuns)

Write-Host "--- Teste de Comunicação com Balança ($porta) ---" -ForegroundColor Cyan
Write-Host "IMPORTANTE: Certifique-se de que o OpenVSPE Service está PARADO antes de rodar." -ForegroundColor Yellow

try {
    $port = New-Object System.IO.Ports.SerialPort($porta, $baud, [System.IO.Ports.Parity]::None, 8, [System.IO.Ports.StopBits]::One)
    $port.Open()
    
    Write-Host "Lendo dados de $porta... (Pressione Ctrl+C para sair)" -ForegroundColor Green
    
    while ($true) {
        if ($port.BytesToRead -gt 0) {
            $data = $port.ReadExisting()
            # Limpa caracteres de controle para exibição limpa
            $cleanData = $data -replace "[^ -~]", ""
            if ($cleanData) {
                Write-Host "Peso Lido: $cleanData"
            }
        }
        Start-Sleep -Milliseconds 200
    }
}
catch {
    Write-Host "ERRO: Não foi possível abrir a porta $porta. Verifique se ela está sendo usada por outro programa." -ForegroundColor Red
    Write-Host $_.Exception.Message
}
finally {
    if ($null -ne $port -and $port.IsOpen) {
        $port.Close()
        Write-Host "Porta $porta fechada." -ForegroundColor Cyan
    }
}
