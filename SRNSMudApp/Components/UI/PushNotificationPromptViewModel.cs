using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

using Microsoft.JSInterop;

using MudBlazor;

namespace SRNSMudApp.Components.UI;

/// <summary>
///     プッシュ通知購読プロンプトのステータス管理および非同期操作を担当する ViewModel。
///     UI（Blazor コンポーネント）からブラウザ API 呼び出しや状態管理を分離し、単体テストを可能にする。
/// </summary>
public class PushNotificationPromptViewModel
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ISnackbar _snackbar;

    public PushNotificationPromptViewModel(IJSRuntime jsRuntime, ISnackbar snackbar)
    {
        _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
        _snackbar = snackbar ?? throw new ArgumentNullException(nameof(snackbar));
    }

    /// <summary>ブラウザがプッシュ通知をサポートしているかどうか。</summary>
    public bool IsSupported { get; set; } = true;

    /// <summary>ブラウザの通知許可状態 ("default", "granted", "denied")。</summary>
    public string Permission { get; set; } = "default";

    /// <summary>現在プッシュ通知を購読済みかどうか。</summary>
    public bool IsSubscribed { get; set; }

    /// <summary>現在非同期処理（購読・解除・テスト送信）が実行中かどうか。</summary>
    public bool IsProcessing { get; set; }

    /// <summary>対象ユーザー ID。</summary>
    public string? UserId { get; set; }

    /// <summary>購読済みの場合にもステータスやテスト通知ボタンを表示するかどうか。</summary>
    public bool ShowWhenSubscribed { get; set; } = true;

    /// <summary>
    ///     認証ユーザー情報およびブラウザの購読ステータスを読み込みます。
    /// </summary>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "JS interop failure should not crash prompt UI")]
    public async Task LoadSubscriptionStatusAsync(ClaimsPrincipal? user)
    {
        try
        {
            if (user?.Identity?.IsAuthenticated == true)
            {
                UserId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            }

            var status = await _jsRuntime.InvokeAsync<SubscriptionStatusResult>("PushNotificationInterop.getSubscriptionStatus");
            if (status != null)
            {
                IsSupported = status.Supported;
                Permission = status.Permission ?? "default";
                IsSubscribed = status.IsSubscribed;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PushNotificationPromptViewModel] Status check error: {ex.Message}");
        }
    }

    /// <summary>
    ///     ブラウザの通知許可ダイアログを表示し、Web Push を購読します。
    /// </summary>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "JS interop failure should be reported via snackbar")]
    public async Task RequestPushPermissionAsync()
    {
        IsProcessing = true;

        try
        {
            var subscribeResult = await _jsRuntime.InvokeAsync<SubscribeResult>(
                "PushNotificationInterop.enablePushNotifications",
                UserId);

            if (subscribeResult != null && subscribeResult.Success)
            {
                IsSubscribed = true;
                Permission = "granted";
                _snackbar.Add("プッシュ通知を有効化しました！", Severity.Success);
            }
            else
            {
                var error = subscribeResult?.Error ?? "通知の許可が得られませんでした。";
                _snackbar.Add(error, Severity.Warning);
                await LoadSubscriptionStatusAsync(null);
            }
        }
        catch (Exception ex)
        {
            _snackbar.Add($"エラーが発生しました: {ex.Message}", Severity.Error);
        }
        finally
        {
            IsProcessing = false;
        }
    }

    /// <summary>
    ///     Web Push の購読を解除します。
    /// </summary>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "JS interop failure should be reported via snackbar")]
    public async Task UnsubscribeAsync()
    {
        IsProcessing = true;

        try
        {
            var result = await _jsRuntime.InvokeAsync<SimpleResult>("PushNotificationInterop.unsubscribe");
            if (result != null && result.Success)
            {
                IsSubscribed = false;
                _snackbar.Add("プッシュ通知の購読を解除しました。", Severity.Info);
            }
            else
            {
                _snackbar.Add(result?.Error ?? "購読解除に失敗しました。", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            _snackbar.Add($"解除中にエラーが発生しました: {ex.Message}", Severity.Error);
        }
        finally
        {
            IsProcessing = false;
        }
    }

    /// <summary>
    ///     テストプッシュ通知の送信を要求します。
    /// </summary>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "JS interop failure should be reported via snackbar")]
    public async Task SendTestNotificationAsync()
    {
        IsProcessing = true;

        try
        {
            var result = await _jsRuntime.InvokeAsync<SimpleResult>("PushNotificationInterop.sendTestNotification");
            if (result != null && result.Success)
            {
                _snackbar.Add(result.Message ?? "テスト通知を送信しました。間もなく届きます。", Severity.Success);
            }
            else
            {
                _snackbar.Add(result?.Message ?? "テスト通知の送信に失敗しました。", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            _snackbar.Add($"送信エラー: {ex.Message}", Severity.Error);
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "DTO tightly coupled to PushNotificationPrompt")]
    public sealed record SubscriptionStatusResult(bool Supported, string Permission, bool IsSubscribed);

    [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "DTO tightly coupled to PushNotificationPrompt")]
    public sealed record SubscribeResult(bool Success, string? Error, string? Endpoint);

    [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "DTO tightly coupled to PushNotificationPrompt")]
    public sealed record SimpleResult(bool Success, string? Message, string? Error);
}