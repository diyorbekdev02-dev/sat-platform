namespace SatPlatform.Components.Layout;

/// <summary>Top-bar title for the current circuit (scoped service).</summary>
public class PageTitleState
{
    public string Current { get; private set; } = "";
    public bool Narrow { get; private set; }
    public event Action? Changed;
    public void Set(string title, bool narrow = false) { Current = title; Narrow = narrow; Changed?.Invoke(); }
}
