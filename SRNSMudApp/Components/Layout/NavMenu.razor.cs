namespace SRNSMudApp.Components.Layout;

using System;
using System.Security.Claims;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Hosting;

/// <summary>
///     ナビゲーションメニューのコードビハインドコンポーネント。
///     通知数の購読・更新ロジックは <see cref="NavMenuViewModel"/> に委譲する。
/// </summary>
public sealed partial class NavMenu : ComponentBase, IDisposable
{
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;
    [Inject] private IWebHostEnvironment Env { get; set; } = null!;
    [Inject] private NavMenuViewModel ViewModel { get; set; } = null!;

    [CascadingParameter] private Task<AuthenticationState> AuthStateTask { get; set; } = default!;

    private string? _currentUserId;

    protected override async Task OnInitializedAsync()
    {
        AuthenticationState authState = await AuthStateTask;
        _currentUserId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        ViewModel.StateChanged += OnViewModelStateChanged;
        NavigationManager.LocationChanged += OnLocationChanged;

        string baseRelative = NavigationManager.ToBaseRelativePath(NavigationManager.Uri);
        var uri = new Uri(NavigationManager.Uri, UriKind.RelativeOrAbsolute);
        await ViewModel.InitializeAsync(_currentUserId, uri, baseRelative);
    }

    private async void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        string baseRelative = NavigationManager.ToBaseRelativePath(e.Location);
        await ViewModel.HandleLocationChangedAsync(e.Location, baseRelative);
    }

    private void OnViewModelStateChanged(object? sender, EventArgs e)
    {
        _ = InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        NavigationManager.LocationChanged -= OnLocationChanged;
        ViewModel.StateChanged -= OnViewModelStateChanged;
        ViewModel.Dispose();
        GC.SuppressFinalize(this);
    }
}