$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
# Proyecto aislado con postgres tmpfs, nunca alinea-local.
$taskComposeArgs = @('compose', '-p', 'alinea-auth-verify', '-f', 'compose.yml', '-f', 'infra/docker/compose.auth-verify.yml')
& docker @taskComposeArgs up -d --no-build --wait
if ($LASTEXITCODE -ne 0) { throw 'No inició la plataforma de verificación.' }
foreach ($taskSyntheticPhone in @('+999000000001', '+999000000002', '+999000000003')) {
    & docker @taskComposeArgs run --rm -e "ALINEA_ADMIN_PHONE=$taskSyntheticPhone" api --allow-phone
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo autorizar una entrada sintética.' }
}
Push-Location frontend
try {
    & npx.cmd playwright test --config playwright.auth.config.ts
    if ($LASTEXITCODE -ne 0) { throw 'Falló la verificación E2E.' }
} finally { Pop-Location }
