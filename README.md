# WinManager

**Your Windows companion** — a comprehensive configuration manager for Windows 11. Install and uninstall apps, manage system features, privacy, power, updates, customize the taskbar and Start menu, export and import full configurations.

Built as an engineering thesis project. Desktop application on .NET 9 / WPF, MVVM, dark theme.

> **Requirements:** Windows 11 (build 22000+, primarily tested on 25H2 Pro) · running as Administrator recommended for full functionality

[Polska wersja niżej ↓](#winmanager-pl)

---

## Features

### Programs & Features
- Uninstall preinstalled Windows apps (Camera, Maps, OneNote, Xbox, Phone Link, …) with a search filter and batch selection
- Install and uninstall external applications via `winget` (Chrome, Firefox, VS Code, Steam, OBS, Notepad++, 7-Zip and more) — UAC is automatically lowered for the duration of the install batch and restored afterwards
- Enable / disable Windows optional features: Hyper-V, WSL, Windows Sandbox, NFS, Legacy Media, .NET Framework 3.5
- One-click installer for all current .NET Desktop Runtimes (6, 7, 8, 9)
- Shortcuts: network reset, system integrity scan (SFC), autologin setup

### Customization
- **Start Menu** — clean pinned apps, hide/show Recommended section, recently added and most used apps, disable Bing search
- **Taskbar** — search box mode (Hidden / Icon / Icon + label / Search box), alignment (left / center), Task View, Copilot, Widgets, End Task; clean pinned items
- **Theme** — dark / light mode with automatic Explorer restart

### Optimization
- **Gaming & performance** — Game Mode, mouse acceleration, Background Apps, Storage Sense, enhanced search indexing
- **Notifications** — global toggle, sounds, lock screen, system tray
- **Sound** — system startup sound
- **Power** — power plan selection, hibernation, display / disk / sleep timeouts, power button & lid actions, min/max processor state, wireless adapter power saving
- **Privacy & security** — UAC level, DNS provider (Default / Google / Cloudflare / Quad9 + DNS over HTTPS), telemetry, advertising ID, Windows Spotlight, app access to location / camera / microphone / account, OneDrive automatic backup blocker, OOBE-style privacy prompt suppressor on next login
- **Updates** — presets (Normal / Security only / Disabled), Delivery Optimization, Microsoft Store auto-update, restart deferral, updates for other Microsoft products

### Configuration
- Export and import the full configuration to a JSON file
- Loading a config from the welcome screen shows a progress dialog and applies all changes automatically

### Misc
- Quick access to system panels: Computer Management, Control Panel, Network Connections, Power Options, Printers, System Properties, Date & Time
- Automatic Windows version and edition detection shown in the header

---

## Installation

### Stable version (recommended)

```powershell
iex (irm https://raw.githubusercontent.com/brtvvv/WinManager/main/install.ps1)
```

The script downloads the latest release from GitHub, installs to `%LOCALAPPDATA%\WinManager` and creates a desktop shortcut.

### Development version (build from the `dev` branch)

```powershell
iex (irm https://raw.githubusercontent.com/brtvvv/WinManager/dev/install-dev.ps1)
```

Fetches the latest automatic build from the `dev` branch (pre-release `dev-latest`). The desktop shortcut is named **WinManager DEV**.

---

## Building from source

Requires **.NET 9 SDK**.

```powershell
git clone https://github.com/brtvvv/WinManager.git
cd WinManager
dotnet build -c Release
dotnet run
```

Build a self-contained single-file `.exe`:

```powershell
dotnet publish WinManager.csproj `
    -r win-x64 -c Release `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    --self-contained true `
    -o ./publish
```

---

## Tech stack

- **.NET 9** / **WPF**
- **MVVM** architecture (custom `ObservableObject`, `RelayCommand`, `AsyncRelayCommand`)
- Direct registry access via `Microsoft.Win32.Registry` (in-process, not through PowerShell)
- Win32 P/Invoke (`SystemParametersInfo`, `SendMessageTimeout`, token privileges)
- Service management via `System.ServiceProcess.ServiceController`
- COM interop with `Microsoft.Update.ServiceManager`
- `powercfg`, `winget`, `DISM` for system operations
- GitHub Actions CI/CD (workflows for `main` releases and `dev` pre-releases)

---

## License

MIT

---
---

<a id="winmanager-pl"></a>

# WinManager (PL)

**Your Windows companion** — kompleksowy menedżer konfiguracji dla Windows 11. Instalacja i deinstalacja aplikacji, zarządzanie funkcjami systemu, prywatnością, zasilaniem, aktualizacjami, personalizacja paska zadań i menu Start, eksport / import pełnej konfiguracji.

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

### Wersja stabilna (zalecana)

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
