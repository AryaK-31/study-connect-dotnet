using Microsoft.AspNetCore.Components.Authorization;

namespace StudyConnect.Web.Services;

/// <summary>Gives interactive components easy access to the signed-in user's id.</summary>
public sealed class CurrentUserService
{
    private readonly AuthenticationStateProvider _authenticationStateProvider;

    public CurrentUserService(AuthenticationStateProvider authenticationStateProvider)
    {
        _authenticationStateProvider = authenticationStateProvider;
    }

    public async Task<int?> TryGetUserIdAsync()
    {
        AuthenticationState state = await _authenticationStateProvider.GetAuthenticationStateAsync();
        return state.User.GetUserId();
    }

    public async Task<int> GetUserIdAsync() =>
        await TryGetUserIdAsync() ?? throw new InvalidOperationException("No user is signed in.");
}

public enum ToastLevel
{
    Info,
    Success,
    Warning,
    Error
}

public sealed record ToastMessage(Guid Id, string Text, ToastLevel Level);

/// <summary>
/// Small notification-toast service (replaces the React ErrorContext / global error toast).
/// Components raise toasts; the ToastHost component listens to <see cref="Changed"/> and re-renders.
/// </summary>
public sealed class ToastService
{
    private static readonly TimeSpan DisplayTime = TimeSpan.FromSeconds(4.5);
    private readonly List<ToastMessage> _toasts = new();
    private readonly object _gate = new();

    /// <summary>Raised whenever a toast is added or removed (C# event based on a delegate).</summary>
    public event Action? Changed;

    public IReadOnlyList<ToastMessage> Snapshot()
    {
        lock (_gate)
        {
            return _toasts.ToList();
        }
    }

    public void Show(string text, ToastLevel level = ToastLevel.Info)
    {
        var toast = new ToastMessage(Guid.NewGuid(), text, level);
        lock (_gate)
        {
            _toasts.Add(toast);
            if (_toasts.Count > 4)
            {
                _toasts.RemoveAt(0);
            }
        }

        Changed?.Invoke();
        _ = RemoveLaterAsync(toast.Id);
    }

    public void Success(string text) => Show(text, ToastLevel.Success);

    public void Error(string text) => Show(text, ToastLevel.Error);

    public void Info(string text) => Show(text, ToastLevel.Info);

    public void Dismiss(Guid id)
    {
        bool removed;
        lock (_gate)
        {
            removed = _toasts.RemoveAll(t => t.Id == id) > 0;
        }

        if (removed)
        {
            Changed?.Invoke();
        }
    }

    private async Task RemoveLaterAsync(Guid id)
    {
        await Task.Delay(DisplayTime);
        Dismiss(id);
    }
}
