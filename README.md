# WinManager

**Your Windows companion** — kompleksowy menedżer konfiguracji dla Windows 11. Instalacja i deinstalacja aplikacji, zarządzanie funkcjami systemu, prywatnością, zasilaniem, aktualizacjami, personalizacja paska zadań i menu Start, eksport/import konfiguracji.

Zbudowany jako praca inżynierska. Aplikacja desktopowa .NET 9 / WPF, MVVM, dark theme.

> **Wymagania:** Windows 11 (build 22000+, testowane głównie na 25H2 Pro) · uruchomienie jako Administrator zalecane dla pełnej funkcjonalności

---

## Możliwości

### Programy i funkcje
- Deinstalacja preinstalowanych aplikacji Windows (Camera, Maps, OneNote, Xbox, Phone Link, …) z filtrem wyszukiwania i wsadowym zaznaczaniem
- Instalacja i deinstalacja zewnętrznych aplikacji przez `winget` (Chrome, Firefox, VS Code, Steam, OBS, Notepad++, 7-Zip i in.) — z automatycznym tymczasowym obniżeniem UAC na czas instalacji wsadowej
- Włączanie/wyłączanie opcjonalnych funkcji Windows: Hyper-V, WSL, Windows Sandbox, NFS, Legacy Media, .NET Framework 3.5
- Instalator wszystkich aktualnych .NET Desktop Runtime (6, 7, 8, 9) jednym kliknięciem
- Skróty: reset sieci, skanowanie integralności systemu (SFC), konfiguracja autologowania

### Personalizacja
- **Menu Start** — wyczyść przypięte aplikacje, ukryj/pokaż sekcję Rekomendowane, ostatnio dodane i najczęściej używane aplikacje, wyłącz wyszukiwanie Bing
- **Pasek zadań** — tryb wyszukiwarki (ukryta / ikona / ikona + tekst / pole wyszukiwania), wyrównanie (lewo/środek), Task View, Copilot, Widgety, End Task; oczyszczenie przypiętych
- **Motyw** — tryb ciemny / jasny z automatycznym restartem Explorera

### Optymalizacja
- **Gaming i wydajność** — Game Mode, mouse acceleration, Background Apps, Storage Sense, enhanced search indexing
- **Powiadomienia** — globalny przełącznik, dźwięki, ekran blokady, system tray
- **Dźwięk** — startup sound systemu
- **Zasilanie** — wybór planu, hibernacja, timeouty ekranu / dysku / uśpienia, akcje przycisków zasilania i klapy, min/max stan procesora, oszczędzanie energii karty Wi-Fi
- **Prywatność i bezpieczeństwo** — poziom UAC, dostawca DNS (Domyślny / Google / Cloudflare / Quad9 + DNS over HTTPS), telemetria, ID reklamowy, Windows Spotlight, dostęp aplikacji do lokalizacji / kamery / mikrofonu / konta, blokada automatycznego backupu OneDrive, blokada OOBE-style monitów prywatności przy starcie
- **Aktualizacje** — presety (Normal / Security only / Disabled), Delivery Optimization, Microsoft Store auto-update, opóźnianie restartów, aktualizacje innych produktów Microsoft

### Konfiguracja
- Eksport i import pełnej konfiguracji do pliku JSON
- Wczytywanie konfiguracji z ekranu powitalnego z paskiem postępu i automatycznym aplikowaniem zmian

### Inne
- Szybki dostęp do paneli systemowych: Computer Management, Control Panel, Network Connections, Power Options, Printers, System Properties, Date & Time
- Automatyczne wykrywanie wersji i edycji Windows wyświetlane w nagłówku

---

## Instalacja

### Wersja stabilna (zalecane)

```powershell
iex (irm https://raw.githubusercontent.com/brtvvv/WinManager/main/install.ps1)
```

Skrypt pobiera najnowszy release z GitHub, instaluje do `%LOCALAPPDATA%\WinManager` i tworzy skrót na pulpicie.

### Wersja deweloperska (build z gałęzi `dev`)

```powershell
iex (irm https://raw.githubusercontent.com/brtvvv/WinManager/dev/install-dev.ps1)
```

Pobiera najnowszy automatyczny build z gałęzi `dev` (pre-release `dev-latest`). Skrót na pulpicie nazywa się **WinManager DEV**.

---

## Budowanie ze zrodel

Wymagany **.NET 9 SDK**.

```powershell
git clone https://github.com/brtvvv/WinManager.git
cd WinManager
dotnet build -c Release
dotnet run
```

Build self-contained single-file `.exe`:

```powershell
dotnet publish WinManager.csproj `
    -r win-x64 -c Release `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    --self-contained true `
    -o ./publish
```

---

## Stack technologiczny

- **.NET 9** / **WPF**
- Architektura **MVVM** (custom `ObservableObject`, `RelayCommand`, `AsyncRelayCommand`)
- Bezpośredni dostęp do rejestru przez `Microsoft.Win32.Registry` (in-process, nie przez PowerShell)
- Win32 P/Invoke (`SystemParametersInfo`, `SendMessageTimeout`, token privileges)
- Zarządzanie usługami przez `System.ServiceProcess.ServiceController`
- COM interop do `Microsoft.Update.ServiceManager`
- `powercfg`, `winget`, `DISM` dla operacji systemowych
- GitHub Actions CI/CD (workflow dla `main` releases i `dev` pre-releases)

---

## Licencja

MIT
