using Microsoft.AspNetCore.Components;
using StudyConnect.Core.Abstractions;
using StudyConnect.Core.Common;
using StudyConnect.Core.Models;
using StudyConnect.Web.Services;

namespace StudyConnect.Web.Components;

/// <summary>
/// Base class for the application's interactive pages (inheritance).
/// It centralises cross-cutting concerns so the pages stay small and cohesive:
/// resolving the signed-in user, running service calls with consistent error handling and
/// toast messages, and managing real-time subscriptions that must be disposed with the page.
/// </summary>
public abstract class AppComponentBase : ComponentBase, IDisposable
{
    private readonly List<IDisposable> _subscriptions = new();
    private bool _disposed;

    [Inject]
    protected ToastService Toasts { get; set; } = default!;

    [Inject]
    protected CurrentUserService CurrentUser { get; set; } = default!;

    [Inject]
    protected NavigationManager Navigation { get; set; } = default!;

    [Inject]
    protected IRealtimeNotifier Realtime { get; set; } = default!;

    [Inject]
    protected ILoggerFactory LoggerFactory { get; set; } = default!;

    /// <summary>Id of the signed-in student (valid after <see cref="EnsureUserAsync"/> returned true).</summary>
    protected int UserId { get; private set; }

    /// <summary>True while a <see cref="RunAsync(Func{Task{OperationResult}}, string?)"/> call is in progress (used to disable buttons).</summary>
    protected bool IsBusy { get; private set; }

    /// <summary>Resolves the current user; redirects to the login page when the session has expired.</summary>
    protected async Task<bool> EnsureUserAsync()
    {
        int? userId = await CurrentUser.TryGetUserIdAsync();
        if (userId is null)
        {
            Navigation.NavigateTo("account/login", forceLoad: true);
            return false;
        }

        UserId = userId.Value;
        return true;
    }

    /// <summary>
    /// Runs a service operation and shows a success or error toast.
    /// Returns true when the operation succeeded.
    /// </summary>
    protected async Task<bool> RunAsync(Func<Task<OperationResult>> operation, string? successMessage = null)
    {
        IsBusy = true;
        try
        {
            OperationResult result = await operation();
            if (result.Succeeded)
            {
                if (!string.IsNullOrEmpty(successMessage))
                {
                    Toasts.Success(successMessage);
                }

                return true;
            }

            Toasts.Error(result.Error ?? "Something went wrong.");
            return false;
        }
        catch (Exception ex)
        {
            LogError(ex);
            Toasts.Error("Something went wrong. Please try again.");
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Generic overload for operations that also return a value (method overloading).</summary>
    protected async Task<T?> RunAsync<T>(Func<Task<Result<T>>> operation, string? successMessage = null)
        where T : class
    {
        IsBusy = true;
        try
        {
            Result<T> result = await operation();
            if (result.Succeeded)
            {
                if (!string.IsNullOrEmpty(successMessage))
                {
                    Toasts.Success(successMessage);
                }

                return result.Value;
            }

            Toasts.Error(result.Error ?? "Something went wrong.");
            return null;
        }
        catch (Exception ex)
        {
            LogError(ex);
            Toasts.Error("Something went wrong. Please try again.");
            return null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Runs a data-loading action and turns unexpected exceptions into a friendly toast.</summary>
    protected async Task LoadSafelyAsync(Func<Task> load, string errorMessage = "Could not load data. Please refresh the page.")
    {
        try
        {
            await load();
        }
        catch (Exception ex)
        {
            LogError(ex);
            Toasts.Error(errorMessage);
        }
    }

    /// <summary>
    /// Subscribes to real-time events for the signed-in user. The handler runs on the component's
    /// dispatcher (via <see cref="ComponentBase.InvokeAsync(Func{Task})"/>) and the subscription is
    /// disposed automatically with the page.
    /// </summary>
    protected void SubscribeToRealtime(Func<RealtimeEvent, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        IDisposable subscription = Realtime.Subscribe(UserId, realtimeEvent => InvokeAsync(async () =>
        {
            await handler(realtimeEvent);
            StateHasChanged();
        }));

        _subscriptions.Add(subscription);
    }

    /// <summary>Hook for derived pages that own additional resources.</summary>
    protected virtual void OnDispose()
    {
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (IDisposable subscription in _subscriptions)
        {
            subscription.Dispose();
        }

        _subscriptions.Clear();
        OnDispose();
        GC.SuppressFinalize(this);
    }

    private void LogError(Exception exception) =>
        LoggerFactory.CreateLogger(GetType()).LogError(exception, "Unhandled error in {Component}", GetType().Name);
}
