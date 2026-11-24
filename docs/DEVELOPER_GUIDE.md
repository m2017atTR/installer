# Installer.Net - Geliştirici Rehberi

## Proje Yapısı

```
Installer.Net/
├── src/
│   ├── Installer.Core/          # Kurulum sihirbazı (UI + REST API)
│   │   ├── wwwroot/index.html   # Installer UI
│   │   ├── Configuration/       # AppSettings, MainAppOptions
│   │   ├── Services/            # DatabaseInstaller, JsonConfigReader, InstallerOrchestrator
│   │   ├── Program.cs           # Entry point + API endpoints
│   │   ├── Dockerfile
│   │   └── Installer.Core.csproj
│   └── MainApp/                 # Ana uygulama API
│       ├── Program.cs           # Entry point + API endpoints
│       ├── Dockerfile
│       ├── appsettings.json
│       └── MainApp.csproj
├── tests/
│   ├── Installer.Core.Tests/    # 9 test (Health, Status, UI, Install, Config, DB)
│   └── MainApp.Tests/           # 5 test (Health, Info, Data, DB, Config)
├── docs/
│   ├── README.md                # Genel bakış ve hızlı başlangıç
│   ├── ARCHITECTURE.md          # Mimari dokümantasyon
│   ├── DEPLOYMENT.md            # Dağıtım rehberi
│   └── LICENSE                  # Lisans
├── docker-compose.yml           # PostgreSQL + Installer + MainApp
├── start-db.ps1                 # PostgreSQL başlatma scripti
├── .env                         # Ortak ortam değişkenleri
├── Directory.Packages.props     # Merkezi paket versiyonları
├── Directory.Build.props        # Ortak build ayarları
├── Installer.slnx               # Solution dosyası
└── global.json                  # .NET SDK versiyonu
```

## Geliştirme Workflow

### 1. Yeni Özellik Ekleme

```bash
# Branch oluştur
git checkout -b feature/yeni-ozellik

# Kod yaz, test ekle
# src/Installer.Core/... veya src/MainApp/...

# Testleri çalıştır
dotnet test

# Build kontrolü
dotnet build

# Commit ve push
git add .
git commit -m "feat: yeni özellik açıklaması"
git push origin feature/yeni-ozellik
```

### 2. Test Yazma Kuralları

- Her public endpoint için test yaz
- `Installer.Core.Tests` → Installer mantığı testleri
- `MainApp.Tests` → MainApp endpoint testleri
- İzolasyon için geçici config dosyası kullan
- `dotnet test --filter "TestName"` ile tek test çalıştır

### 3. Debug İpuçları

```bash
# Installer logları
dotnet run --project src/Installer.Core --verbosity detailed

# MainApp logları
dotnet run --project src/MainApp --verbosity detailed

# Test debug (VS Code launch.json)
# "request": "launch", "program": "dotnet", "args": ["test", "--filter", "TestName"]
```

## Kod Standartları

### Naming Conventions
- **Classes**: PascalCase (`DatabaseInstaller`)
- **Methods**: PascalCase (`TestConnectionAsync`)
- **Properties**: PascalCase (`ConnectionString`)
- **Parameters**: camelCase (`connectionString`)
- **Interfaces**: `I` prefix (`IInstaller`, `IConfigReader`)

### Async/Await
- Tüm I/O operasyonları async
- `CancellationToken` parametresi zorunlu
- `ConfigureAwait(false)` library kodunda

### Error Handling
- `Results.BadRequest()`, `Results.Problem()` kullan
- Exception'ları logla, kullanıcıya friendly mesaj ver
- `try-catch` bloklarını minimum tut

### Configuration
- Hardcoded değer YOK, her şey `.env` / `appsettings.json` / env vars'dan
- `IOptions<T>` pattern kullan
- `IConfiguration` injection yap

## Yaygın Sorunlar ve Çözümleri

| Sorun | Çözüm |
|-------|-------|
| `dotnet build` kilitleniyor | `taskkill /F /IM dotnet.exe` |
| Testler "Program not found" hatası veriyor | `public partial class Program { }` ekle |
| PostgreSQL bağlantı hatası | `start-db.ps1` çalıştır, container sağlıklı mı kontrol et |
| Port çakışması | `docker-compose.yml` portlarını değiştir |

## Faydalı Komutlar

```bash
# Tüm testler
dotnet test

# Belirli test
dotnet test --filter "HealthEndpoint"

# Build
dotnet build

# Docker
docker-compose up -d
docker-compose down -v

# DB başlat
.\start-db.ps1

# Çalıştır
dotnet run --project src/Installer.Core
dotnet run --project src/MainApp
```

## VS Code Launch.json

```json
{
  "configurations": [
    {
      "name": "Installer.Core",
      "type": "coreclr",
      "request": "launch",
      "program": "${workspaceFolder}/src/Installer.Core/bin/Debug/net10.0/Installer.Core.dll",
      "cwd": "${workspaceFolder}/src/Installer.Core"
    },
    {
      "name": "MainApp",
      "type": "coreclr",
      "request": "launch",
      "program": "${workspaceFolder}/src/MainApp/bin/Debug/net10.0/MainApp.dll",
      "cwd": "${workspaceFolder}/src/MainApp"
    },
    {
      "name": "Run Tests",
      "type": "coreclr",
      "request": "launch",
      "program": "dotnet",
      "args": ["test", "--filter", "HealthEndpoint"],
      "cwd": "${workspaceFolder}"
    }
  ]
}
```