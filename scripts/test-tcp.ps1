param(
    [string]$HostAddress = "127.0.0.1",
    [int]$Port = 8899,
    [string]$Message = "PESO?"
)

try {
    Write-Host "Conectando em $($HostAddress):$($Port)..."
    $client = New-Object System.Net.Sockets.TcpClient
    $client.Connect($HostAddress, $Port)
    $stream = $client.GetStream()
    $bytes = [System.Text.Encoding]::ASCII.GetBytes($Message)
    $stream.Write($bytes, 0, $bytes.Length)
    $stream.Flush()
    Write-Host "Enviado: $Message"
    Start-Sleep -Milliseconds 500
    if ($stream.DataAvailable) {
        $buffer = New-Object byte[] 1024
        $read = $stream.Read($buffer, 0, $buffer.Length)
        $resp = [System.Text.Encoding]::ASCII.GetString($buffer, 0, $read)
        Write-Host "Resposta: $resp" -ForegroundColor Green
    }
    $stream.Close()
    $client.Close()
}
catch {
    Write-Host "Erro: $($_.Exception.Message)" -ForegroundColor Red
}
