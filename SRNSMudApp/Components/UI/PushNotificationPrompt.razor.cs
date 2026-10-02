namespace SRNSMudApp.Components.UI;

using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

/// <summary>
///     Web Push通知の有効化プロンプトおよび管理コンポーネントのコードビハインド。
///     通知許可要求、テスト通知送信、通知購読解除処理を制御する。
/// </summary>
public partial class PushNotificationPrompt : ComponentBase
{
    [CascadingParameter]
    private Task<AuthenticationState> AuthenticationStateTask { get; set; } = default!;

    /// <summary>
    ///     購読済みの場合にもステータスやテスト通知ボタンを表示するかどうか。
    /// </summary>
    [Parameter]
    public bool ShowWhenSubscribed { get; set; } = true;

    [Inject]
    private PushNotificationPromptViewModel ViewModel { get; set; } = null!;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            var authState = await AuthenticationStateTask;
            ViewModel.ShowWhenSubscribed = ShowWhenSubscribed;
            await ViewModel.LoadSubscriptionStatusAsync(authState?.User);
            StateHasChanged();
        }
    }

    /// <summary>
    ///     ブラウザの通知許可ダイアログを表示し、VAPID鍵を用いてWeb Pushを購読します。
    /// </summary>
    public async Task RequestPushPermissionAsync()
    {
        await ViewModel.RequestPushPermissionAsync();
    }

    private async Task UnsubscribeAsync()
    {
        await ViewModel.UnsubscribeAsync();
    }

    private async Task SendTestNotificationAsync()
    {
        await ViewModel.SendTestNotificationAsync();
    }
}