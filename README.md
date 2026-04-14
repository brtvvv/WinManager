# WinManager

Windows configuration manager — install/uninstall apps, tweak privacy, power, gaming settings, customize taskbar, start menu, and more.

## Instalacja

```powershell
iex (irm https://raw.githubusercontent.com/brtvvv/WinManager/main/install.ps1)
```

Skrypt pobiera najnowszy release z GitHub, instaluje do `%LOCALAPPDATA%\WinManager` i tworzy skrot na pulpicie.

## Budowanie ze zrodel

Wymagany .NET 9 SDK.

```bash
dotnet build
dotnet run
```

## Licencja

MIT
