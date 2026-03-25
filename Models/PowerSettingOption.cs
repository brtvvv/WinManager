namespace WinManager.Models;

public class PowerSettingOption
{
    public PowerSettingOption(string label, int value)
    {
        Label = label;
        Value = value;
    }

    public string Label { get; }
    public int Value { get; }
}
