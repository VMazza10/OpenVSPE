param(
    [string]$HostAddress = "127.0.0.1",
    [int]$Port = 9100,
    [string]$Text = "Teste de Impressao OpenVSPE"
)

try {
    Write-Host "Conectando em $($HostAddress):$($Port)..."
    $client = New-Object System.Net.Sockets.TcpClient
    $client.Connect($HostAddress, $Port)
    $stream = $client.GetStream()
    $bytes = [System.Text.Encoding]::ASCII.GetBytes($Text + "`r`n")
    $stream.Write($bytes, 0, $bytes.Length)
    $stream.Flush()
    $stream.Close()
    $client.Close()
    Write-Host "Sucesso: O texto foi enviado!" -ForegroundColor Green
}
catch {
    Write-Host "Erro: $($_.Exception.Message)" -ForegroundColor Red
}
