namespace SRNSMudApp.Components.UI;

using System;
using System.Security.Claims;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Routing;

/// <summary>
///     アプリアプリバー通知バッジコンポーネントのコードビハインド。
///     未読通知数の監視、ルート遷移時の既読更新、およびイベント購読解除を制御する。
/// </summary>
public partial class NotificationBadge : ComponentBase, IDisposable
{
    [CascadingParameter]
    private Task<AuthenticationState> AuthenticationStateTask { get; set; } = default!;

    [Inject]
    private NotificationBadgeViewModel ViewModel { get; set; } = null!;

    [Inject]
    private NavigationManager NavigationManager { get; set; } = null!;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            NavigationManager.LocationChanged -= OnLocationChanged;
            ViewModel.StateChanged -= OnStateChanged;
        }
    }

    protected override async Task OnInitializedAsync()
    {
        NavigationManager.LocationChanged += OnLocationChanged;
        ViewModel.StateChanged += OnStateChanged;

        var authState = await AuthenticationStateTask;
        var userId = authState.User.Identity?.IsAuthenticated == true
            ? authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            : null;

        await ViewModel.InitializeAsync(userId, NavigationManager.Uri);
    }

    private async void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        if (await ViewModel.OnLocationChangedAsync(e.Location))
        {
            await InvokeAsync(StateHasChanged);
        }
    }

    private async void OnStateChanged(object? sender, EventArgs e)
    {
        await InvokeAsync(StateHasChanged);
    }
}