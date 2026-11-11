param([switch]$ExerciseOutage)
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)

function Assert-Endpoint([string]$Path, [int]$ExpectedStatus) {
    try {
        $taskResponse = Invoke-WebRequest -UseBasicParsing -Uri "http://127.0.0.1:8080$Path" -TimeoutSec 15
        $taskStatus = [int]$taskResponse.StatusCode
        $taskBody = $taskResponse.Content
    } catch {
        if (-not $_.Exception.Response) { throw }
        $taskStatus = [int]$_.Exception.Response.StatusCode
        $taskReader = New-Object IO.StreamReader($_.Exception.Response.GetResponseStream())
        try { $taskBody = $taskReader.ReadToEnd() } finally { $taskReader.Dispose() }
    }
    if ($taskStatus -ne $ExpectedStatus) { throw "$Path expected $ExpectedStatus; received $taskStatus" }
    Write-Output "$Path -> $taskStatus $taskBody"
}

Assert-Endpoint '/health' 200
Assert-Endpoint '/health/ready' 200
if ($ExerciseOutage) {
    try {
        docker compose stop postgres
        if ($LASTEXITCODE -ne 0) { throw 'Cannot stop local PostgreSQL' }
        Assert-Endpoint '/health' 200
        Assert-Endpoint '/health/ready' 503
    } finally {
        docker compose up -d --wait
        if ($LASTEXITCODE -ne 0) { throw 'Local stack recovery failed' }
    }
    Assert-Endpoint '/health/ready' 200
}
