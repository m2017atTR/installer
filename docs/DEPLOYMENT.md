# Installer.Net - Dağıtım Rehberi

## Gereksinimler

- .NET 10 SDK
- Docker Desktop (PostgreSQL için)
- PowerShell 7+ (Windows) / Bash (Linux/macOS)

## Hızlı Başlangıç (Development)

### 1. PostgreSQL Başlatma

```powershell
# Windows
.\start-db.ps1

# veya Docker Compose
docker-compose up -d postgres
```

### 2. Uygulamaları Çalıştırma

```bash
# Terminal 1 - Installer (varsayılan startup)
dotnet run --project src/Installer.Core

# Terminal 2 - MainApp
dotnet run --project src/MainApp
```

### 3. Erişim

| Servis | URL | Açıklama |
|--------|-----|----------|
| Installer UI | http://localhost:5000 | Kurulum sihirbazı |
| Installer API Docs | http://localhost:5000/scalar | Scalar OpenAPI |
| MainApp API | http://localhost:5002 | Ana uygulama |
| MainApp API Docs | http://localhost:5002/scalar | Scalar OpenAPI |

## Docker Dağıtımı (Production)

### Image'ları Build Etme

```bash
docker-compose build
```

### Tüm Servisleri Başlatma

```bash
docker-compose up -d
```

### Logları İzleme

```bash
docker-compose logs -f installer
docker-compose logs -f mainapp
docker-compose logs -f postgres
```

### Durdurma

```bash
docker-compose down
```

### Verileri Temizleme (Dikkat: Veri kaybı!)

```bash
docker-compose down -v
```

## Environment Variables

| Değişken | Açıklama | Varsayılan |
|----------|----------|------------|
| `POSTGRES_HOST` | PostgreSQL host | `localhost` |
| `POSTGRES_PORT` | PostgreSQL port | `5432` |
| `POSTGRES_DB` | Veritabanı adı | `installerdb` |
| `POSTGRES_USER` | Kullanıcı | `postgres` |
| `POSTGRES_PASSWORD` | Şifre | `postgres` |
| `MainApp:Url` | MainApp URL'si | `http://localhost:5002` |
| `ASPNETCORE_ENVIRONMENT` | Ortam | `Development` |

## Production Checklist

- [ ] `.env` dosyasında güçlü şifreler kullanın
- [ ] `ASPNETCORE_ENVIRONMENT=Production` ayarlayın
- [ ] HTTPS sertifikaları yapılandırın
- [ ] PostgreSQL için güvenli şifre ve network isolation
- [ ] Docker secrets veya environment file kullanın
- [ ] Health check endpoint'lerini monitoring sistemine ekleyin
- [ ] Log aggregation (ELK, Loki, vb.) yapılandırın
- [ ] Backup stratejisi tanımlayın (pg_dump, volume backup)

## Troubleshooting

### PostgreSQL Bağlanmıyor
```bash
# Container durumu
docker ps -a

# Loglar
docker logs installer-net-postgres

# Health check
docker inspect installer-net-postgres --format '{{.State.Health.Status}}'
```

### Installer Kurulum Sonrası MainApp'e Gitmiyor
1. `appsettings.json` dosyasını kontrol edin: `IsInstalled: true`
2. Connection string dolu mu?
3. MainApp URL'si doğru mu? (`MainApp:Url`)

### Port Çakışması
```bash
# Kullanılan portlar
netstat -tulpn | grep -E '5000|5001|5002|5003|5432'

# docker-compose.yml portlarını değiştirin
```

## CI/CD Pipeline (GitHub Actions Örneği)

```yaml
name: CI/CD
on: [push, pull_request]
jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet restore
      - run: dotnet build --no-restore
      - run: dotnet test --no-build --verbosity normal
  docker:
    needs: build-and-test
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - run: docker-compose build
      - run: docker-compose up -d postgres
      - run: sleep 10 && docker-compose run --rm installer dotnet test
```

## Backup & Restore

### Backup (pg_dump)
```bash
docker exec installer-net-postgres pg_dump -U postgres installerdb > backup_$(date +%Y%m%d).sql
```

### Restore
```bash
cat backup_20240115.sql | docker exec -i installer-net-postgres psql -U postgres installerdb
```