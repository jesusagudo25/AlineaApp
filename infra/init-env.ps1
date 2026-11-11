$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskEnvPath = Join-Path $taskRoot '.env'
if (Test-Path -LiteralPath $taskEnvPath) { throw '.env ya existe; no se sobrescribe.' }
$taskBytes = New-Object byte[] 32
$taskRng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
try { $taskRng.GetBytes($taskBytes) } finally { $taskRng.Dispose() }
$taskSecret = [BitConverter]::ToString($taskBytes).Replace('-', '').ToLowerInvariant()
$taskLines = @('POSTGRES_DB=alinea', 'POSTGRES_USER=alinea', "POSTGRES_PASSWORD=$taskSecret", 'WEB_PORT=8080')
[IO.File]::WriteAllLines($taskEnvPath, $taskLines, (New-Object Text.UTF8Encoding $false))
Write-Output '.env local creado con secreto aleatorio; no se muestra ni se versiona.'
