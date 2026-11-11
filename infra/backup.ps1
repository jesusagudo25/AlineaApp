param([string]$Destination)
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
if (-not $Destination) { $Destination = Join-Path 'backups' ('alinea-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.dump') }
if (Test-Path -LiteralPath $Destination) { throw 'El destino ya existe; no se sobrescribe.' }
$taskBackupDir = Split-Path ([IO.Path]::GetFullPath($Destination)) -Parent
New-Item -ItemType Directory -Path $taskBackupDir -Force | Out-Null
docker compose exec -T postgres sh -c 'pg_dump -U $POSTGRES_USER -d $POSTGRES_DB --format=custom --file=/tmp/alinea-backup.dump'
if ($LASTEXITCODE -ne 0) { throw 'pg_dump falló.' }
docker compose cp postgres:/tmp/alinea-backup.dump $Destination
if ($LASTEXITCODE -ne 0) { throw 'La copia del dump falló.' }
Get-FileHash -Algorithm SHA256 -LiteralPath $Destination
