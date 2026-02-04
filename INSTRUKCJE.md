# Instrukcje i mapa plików (WinManager)

Krótka ściąga co za co odpowiada i gdzie szukać logiki:

- `App.xaml` / `App.xaml.cs` — definicja zasobów (motyw, style), start aplikacji, globalne przechwytywanie wyjątków i logowanie do `error.log`.
- `MainWindow.xaml` / `MainWindow.xaml.cs` — główne okno, układ: lewy sidebar nawigacyjny (Programs/Optimization/Customization), górny pasek tytułu/statusu, prawa część z właściwą zawartością. Zawiera też widok Programs & Features (kafelki Windows Apps / External Apps).
- `Common/ObservableObject.cs` — prosty bazowy INotifyPropertyChanged.
- `Common/RelayCommand.cs` — ICommand bez parametrów.
- `Common/RelayCommandGeneric.cs` — ICommand z parametrem typu T (używane do przełączania sekcji).
- `Common/AsyncRelayCommand.cs` — ICommand z obsługą async i blokadą podczas działania.
- `Models/AppStatus.cs` — status instalacji (Installed/NotInstalled/Unknown).
- `Models/AppSection.cs` — sekcje aplikacji (Programs/Optimization/Customization) do nawigacji.
- `Models/SoftwareItem.cs` — model aplikacji (ID, nazwa, opis, status, wybór, winget id, słowa kluczowe do detekcji).
- `Models/ProcessResult.cs` — prosty wynik procesu (sukces, output, exit code).
- `Services/ProcessRunner.cs` — uruchamianie procesów (winget, PowerShell), odczyt stdout/stderr.
- `Services/WindowsAppService.cs` — definicje aplikacji Windows, wykrywanie statusu (Get-AppxPackage / winget list), instalacja/odinstalowanie.
- `Services/ExternalAppService.cs` — definicje aplikacji zewnętrznych, instalacja/odinstalowanie przez winget.
- `ViewModels/MainViewModel.cs` — stan głównego okna (wybrana sekcja, VM Programs).
- `ViewModels/ProgramsViewModel.cs` — logika Programs & Features (ładowanie list, filtrowanie, wybór, instalacja/odinstalowanie, log tekstowy).
- `README.md` — opis projektu i uruchamiania (po angielsku).

Tipy:
- Sekcja widoczności widoków w `MainWindow.xaml` sterowana jest przez `SelectedSection` + `SectionToVisibilityConverter`.
- Jeśli aplikacja się nie buduje z powodu zablokowanego exe, zamknij działające `WinManager.exe` (np. `taskkill /IM WinManager.exe /F`).

