namespace WinManager.Models;

public class PowerPlan
{
    public PowerPlan(string guid, string name, bool isActive)
    {
        Guid = guid;
        Name = name;
        IsActive = isActive;
    }

    public string Guid { get; }
    public string Name { get; }
    public bool IsActive { get; }
}
