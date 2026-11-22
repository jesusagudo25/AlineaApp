param([switch]$ResetTestContainers)
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
$taskFinanceCompose = @('compose', '-p', 'alinea-finance-verify', '-f', 'compose.yml', '-f', 'infra/docker/compose.finance-verify.yml')
if ($ResetTestContainers) {
    # Solo contenedores de prueba tmpfs. Nunca elimina volúmenes ni usa alinea-local.
    & docker @taskFinanceCompose down
    if ($LASTEXITCODE -ne 0) { throw 'Falló la recreación de contenedores de pruebas.' }
}
& docker @taskFinanceCompose up -d --no-build --wait
if ($LASTEXITCODE -ne 0) { throw 'No inició la plataforma aislada.' }
foreach ($taskSyntheticPhone in @('+999000000201', '+999000000202')) {
    & docker @taskFinanceCompose run --rm -e "ALINEA_ADMIN_PHONE=$taskSyntheticPhone" api --allow-phone
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo autorizar una entrada sintética.' }
}
Push-Location frontend
try {
    & npx.cmd playwright test --config playwright.finance.config.ts
    if ($LASTEXITCODE -ne 0) { throw 'Falló la verificación financiera.' }
} finally { Pop-Location }
