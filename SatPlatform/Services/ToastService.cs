namespace SatPlatform.Services;

/// <summary>Per-circuit toast messages.</summary>
public class ToastService
{
    public record Toast(Guid Id, string Text, string Kind);
    public List<Toast> Items { get; } = [];
    public event Action? Changed;

    public void Show(string text, string kind = "")
    {
        var t = new Toast(Guid.NewGuid(), text, kind);
        Items.Add(t);
        Changed?.Invoke();
        _ = Task.Delay(4000).ContinueWith(_ => { Items.Remove(t); Changed?.Invoke(); });
    }
    public void Ok(string text) => Show(text, "ok");
    public void Error(string text) => Show(text, "danger");
}
