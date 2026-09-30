using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

using Microsoft.JSInterop;

namespace SRNSMudApp.Components.User;

/// <summary>
///     管理者権限切り替えデバッグページ (MakeMeAdmin) の権限判定・状態遷移を担当する ViewModel。
/// </summary>
public sealed class MakeMeAdminViewModel
{
    /// <summary>現在ログイン中ユーザーが管理者ロールを持つかどうか。</summary>
    public bool IsAdmin { get; set; }

    /// <summary>切り替え先となる目標管理者状態。</summary>
    public bool TargetAdminState { get; set; }

    /// <summary>現在権限切り替えの送信処理中かどうか。</summary>
    public bool IsProcessing { get; set; }

    /// <summary>
    ///     ログインユーザー情報に基づいて現在の管理者権限状態を初期化します。
    /// </summary>
    public void Initialize(ClaimsPrincipal? user)
    {
        IsAdmin = user?.IsInRole("Admin") == true;
        TargetAdminState = !IsAdmin;
    }

    /// <summary>
    ///     管理者権限の切り替えを要求し、非表示フォームの送信をトリガーします。
    /// </summary>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "JS interop failure should reset processing state")]
    public async Task<bool> ToggleAdminAsync(bool newValue, IJSRuntime jsRuntime)
    {
        ArgumentNullException.ThrowIfNull(jsRuntime);

        if (IsProcessing)
        {
            return false;
        }

        IsProcessing = true;
        TargetAdminState = newValue;

        try
        {
            await jsRuntime.InvokeVoidAsync("eval", "document.getElementById('toggleAdminSubmitBtn').click()");
            return true;
        }
        catch
        {
            IsProcessing = false;
            return false;
        }
    }
}