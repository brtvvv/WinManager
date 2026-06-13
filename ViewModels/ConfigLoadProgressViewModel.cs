using WinManager.Common;

namespace WinManager.ViewModels;

public class ConfigLoadProgressViewModel : ObservableObject
{
    private string _currentStep = "Preparing...";
    private double _progress;
    private string _counter = "0 / 0";

    public string CurrentStep
    {
        get => _currentStep;
        set => SetProperty(ref _currentStep, value);
    }

    public double Progress
    {
        get => _progress;
        set => SetProperty(ref _progress, value);
    }

    public string Counter
    {
        get => _counter;
        set => SetProperty(ref _counter, value);
    }

    /// <summary>
    /// Runs each step sequentially, updating CurrentStep / Counter / Progress
    /// between them so the modal window stays responsive and informative.
    /// Exceptions in individual steps are swallowed so a single failure
    /// doesn't abort the whole apply pass.
    /// </summary>
    public async Task RunAsync(IReadOnlyList<(string label, Func<Task> action)> steps)
    {
        var total = steps.Count;
        if (total == 0)
        {
            CurrentStep = "Nothing to apply.";
            Progress = 100;
            Counter = "0 / 0";
            await Task.Delay(400);
            return;
        }

        for (int i = 0; i < total; i++)
        {
            var (label, action) = steps[i];
            CurrentStep = $"Applying: {label}...";
            Counter = $"{i + 1} / {total}";
            Progress = (double)i / total * 100;

            try { await action(); }
            catch { /* one step failing should not abort the rest */ }
        }

        CurrentStep = "Done.";
        Counter = $"{total} / {total}";
        Progress = 100;
        await Task.Delay(400);
    }
}
