$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$appDir = Join-Path $repoRoot 'HastaGeriBildirim'
$logDir = Join-Path $repoRoot 'logs'
$outLog = Join-Path $logDir 'hgb-ui.out.log'
$errLog = Join-Path $logDir 'hgb-ui.err.log'
$exePath = Join-Path $appDir 'bin\Debug\net8.0\HastaGeriBildirim.exe'
$runtimeSettings = Join-Path $appDir 'bin\Debug\net8.0\appsettings.json'

New-Item -ItemType Directory -Force -Path $logDir | Out-Null

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET 8 SDK bulunamadi. README.md icindeki Prerequisites bolumunu izleyin.'
}

if (-not (Test-Path $exePath)) {
    Write-Host 'Derlenmis uygulama bulunamadi; dotnet build calistiriliyor...'
    dotnet build (Join-Path $repoRoot 'HastaGeriBildirim.sln')
    if ($LASTEXITCODE -ne 0) {
        throw 'dotnet build basarisiz oldu. .NET 8 SDK kurulu oldugundan emin olun.'
    }
}

$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'http://localhost:5080'

if (-not [string]::IsNullOrWhiteSpace($env:ConnectionStrings__OracleDb)) {
    Write-Host 'Using ConnectionStrings__OracleDb from current environment.'
}
elseif (Test-Path $runtimeSettings) {
    $settings = Get-Content -Raw -Path $runtimeSettings | ConvertFrom-Json
    $runtimeConnectionString = $settings.ConnectionStrings.OracleDb
    if (-not [string]::IsNullOrWhiteSpace($runtimeConnectionString) -and
        $runtimeConnectionString -notmatch 'CHANGE_ME') {
        $env:ConnectionStrings__OracleDb = $runtimeConnectionString
        Write-Host 'Using Oracle connection string from runtime appsettings.json.'
    }
}

if ([string]::IsNullOrWhiteSpace($env:ConnectionStrings__OracleDb)) {
    $docker = Get-Command docker -ErrorAction SilentlyContinue
    if ($docker) {
        $oraclePassword = docker inspect patient-oracle --format '{{range .Config.Env}}{{println .}}{{end}}' 2>$null |
            Where-Object { $_ -like 'ORACLE_PASSWORD=*' } |
            Select-Object -First 1

        if ($oraclePassword) {
            $oraclePassword = $oraclePassword.Substring('ORACLE_PASSWORD='.Length)
            $env:ConnectionStrings__OracleDb = "User Id=patient_app;Password=$oraclePassword;Data Source=127.0.0.1:1521/FREEPDB1;Connection Timeout=5;"
            Write-Host 'Using Oracle connection string from local patient-oracle Docker container.'
        }
    }
}

if ([string]::IsNullOrWhiteSpace($env:ConnectionStrings__OracleDb)) {
    Write-Warning 'ConnectionStrings__OracleDb is not set. Login will fail until Oracle credentials are provided.'
}

Set-Location $appDir
& $exePath --urls 'http://localhost:5080' 1>> $outLog 2>> $errLog
