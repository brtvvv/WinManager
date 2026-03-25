using WinManager.Common;

namespace WinManager.Models;

public class PowerSettingItem : ObservableObject
{
    private PowerSettingOption? _selectedAcOption;
    private PowerSettingOption? _selectedDcOption;
    private int _acValue;
    private int _dcValue;
    private string _acInput = "0";
    private string _dcInput = "0";
    private bool _suppress;
    private bool _isVisible = true;

    public PowerSettingItem(string name, string description,
        string subGuid, string settingGuid,
        IReadOnlyList<PowerSettingOption>? options = null,
        bool isSlider = false, int minValue = 0, int maxValue = 100)
    {
        Name = name;
        Description = description;
        SubGuid = subGuid;
        SettingGuid = settingGuid;
        Options = options ?? Array.Empty<PowerSettingOption>();
        IsSlider = isSlider;
        MinValue = minValue;
        MaxValue = maxValue;

        ApplyAcCommand = new RelayCommand(() => ApplyInput(true));
        ApplyDcCommand = new RelayCommand(() => ApplyInput(false));
    }

    public string Name { get; }
    public string Description { get; }
    public string SubGuid { get; }
    public string SettingGuid { get; }
    public bool IsSlider { get; }
    public int MinValue { get; }
    public int MaxValue { get; }
    public IReadOnlyList<PowerSettingOption> Options { get; }

    public Action<PowerSettingItem, bool>? ValueChanged { get; set; }

    public RelayCommand ApplyAcCommand { get; }
    public RelayCommand ApplyDcCommand { get; }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    public string AcInput
    {
        get => _acInput;
        set => SetProperty(ref _acInput, value);
    }

    public string DcInput
    {
        get => _dcInput;
        set => SetProperty(ref _dcInput, value);
    }

    public PowerSettingOption? SelectedAcOption
    {
        get => _selectedAcOption;
        set
        {
            if (!SetProperty(ref _selectedAcOption, value)) return;
            if (!_suppress && value != null) ValueChanged?.Invoke(this, true);
        }
    }

    public PowerSettingOption? SelectedDcOption
    {
        get => _selectedDcOption;
        set
        {
            if (!SetProperty(ref _selectedDcOption, value)) return;
            if (!_suppress && value != null) ValueChanged?.Invoke(this, false);
        }
    }

    public int AcValue
    {
        get => _acValue;
        set
        {
            if (!SetProperty(ref _acValue, value)) return;
            if (!_suppress) ValueChanged?.Invoke(this, true);
        }
    }

    public int DcValue
    {
        get => _dcValue;
        set
        {
            if (!SetProperty(ref _dcValue, value)) return;
            if (!_suppress) ValueChanged?.Invoke(this, false);
        }
    }

    public void LoadValue(int value, bool isAc)
    {
        _suppress = true;
        try
        {
            if (IsSlider)
            {
                var clamped = Math.Clamp(value, MinValue, MaxValue);
                if (isAc) { AcValue = clamped; AcInput = clamped.ToString(); }
                else { DcValue = clamped; DcInput = clamped.ToString(); }
            }
            else
            {
                var match = Options.FirstOrDefault(o => o.Value == value) ?? Options.FirstOrDefault();
                if (isAc) SelectedAcOption = match;
                else SelectedDcOption = match;
            }
        }
        finally { _suppress = false; }
    }

    public int GetValue(bool isAc)
    {
        if (IsSlider) return isAc ? AcValue : DcValue;
        var opt = isAc ? SelectedAcOption : SelectedDcOption;
        return opt?.Value ?? 0;
    }

    private void ApplyInput(bool isAc)
    {
        var input = isAc ? AcInput : DcInput;
        if (!int.TryParse(input, out var val)) return;
        val = Math.Clamp(val, MinValue, MaxValue);
        if (isAc) { AcInput = val.ToString(); AcValue = val; }
        else { DcInput = val.ToString(); DcValue = val; }
    }
}
