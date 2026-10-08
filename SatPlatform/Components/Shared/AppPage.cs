using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SatPlatform.Components.Layout;
using SatPlatform.Services;

namespace SatPlatform.Components.Shared;

/// <summary>Base class for pages: common injections and the top-bar title.</summary>
public abstract class AppPage : ComponentBase
{
    [Inject] protected PageTitleState TitleState { get; set; } = null!;
    [Inject] protected CurrentUser Me { get; set; } = null!;
    [Inject] protected ToastService Toasts { get; set; } = null!;
    [Inject] protected NavigationManager Nav { get; set; } = null!;
    [Inject] protected IJSRuntime JS { get; set; } = null!;

    protected bool Loading { get; set; } = true;
    protected string? Error { get; set; }

    protected void SetTitle(string title, bool narrow = false) => TitleState.Set(title, narrow);

    protected async Task RenderMathAsync() { try { await JS.InvokeVoidAsync("sat.renderMath"); } catch { } }

    protected async Task<bool> ConfirmAsync(string message) { try { return await JS.InvokeAsync<bool>("sat.confirm", message); } catch { return false; } }

    /// <summary>Runs an action, turning AppException into a toast.</summary>
    protected async Task<bool> TryAsync(Func<Task> action, string? success = null)
    {
        try { await action(); if (success is not null) Toasts.Ok(success); return true; }
        catch (AppException ex) { Toasts.Error(ex.Message); return false; }
        catch (Exception ex) { Toasts.Error("Xatolik: " + ex.Message); return false; }
    }
}
