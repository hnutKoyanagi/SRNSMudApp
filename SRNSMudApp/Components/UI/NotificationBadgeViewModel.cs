namespace SRNSMudApp.Components.UI;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

using SRNSMudApp.Services;

/// <summary>
///     NotificationBadge コンポーネントの状態管理および未読通知数カウントロジックを担う ViewModel。
/// </summary>
[SuppressMessage("Design", "CA1054:UriParametersShouldNotBeStrings", Justification = "NavigationManager URI strings")]
public sealed class NotificationBadgeViewModel : IDisposable
{
    private readonly INotificationService _notificationService;
    private string _currentUri = string.Empty;

    public event EventHandler? StateChanged;

    public NotificationBadgeViewModel(INotificationService notificationService)
    {
        _notificationService = notificationService;
        _notificationService.NotificationsChanged += OnNotificationsChanged;
    }

    public string? CurrentUserId { get; private set; }
    public int UnreadCount { get; private set; }
    public string? LastBasePath { get; private set; }

    public bool IsVisible => CurrentUserId != null && UnreadCount > 0;

    /// <summary>
    ///     コンポーネント初期化時にユーザーIDと初期URLを設定し、未読件数を取得する。
    /// </summary>
    public async Task InitializeAsync(string? userId, string initialUri)
    {
        CurrentUserId = userId;
        _currentUri = initialUri;
        LastBasePath = ExtractBasePath(initialUri);
        await RefreshUnreadCountAsync(initialUri);
    }

    /// <summary>
    ///     ナビゲーション変更時にURLを比較し、ベースパスが変わっていれば未読件数を再取得する。
    /// </summary>
    public async Task<bool> OnLocationChangedAsync(string currentUri)
    {
        _currentUri = currentUri;
        var currentBasePath = ExtractBasePath(currentUri);
        if (currentBasePath == LastBasePath)
        {
            return false;
        }

        LastBasePath = currentBasePath;
        await RefreshUnreadCountAsync(currentUri);
        return true;
    }

    /// <summary>
    ///     明示的に未読件数を再取得する。
    /// </summary>
    public async Task RefreshAsync(string currentUri)
    {
        _currentUri = currentUri;
        await RefreshUnreadCountAsync(currentUri);
    }

    [SuppressMessage("Design", "CA1031:Do not catch generic exception types", Justification = "UI event handler background notification refresh")]
    private async void OnNotificationsChanged(object? sender, EventArgs e)
    {
        try
        {
            await RefreshAsync(_currentUri);
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            // イベントハンドラ内のバックグラウンド更新失敗でクラッシュさせない
        }
    }

    public void Dispose()
    {
        _notificationService.NotificationsChanged -= OnNotificationsChanged;
    }

    /// <summary>
    ///     URIからクエリ文字列を除いたベースパスを抽出する。
    /// </summary>
    public static string ExtractBasePath(string uri)
    {
        if (string.IsNullOrEmpty(uri))
        {
            return string.Empty;
        }

        var queryIndex = uri.IndexOf('?', StringComparison.Ordinal);
        return queryIndex >= 0 ? uri[..queryIndex] : uri;
    }

    /// <summary>
    ///     指定されたURIが通知ページかどうかを判定する。
    /// </summary>
    public static bool IsNotificationsPage(string uri)
    {
        if (string.IsNullOrEmpty(uri))
        {
            return false;
        }

        var basePath = ExtractBasePath(uri).TrimEnd('/');
        return basePath.EndsWith("/Notifications", StringComparison.OrdinalIgnoreCase) ||
               basePath.EndsWith("/notifications", StringComparison.OrdinalIgnoreCase) ||
               basePath.Equals("notifications", StringComparison.OrdinalIgnoreCase);
    }

    private async Task RefreshUnreadCountAsync(string currentUri)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
        {
            UnreadCount = 0;
            return;
        }

        if (IsNotificationsPage(currentUri))
        {
            UnreadCount = 0;
            return;
        }

        UnreadCount = await _notificationService.GetUnreadCountAsync(CurrentUserId);
    }
}