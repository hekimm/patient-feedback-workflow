# Local demo database only. Do not use in production.

param(
    [string]$ContainerName = 'patient-oracle',
    [int]$Port = 1521,
    [string]$OraclePassword = 'OraclePass_12345',
    [string]$AppPassword = '',
    [switch]$Reset
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($AppPassword)) {
    $AppPassword = $OraclePassword
}

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$dbDir = Join-Path $repoRoot 'HastaGeriBildirim\db'

function Assert-LastExitCode {
    param([string]$Message)

    if ($LASTEXITCODE -ne 0) {
        throw $Message
    }
}

function Test-TcpPortFree {
    param([int]$Port)

    $listener = $null
    try {
        $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $Port)
        $listener.Start()
        return $true
    }
    catch {
        return $false
    }
    finally {
        if ($listener) { $listener.Stop() }
    }
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw 'Docker bulunamadi. Docker Desktop kurun ve baslatin, veya README''deki "Docker olmadan kurulum" adimlarini izleyin.'
}

docker version --format '{{.Server.Version}}' *> $null
Assert-LastExitCode 'Docker daemon erisilebilir degil. Docker Desktop calistigindan emin olun.'

$existing = docker ps -a --filter "name=^$ContainerName$" --format '{{.Names}}'
Assert-LastExitCode 'Docker container listesi okunamadi.'

if ($Reset -and -not [string]::IsNullOrWhiteSpace($existing)) {
    Write-Host "Reset istendi; mevcut container siliniyor: $ContainerName"
    docker rm -f $ContainerName | Out-Null
    Assert-LastExitCode "Docker container silinemedi: $ContainerName"
    $existing = ''
}

if ([string]::IsNullOrWhiteSpace($existing)) {
    if (-not (Test-TcpPortFree $Port)) {
        throw "Port $Port kullanimda. Farkli port icin -Port kullanin veya mevcut servisi durdurun."
    }

    Write-Host "Oracle container'i olusturuluyor: $ContainerName (port $Port)..."
    docker run -d --name $ContainerName -p "${Port}:1521" -e "ORACLE_PASSWORD=$OraclePassword" gvenzl/oracle-free | Out-Null
    Assert-LastExitCode "Oracle container olusturulamadi: $ContainerName"
}
else {
    Write-Host "Mevcut container baslatiliyor: $ContainerName"
    docker start $ContainerName | Out-Null
    Assert-LastExitCode "Docker container baslatilamadi: $ContainerName"
}

Write-Host 'Oracle hazir olana kadar bekleniyor (ilk kurulumda birkac dakika surebilir)...'
$deadline = (Get-Date).AddMinutes(6)
$health = ''
while ((Get-Date) -lt $deadline) {
    $health = docker inspect --format '{{.State.Health.Status}}' $ContainerName
    if ($health -eq 'healthy') { break }
    Start-Sleep -Seconds 5
}
if ($health -ne 'healthy') {
    throw "Oracle container'i hazir duruma gelmedi (durum: $health). 'docker logs $ContainerName' ciktisini kontrol edin."
}

Write-Host 'Veritabani scriptleri container icine kopyalaniyor...'
docker exec $ContainerName sh -c 'rm -rf /tmp/hgb-db' | Out-Null
Assert-LastExitCode 'Container icindeki onceki /tmp/hgb-db klasoru temizlenemedi.'
docker cp $dbDir "${ContainerName}:/tmp/hgb-db"
Assert-LastExitCode 'Veritabani scriptleri container icine kopyalanamadi.'

function Invoke-SqlplusScript {
    param([string]$Connect, [string]$ScriptPath, [string]$ScriptArg)

    if ([string]::IsNullOrWhiteSpace($ScriptArg)) {
        $output = docker exec $ContainerName sqlplus -S -L $Connect "@$ScriptPath"
    }
    else {
        $output = docker exec $ContainerName sqlplus -S -L $Connect "@$ScriptPath" $ScriptArg
    }

    $output | ForEach-Object { Write-Host "  $_" }
    $joined = ($output -join "`n")

    if ($LASTEXITCODE -ne 0 -or $joined -match 'ORA-\d+|SP2-\d+') {
        throw "SQL script basarisiz oldu: $ScriptPath"
    }
    return $joined
}

Write-Host 'patient_app kullanicisi olusturuluyor/yetkilendiriliyor...'
Invoke-SqlplusScript "system/$OraclePassword@localhost/FREEPDB1" '/tmp/hgb-db/setup-oracle-permissions.sql' $AppPassword | Out-Null

Write-Host 'Sema, FK, hardening, demo seed ve smoke kontrolleri uygulaniyor (install-demo.sql)...'
$installOutput = Invoke-SqlplusScript "patient_app/$AppPassword@localhost/FREEPDB1" '/tmp/hgb-db/install-demo.sql'

$tableCountMatch = [regex]::Match($installOutput, 'TABLE_COUNT\s*-+\s*(\d+)')
$tableCount = 0
if ($tableCountMatch.Success) { $tableCount = [int]$tableCountMatch.Groups[1].Value }

if ($tableCount -lt 46) {
    throw "Dogrulama basarisiz: beklenen HGB tablolari bulunamadi (sayilan: $tableCount)."
}
if ($installOutput -notmatch 'admin\.demo') {
    throw 'Dogrulama basarisiz: demo kullanicilari olusmadi.'
}

Write-Host ''
Write-Host "Kurulum tamamlandi: $tableCount HGB tablosu, demo kullanicilari hazir." -ForegroundColor Green
Write-Host ''
Write-Host 'Uygulamayi baslatmak icin:'
Write-Host '  .\run-hgb-ui.ps1'
if ($ContainerName -ne 'patient-oracle' -or $Port -ne 1521 -or $AppPassword -ne $OraclePassword) {
    Write-Host ''
    Write-Host 'Varsayilan disi container/port/parola kullandiginiz icin once baglanti dizesini verin:'
    Write-Host "  `$env:ConnectionStrings__OracleDb = 'User Id=patient_app;Password=$AppPassword;Data Source=localhost:$Port/FREEPDB1;Connection Timeout=5;'"
}
Write-Host ''
Write-Host 'Demo girisleri: admin.demo/Admin123!  kalite.demo/Kalite123!  birim.demo/Birim123!'
