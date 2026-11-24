# Installer.Net - Mimari Dokümantasyonu

## Genel Bakış

Installer.Net, PostgreSQL veritabanı ile çalışan, kurulum sihirbazı (Installer) ve ana uygulama (MainApp) olarak ayrılmış iki bileşenli bir .NET 10 uygulamasıdır.

## Bileşenler

### 1. Installer.Core (Port 5000/5001)
**Sorumluluk**: İlk kurulum, veritabanı bağlantı testi, migration çalıştırma, konfigürasyon kaydetme.

**Endpoint'ler**:
- `GET /health` - Sağlık kontrolü
- `GET /api/installer/status` - Kurulum durumu
- `POST /api/installer` - Kurulum işlemi
- `GET /` - Installer UI (kurulu değilse) veya MainApp'e redirect (kuruluysa)

**Akış**:
1. Kullanıcı tarayıcıda `/` açar
2. `IsInstalled=false` veya connection string boşsa → Installer UI gösterir
3. Kullanıcı connection string girer, "Install" der
4. POST `/api/installer` → Bağlantı testi → Migration → Config yazma
5. Başarılıysa `IsInstalled=true` kaydeder
6. Sonraki ziyaretlerde `/` → MainApp'e redirect

### 2. MainApp (Port 5002/5003)
**Sorumluluk**: Ana iş mantığı API'si, veritabanı işlemleri.

**Endpoint'ler**:
- `GET /health` - Sağlık kontrolü
- `GET /api/v1/info` - Uygulama bilgileri
- `GET /api/v1/data` - Örnek veri
- `GET /api/v1/db-test` - Veritabanı bağlantı testi

## Veri Akışı

```
[Browser] → GET / (Installer.Core)
    │
    ├─ Not installed → Show Installer UI
    │       │
    │       └─ POST /api/installer → Test Conn → Migrate → Write Config
    │
    └─ Installed → 307 Redirect → MainApp URL
```

## Konfigürasyon

### Ortam Değişkenleri (.env)
```bash
POSTGRES_HOST=localhost
POSTGRES_PORT=5432
POSTGRES_DB=installerdb
POSTGRES_USER=postgres
POSTGRES_PASSWORD=postgres
MainApp:Url=http://localhost:5002
```

### appsettings.json
```json
{
  "Database": {
    "ConnectionString": "",
    "Provider": "PostgreSQL"
  },
  "IsInstalled": false
}
```

## Docker Dağıtımı

```yaml
services:
  postgres:    # port 5432
  installer:   # port 5000/5001
  mainapp:     # port 5002/5003
```

## Test Stratejisi

- **Installer.Core.Tests**: 9 test (Health, Status, UI, Install, Config, DB)
- **MainApp.Tests**: 5 test (Health, Info, Data, DB, Config)
- **İzolasyon**: Geçici config dosyası ile test izolasyonu
- **Çalıştırma**: `dotnet test`

## Teknoloji Yığını

- .NET 10 (net10.0)
- ASP.NET Core Minimal APIs
- Scalar.AspNetCore (OpenAPI UI)
- Npgsql (PostgreSQL)
- xUnit / Microsoft.AspNetCore.Mvc.Testing