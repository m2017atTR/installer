# Installer.Net

.NET 10 tabanlı, PostgreSQL varsayılan veritabanı ile kurulum sihirbazı uygulaması.

## Özellikler

- **Web Tabanlı Kurulum Arayüzü**: Bootstrap 5 ile responsive HTML UI
- **REST API**: Minimal APIs ile kurulum endpoint'leri
- **OpenAPI/Swagger**: Scalar.AspNetCore ile interaktif API dokümantasyonu
- **PostgreSQL 17+ Desteği**: Varsayılan veritabanı sağlayıcısı
- **Docker Ready**: PostgreSQL container otomatik başlatma
- **Test-Driven Development**: xUnit test projesi ile runtime hatalarını build/test zamanında yakalama

## Teknoloji Yığını

- .NET 10 (net10.0)
- ASP.NET Core Minimal APIs
- Scalar.AspNetCore 2.x (OpenAPI UI)
- Microsoft.AspNetCore.OpenApi (Built-in OpenAPI generator)
- PostgreSQL 17+ (Npgsql 9.0.3)
- System.Data.SqlClient (SQL Server fallback)
- xUnit / Microsoft.AspNetCore.Mvc.Testing (Testler)
- Testcontainers.PostgreSql (Integration testler)

## Proje Yapısı

```
Installer.Net/
├── .github/                    # GitHub workflows (CI/CD)
├── .vscode/                    # VS Code ayarları
├── src/                        # Kaynak kodlar
│   ├── Installer.Core/         # Kurulum sihirbazı (UI + REST API)
│   │   ├── wwwroot/
│   │   │   └── index.html      # Kurulum sihirbazı UI
│   │   ├── Configuration/
│   │   │   └── AppSettings.cs  # Yapılandırma modelleri
│   │   ├── Services/
│   │   │   ├── DatabaseInstaller.cs   # Kurulum mantığı
│   │   │   ├── JsonConfigReader.cs    # JSON config okuyucu/yazıcı
│   │   │   └── InstallerOrchestrator.cs # Kurulum orkestrasyonu
│   │   ├── Program.cs          # Entry point + API endpoints
│   │   ├── Dockerfile          # Docker image tanımı
│   │   └── Installer.Core.csproj
│   ├── MainApp/                # Ana uygulama API
│   │   ├── Program.cs          # Entry point + API endpoints
│   │   ├── Dockerfile          # Docker image tanımı
│   │   ├── appsettings.json    # Uygulama ayarları
│   │   └── MainApp.csproj
│   └── tests/                  # Test projeleri
│       └── MainApp.Tests/      # xUnit test projesi
│           ├── MainAppTests.cs     # API ve konfigürasyon testleri
│           ├── test.env            # Test ortamı değişkenleri
│           └── MainApp.Tests.csproj
├── docker-compose.yml          # PostgreSQL + Installer + MainApp
├── start-db.ps1                # PostgreSQL başlatma scripti
├── .env                        # Ortak ortam değişkenleri
├── Directory.Packages.props    # Merkezi paket versiyon yönetimi
├── Directory.Build.props       # Ortak build ayarları
├── Installer.slnx              # Solution dosyası
└── README.md
```

## Hızlı Başlangıç

### Ön Koşullar
- .NET 10 SDK
- Docker Desktop (PostgreSQL için)

### 1. PostgreSQL Başlatma

```powershell
# Windows PowerShell
.\start-db.ps1
```

Veya Docker Compose ile:

```bash
docker-compose up -d
```

### 2. Testleri Çalıştırma

```bash
dotnet test src/tests/MainApp.Tests/MainApp.Tests.csproj
```

### 3. Uygulamaları Çalıştırma

```bash
# Installer (port 5000/5001)
dotnet run --project src/Installer.Core/Installer.Core.csproj

# MainApp (port 5002/5003)
dotnet run --project src/MainApp/MainApp.csproj
```

### 4. Erişim URL'leri

| Servis | HTTP | HTTPS | Açıklama |
|--------|------|-------|----------|
| **Installer UI** | http://localhost:5000 | https://localhost:5001 | Kurulum sihirbazı |
| **Installer API Docs** | http://localhost:5000/scalar | https://localhost:5001/scalar | Scalar OpenAPI UI |
| **MainApp API** | http://localhost:5002 | https://localhost:5003 | Ana uygulama API |
| **MainApp API Docs** | http://localhost:5002/scalar | https://localhost:5003/scalar | Scalar OpenAPI UI |

## API Endpoint'leri

### Installer API

| Method | Endpoint | Açıklama |
|--------|----------|----------|
| GET | `/health` | Sağlık kontrolü |
| GET | `/api/installer/status` | Kurulum durumu kontrolü |
| POST | `/api/installer` | Veritabanı kurulumu |

### MainApp API

| Method | Endpoint | Açıklama |
|--------|----------|----------|
| GET | `/health` | Sağlık kontrolü |
| GET | `/api/v1/info` | Uygulama bilgileri |
| GET | `/api/v1/data` | Örnek veri |
| GET | `/api/v1/db-test` | Veritabanı bağlantı testi |

## Veritabanı Yapılandırması

### .env (Ortak)
```bash
POSTGRES_HOST=localhost
POSTGRES_PORT=5432
POSTGRES_DB=installerdb
POSTGRES_USER=postgres
POSTGRES_PASSWORD=postgres
```

### Docker PostgreSQL (varsayılan)
- **Host**: localhost
- **Port**: 5432
- **Database**: installerdb
- **Username**: postgres
- **Password**: postgres

Bağlantı dizesi: `Host=localhost;Port=5432;Database=installerdb;Username=postgres;Password=postgres;`

## Docker Detayları

### docker-compose.yml
3 servis başlatır:
1. **postgres** - PostgreSQL 17 (port 5432, volume: postgres_data)
2. **installer** - Installer.Core API (port 5000/5001)
3. **mainapp** - MainApp API (port 5002/5003)

### start-db.ps1
- .env dosyasını yükler
- Docker Desktop çalışıyor mu kontrol eder
- Container zaten varsa yeniden başlatır
- Health check bekler (pg_isready)
- Bağlantı bilgilerini gösterir

## Geliştirme

### Build
```bash
dotnet build src/Installer.Core/Installer.Core.csproj
dotnet build src/MainApp/MainApp.csproj
```

### Test
```bash
dotnet test src/tests/MainApp.Tests/MainApp.Tests.csproj
```

### Docker Build
```bash
docker-compose build
docker-compose up -d
```

## CI/CD

GitHub Actions workflow (`.github/workflows/ci.yml`) şunları yapar:
1. Kod checkout
2. .NET 10 setup
3. Bağımlılıkları restore
4. Build
5. Testler çalıştır
6. Docker image build (main branch için)

## Mimari Kararlar

1. **src/ klasörü**: Tüm kaynak kodları `src/` altında toplanır, kök sadece yapılandırma dosyalarını içerir
2. **tests/ klasörü**: Test projeleri `src/tests/` altında organize edilir, solution'da ayrı section olarak görünür
3. **Test-Driven Debugging**: `dotnet run` yerine `dotnet test` kullanılır, runtime hataları testlerde yakalanır
4. **Merkezi Paket Yönetimi**: `Directory.Packages.props` ile tüm paket versiyonları tek yerden yönetilir
5. **Environment-based Config**: `.env` dosyası ile ortam değişkenleri yönetilir, kodda hardcoded değer yok
6. **Installer Pattern**: Uygulama ilk çalışmada kurulum sihirbazını gösterir, kuruluysa ana uygulamaya yönlendirir

## Lisans

MIT