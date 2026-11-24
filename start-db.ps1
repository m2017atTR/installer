<#
.SYNOPSIS
    Installer.Net icin PostgreSQL 17+ container'inu baslatir.

.DESCRIPTION
    .env dosyasini yukler, Docker Desktop calisiyor mu kontrol eder, calisiyorsa docker-compose ile
    PostgreSQL 17 container'inu baslatir/yeniden baslatir ve health check bekler.

.EXAMPLE
    .\start-db.ps1
#>

param()

$ErrorActionPreference = "Stop"

Write-Host "=== Installer.Net PostgreSQL Baslatici ===" -ForegroundColor Cyan

# .env dosyasini yukle
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$envFile = Join-Path $scriptDir ".env"

if (Test-Path $envFile) {
    Write-Host ".env dosyasi yukleniyor..." -ForegroundColor Yellow
    $envContent = Get-Content $envFile -Raw
    $envLines = $envContent -split "`r?`n"
    foreach ($line in $envLines) {
        $line = $line.Trim()
        if ($line -and -not $line.StartsWith("#")) {
            $parts = $line -split "=", 2
            if ($parts.Count -eq 2) {
                $name = $parts[0].Trim()
                $value = $parts[1].Trim()
                [Environment]::SetEnvironmentVariable($name, $value, "Process")
            }
        }
    }
    Write-Host "Ortam degiskenleri yuklendi." -ForegroundColor Green
}

# Docker Desktop calisiyor mu kontrol et
$dockerRunning = $false
try {
    $dockerVersion = docker version --format '{{.Server.Version}}' 2>$null
    if ($dockerVersion) {
        $dockerRunning = $true
        Write-Host "Docker Desktop calisiyor (v$dockerVersion)" -ForegroundColor Green
    }
}
catch {
    $dockerRunning = $false
}

if (-not $dockerRunning) {
    Write-Host "Docker Desktop calismiyor!" -ForegroundColor Red
    Write-Host "  Lutfen Docker Desktop'i baslatin ve tekrar deneyin." -ForegroundColor Yellow
    Write-Host "  Indirme: https://www.docker.com/products/docker-desktop/" -ForegroundColor Gray
    exit 1
}

$composeFile = Join-Path $scriptDir "docker-compose.yml"

if (-not (Test-Path $composeFile)) {
    Write-Host "docker-compose.yml bulunamadi: $composeFile" -ForegroundColor Red
    exit 1
}

Write-Host "`nPostgreSQL 17 container'i baslatiliyor..." -ForegroundColor Cyan

# Container zaten var mi kontrol et
$existingContainer = docker ps -a --filter "name=installer-net-postgres" --format "{{.Names}}" 2>$null
if ($existingContainer) {
    Write-Host "  Mevcut container bulunuyor, yeniden baslatiliyor..." -ForegroundColor Yellow
    docker-compose -f $composeFile down --remove-orphans
}

# Container'i baslat
docker-compose -f $composeFile up -d

if ($LASTEXITCODE -ne 0) {
    Write-Host "Container baslatilamadi!" -ForegroundColor Red
    exit 1
}

Write-Host "  Container baslatildi, health check bekleniyor..." -ForegroundColor Yellow

# Health check bekle
$maxWait = 60
$interval = 3
$elapsed = 0

while ($elapsed -lt $maxWait) {
    $health = docker inspect installer-net-postgres --format '{{.State.Health.Status}}' 2>$null
    
    if ($health -eq "healthy") {
        Write-Host "`nPostgreSQL hazir ve saglikli!" -ForegroundColor Green
        break
    }
    elseif ($health -eq "unhealthy") {
        Write-Host "`nPostgreSQL sagliksiz duruma dusu!" -ForegroundColor Red
        docker logs installer-net-postgres --tail 20
        exit 1
    }
    
    Write-Host -NoNewline "."
    Start-Sleep -Seconds $interval
    $elapsed += $interval
}

if ($elapsed -ge $maxWait) {
    Write-Host "`nHealth check timeout ($maxWait sn), ancak container calisiyor olabilir." -ForegroundColor Yellow
}

# Baglanti bilgilerini goster
$pgHost = $env:POSTGRES_HOST ?? "localhost"
$pgPort = $env:POSTGRES_PORT ?? "5432"
$pgDb = $env:POSTGRES_DB ?? "installerdb"
$pgUser = $env:POSTGRES_USER ?? "postgres"
$pgPass = $env:POSTGRES_PASSWORD ?? "postgres"

Write-Host "`nBaglanti Bilgileri:" -ForegroundColor Cyan
Write-Host "  Host:     $pgHost" -ForegroundColor White
Write-Host "  Port:     $pgPort" -ForegroundColor White
Write-Host "  Database: $pgDb" -ForegroundColor White
Write-Host "  User:     $pgUser" -ForegroundColor White
Write-Host "  Password: $pgPass" -ForegroundColor White
Write-Host ""
Write-Host "  Connection String:" -ForegroundColor Cyan
Write-Host "  Host=$pgHost;Port=$pgPort;Database=$pgDb;Username=$pgUser;Password=$pgPass;" -ForegroundColor Green

Write-Host "`nPostgreSQL kullanima hazir!" -ForegroundColor Green