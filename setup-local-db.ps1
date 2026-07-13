# Local demo database only. Do not use in production.

param(
    [string]$ContainerName = 'patient-oracle',
    [int]$Port = 1521,
    [string]$OraclePassword = 'OraclePass_12345',
    [string]$AppPassword = '',
    [string]$OracleImage = 'gvenzl/oracle-free:23.26.2-slim-faststart',
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
    throw 'Docker bulunamadi. Docker Desktop kurun ve baslatin, veya README''deki "Use an Existing Oracle Database" adimlarini izleyin.'
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
    docker run -d --name $ContainerName -p "${Port}:1521" -e "ORACLE_PASSWORD=$OraclePassword" $OracleImage | Out-Null
    Assert-LastExitCode "Oracle container olusturulamadi: $ContainerName"
}
else {
    $existingImage = docker inspect --format '{{.Config.Image}}' $ContainerName
    Assert-LastExitCode "Docker container image bilgisi okunamadi: $ContainerName"
    if ($existingImage -ne $OracleImage) {
        throw "Mevcut container image'i '$existingImage'; beklenen '$OracleImage'. -Reset kullanin veya -OracleImage '$existingImage' verin."
    }

    $publishedPorts = @(docker port $ContainerName 1521/tcp)
    Assert-LastExitCode "Docker container port bilgisi okunamadi: $ContainerName"
    $mappedPorts = @(
        $publishedPorts |
            ForEach-Object {
                if ($_ -match ':(\d+)$') { [int]$Matches[1] }
            } |
            Select-Object -Unique
    )
    if ($mappedPorts -notcontains $Port) {
        throw "Mevcut container Oracle portu $($mappedPorts -join ','); istenen port $Port. Dogru -Port degerini verin veya -Reset kullanin."
    }

    Write-Host "Mevcut container baslatiliyor: $ContainerName"
    docker start $ContainerName | Out-Null
    Assert-LastExitCode "Docker container baslatilamadi: $ContainerName"
}

Write-Host 'Oracle hazir olana kadar bekleniyor (ilk kurulumda birkac dakika surebilir)...'
$deadline = (Get-Date).AddMinutes(6)
$isReady = $false
while ((Get-Date) -lt $deadline) {
    docker exec $ContainerName healthcheck.sh *> $null
    if ($LASTEXITCODE -eq 0) {
        $isReady = $true
        break
    }
    Start-Sleep -Seconds 5
}
if (-not $isReady) {
    throw "Oracle container'i hazir duruma gelmedi. 'docker logs $ContainerName' ciktisini kontrol edin."
}

Write-Host 'Veritabani scriptleri container icine kopyalaniyor...'
docker exec -u 0 $ContainerName sh -c 'rm -rf /tmp/hgb-db' | Out-Null
Assert-LastExitCode 'Container icindeki onceki /tmp/hgb-db klasoru temizlenemedi.'
docker cp $dbDir "${ContainerName}:/tmp/hgb-db"
Assert-LastExitCode 'Veritabani scriptleri container icine kopyalanamadi.'

function Invoke-SqlplusScript {
    param(
        [string]$Connect,
        [string]$ScriptPath,
        [string[]]$SqlArguments = @()
    )

    if ($SqlArguments.Count -eq 0) {
        $output = docker exec -w /tmp/hgb-db $ContainerName sqlplus -S -L $Connect "@$ScriptPath"
    }
    else {
        $output = docker exec -w /tmp/hgb-db $ContainerName sqlplus -S -L $Connect "@$ScriptPath" @SqlArguments
    }

    $output | ForEach-Object { Write-Host "  $_" }
    $joined = ($output -join "`n")

    if ($LASTEXITCODE -ne 0 -or $joined -match 'ORA-\d+|SP2-\d+') {
        throw "SQL script basarisiz oldu: $ScriptPath"
    }
    return $joined
}

Write-Host 'patient_app kullanicisi olusturuluyor/yetkilendiriliyor...'
Invoke-SqlplusScript "system/$OraclePassword@localhost/FREEPDB1" '/tmp/hgb-db/admin/001-create-application-user.sql' @($AppPassword) | Out-Null
Invoke-SqlplusScript "system/$OraclePassword@localhost/FREEPDB1" '/tmp/hgb-db/admin/002-grant-application-privileges.sql' @('USERS', 'UNLIMITED') | Out-Null

Write-Host 'Sema, indeks, FK, demo verisi ve dogrulama modulleri uygulaniyor (install-demo.sql)...'
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
Write-Host "  `$env:ASPNETCORE_ENVIRONMENT = 'Development'"
Write-Host "  `$env:ConnectionStrings__OracleDb = 'User Id=patient_app;Password=$AppPassword;Data Source=localhost:$Port/FREEPDB1;Connection Timeout=5;'"
Write-Host '  dotnet run --project .\HastaGeriBildirim\HastaGeriBildirim.csproj -- --urls http://localhost:5080'
Write-Host ''
Write-Host 'Demo girisleri: admin.demo/Admin123!  kalite.demo/Kalite123!  birim.demo/Birim123!'
