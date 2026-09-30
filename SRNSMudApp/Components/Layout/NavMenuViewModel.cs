namespace SRNSMudApp.Components.Layout;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

using SRNSMudApp.Services;

/// <summary>
///     NavMenu の通知バッジ数およびナビゲーション状態を管理する ViewModel。
///     通知変更イベントの購読と未読カウント計算を UI から分離する。
/// </summary>
public sealed class NavMenuViewModel : IDisposable
{
    private readonly INotificationService _notificationService;
    private string? _currentUserId;
    private string? _lastBasePath;

    public NavMenuViewModel(INotificationService notificationService)
    {
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _notificationService.NotificationsChanged += OnNotificationsChanged;
    }

    public int UnreadNotificationCount { get; private set; }

    [SuppressMessage("Design", "CA1056:UriPropertiesShouldNotBeStrings", Justification = "Relative URL string for Blazor NavigationManager")]
    public string? CurrentUrl { get; private set; }

    public event EventHandler? StateChanged;

    public Task InitializeAsync(string? currentUserId, Uri initialUri, string baseRelativePath)
    {
        ArgumentNullException.ThrowIfNull(initialUri);
        return InitializeInternalAsync(currentUserId, initialUri.IsAbsoluteUri ? initialUri.AbsolutePath : initialUri.ToString().Split('?')[0], baseRelativePath);
    }

    public Task InitializeAsync(string? currentUserId, string initialUri, string baseRelativePath)
    {
        ArgumentNullException.ThrowIfNull(initialUri);
        var uri = new Uri(initialUri, UriKind.RelativeOrAbsolute);
        return InitializeInternalAsync(currentUserId, uri.IsAbsoluteUri ? uri.AbsolutePath : initialUri.Split('?')[0], baseRelativePath);
    }

    private async Task InitializeInternalAsync(string? currentUserId, string lastBasePath, string baseRelativePath)
    {
        _currentUserId = currentUserId;
        CurrentUrl = baseRelativePath;
        _lastBasePath = lastBasePath;

        await UpdateUnreadCountAsync();
    }

    public async Task HandleLocationChangedAsync(string location, string baseRelativePath)
    {
        CurrentUrl = baseRelativePath;
        var uri = new Uri(location, UriKind.RelativeOrAbsolute);
        string absolutePath = uri.IsAbsoluteUri ? uri.AbsolutePath : location.Split('?')[0];

        if (string.Equals(_lastBasePath, absolutePath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _lastBasePath = absolutePath;
        await UpdateUnreadCountAsync();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private async void OnNotificationsChanged(object? sender, EventArgs e)
    {
        await UpdateUnreadCountAsync();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool IsNotificationsPage()
    {
        if (CurrentUrl == null)
        {
            return false;
        }
        return CurrentUrl.Trim('/').Equals("notifications", StringComparison.OrdinalIgnoreCase);
    }

    public async Task UpdateUnreadCountAsync()
    {
        if (!string.IsNullOrEmpty(_currentUserId))
        {
            if (IsNotificationsPage())
            {
                UnreadNotificationCount = 0;
            }
            else
            {
                UnreadNotificationCount = await _notificationService.GetUnreadCountAsync(_currentUserId);
            }
        }
        else
        {
            UnreadNotificationCount = 0;
        }
    }

    public void Dispose()
    {
        _notificationService.NotificationsChanged -= OnNotificationsChanged;
        GC.SuppressFinalize(this);
    }
}