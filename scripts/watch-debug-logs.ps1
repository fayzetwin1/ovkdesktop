param(
    [Parameter(Mandatory = $true)]
    [string]$LogsDirectory
)

$Host.UI.RawUI.WindowTitle = "OpenVK Desktop - Debug Logs"
$startedAt = Get-Date
$timeoutAt = $startedAt.AddSeconds(45)

Write-Host "OpenVK Desktop live logs" -ForegroundColor Cyan
Write-Host "Waiting for the application to create a new log file..."
Write-Host "Logs directory: $LogsDirectory"
Write-Host ""

$logFile = $null
while ((Get-Date) -lt $timeoutAt) {
    if (Test-Path -LiteralPath $LogsDirectory) {
        $logFile = Get-ChildItem -LiteralPath $LogsDirectory -Filter "ovklog-*.log" -File -ErrorAction SilentlyContinue |
            Where-Object { $_.LastWriteTime -ge $startedAt.AddSeconds(-2) } |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1
    }

    if ($null -ne $logFile) {
        break
    }

    Start-Sleep -Milliseconds 250
}

if ($null -eq $logFile) {
    Write-Host "The application did not create a log file within 45 seconds." -ForegroundColor Red
    Write-Host "Check the build window or start the application again."
    Read-Host "Press Enter to close this window"
    exit 1
}

Write-Host "Streaming: $($logFile.FullName)" -ForegroundColor Green
Write-Host "Press Ctrl+C to stop watching logs." -ForegroundColor DarkGray
Write-Host ""

Get-Content -LiteralPath $logFile.FullName -Tail 100 -Wait
