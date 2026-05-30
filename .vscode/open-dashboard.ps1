param(
    [string]$Url = "http://127.0.0.1:3000",
    [int]$TimeoutSeconds = 60
)

$deadline = (Get-Date).AddSeconds($TimeoutSeconds)

while ((Get-Date) -lt $deadline) {
    try {
        $response = Invoke-WebRequest -UseBasicParsing -Uri $Url -TimeoutSec 2
        if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 500) {
            Start-Process $Url
            exit 0
        }
    }
    catch {
        Start-Sleep -Milliseconds 500
    }
}

Write-Error "Dashboard did not respond at $Url within $TimeoutSeconds seconds."
exit 1
