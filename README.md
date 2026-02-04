# WinManager

Modern Windows app manager inspired by Winhance. The project lives in its own folder and ships three tabs: Programs & Features (implemented), Optimization (placeholder), and Customization (placeholder).

## Highlights
- Windows Apps and External Apps inside Programs & Features.
- Detects built-in apps via `Get-AppxPackage` and optionally `winget list`.
- Bulk selection (all / installed / not installed) and install/uninstall selected items.
- Activity log panel at the bottom.
- Dark UI ready for further styling.

## Run
1. Install .NET 9 SDK (`net9.0-windows` target).
2. Make sure `winget` and PowerShell are available.
3. From the project root run:
   ```powershell
   dotnet restore
   dotnet run --project .\WinManager\WinManager.csproj
   ```
4. For full install/uninstall capability of system apps, run elevated (as Administrator).

## Structure
- `WinManager.csproj` – .NET 9 WPF project.
- `App.xaml` / `MainWindow.xaml` – UI and theme.
- `ViewModels` – view logic (light MVVM).
- `Services` – app operations (winget/PowerShell).
- `Models` & `Common` – data models and helpers.

## Next steps
- Fill Optimization/Customization with real actions.
- Refine package IDs for edge cases and add richer progress feedback.
- Optionally add unit tests for process-running services.

