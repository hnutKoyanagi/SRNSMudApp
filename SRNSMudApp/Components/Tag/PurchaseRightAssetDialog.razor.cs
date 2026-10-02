namespace SRNSMudApp.Components.Tag;

using System.Security.Claims;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

using MudBlazor;

using SRNSMudApp.Data;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;

/// <summary>
///     JPYCによる操作権限（RightAsset）購入ダイアログコンポーネントのコードビハインド。
///     数量・単価入力、決済ネットワーク選択、入金アドレス表示、TxHash検証・送信を制御する。
///     ドメインロジック・ブロックチェーン連携は <see cref="PurchaseRightAssetViewModel"/> に委譲する。
/// </summary>
public partial class PurchaseRightAssetDialog : ComponentBase
{
    [Inject] private PurchaseRightAssetViewModel ViewModel { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;
    [CascadingParameter] private Task<AuthenticationState>? AuthState { get; set; }

    [Parameter] public Tag RequestedTag { get; set; } = null!;
    [Parameter] public int DefaultAmount { get; set; } = 1;
    [Parameter] public int DefaultUnitPriceJpyc { get; set; } = 100;

    private MudForm? _form;
    private bool _isValid = true;

    protected override async Task OnInitializedAsync()
    {
        var currentUserId = string.Empty;
        if (AuthState is not null)
        {
            var auth = await AuthState;
            currentUserId = auth.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        }

        await ViewModel.InitializeAsync(RequestedTag, currentUserId, DefaultAmount, DefaultUnitPriceJpyc);
    }

    private async Task OnNetworkChangedAsync(string newNetwork)
    {
        await ViewModel.SelectNetworkAsync(newNetwork);
    }

    private async Task SimulatePaymentAsync()
    {
        var result = await ViewModel.SimulatePaymentAsync();
        switch (result)
        {
            case Success<string>:
                if (_form is not null)
                {
                    await _form.ValidateAsync();
                }

                Snackbar.Add("あなた専用アドレス宛ての送金をシミュレートしました。TxHashを自動入力しました。", Severity.Success);
                break;
            case Failure fail:
                Snackbar.Add(fail.ErrorMessage, Severity.Error);
                break;
            default:
                break;
        }
    }

    private async Task CopyToClipboard(string text, string label)
    {
        try
        {
            await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", text);
            Snackbar.Add($"{label}をクリップボードにコピーしました。", Severity.Success);
        }
        catch (JSException)
        {
            Snackbar.Add($"{label}: {text}", Severity.Info);
        }
    }

    private void Cancel()
    {
        MudDialog.Cancel();
    }

    private async Task SubmitAsync()
    {
        if (_form is not null)
        {
            await _form.ValidateAsync();
            if (!_isValid)
            {
                return;
            }
        }

        var result = await ViewModel.SubmitPurchaseAsync();
        switch (result)
        {
            case Success<RightAsset> success:
                Snackbar.Add($"送金完了を確認しました！操作権限 {ViewModel.Amount} を付与しました（{ViewModel.TotalJpyc:N0} JPYC）", Severity.Success);
                MudDialog.Close(DialogResult.Ok(success.Value));
                break;
            case Failure fail:
                Snackbar.Add(fail.ErrorMessage, Severity.Error);
                break;
            default:
                break;
        }
    }
}