param([switch]$EnableLocalSms)
$ErrorActionPreference = 'Stop'
$taskEnvPath = Join-Path (Split-Path $PSScriptRoot -Parent) '.env'
if (-not (Test-Path -LiteralPath $taskEnvPath)) { throw 'Ejecuta primero infra/init-env.ps1.' }
$taskEnvContent = [IO.File]::ReadAllText($taskEnvPath)
foreach ($taskKeyName in @('OTP_KEY', 'DEV_SMS_INBOX_KEY')) {
    if ($taskEnvContent -notmatch "(?m)^$taskKeyName=") {
        $taskBytes = New-Object byte[] 32
        $taskRng = [Security.Cryptography.RandomNumberGenerator]::Create()
        try { $taskRng.GetBytes($taskBytes) } finally { $taskRng.Dispose() }
        $taskSecret = [BitConverter]::ToString($taskBytes).Replace('-', '')
        [IO.File]::AppendAllText($taskEnvPath, "`n$taskKeyName=$taskSecret`n")
    }
}
if ($EnableLocalSms -and $taskEnvContent -notmatch '(?m)^SMS_PROVIDER=') {
    [IO.File]::AppendAllText($taskEnvPath, "`nSMS_PROVIDER=DevelopmentMailbox`n")
}
Write-Output 'Configuración local completada; secretos omitidos. No se envían SMS reales.'
