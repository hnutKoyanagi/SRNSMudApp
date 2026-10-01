namespace SRNSMudApp.Components.PublicOffer;

using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

using MudBlazor;

using SRNSMudApp.Data;
using SRNSMudApp.Models.Unions;

/// <summary>
///     パブリックオファー実行ダイアログコンポーネントのコードビハインド。
///     ユーザーによる対象アイテムおよび消費アセットの選択、入力検証、オファー実行処理を制御する。
/// </summary>
public partial class TriggerPublicOfferDialog : ComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    [CascadingParameter]
    private Task<AuthenticationState> AuthStateTask { get; set; } = default!;

    [Parameter]
    public PublicTradeOffer Offer { get; set; } = null!;

    [Inject]
    private TriggerPublicOfferViewModel ViewModel { get; set; } = null!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = null!;

    private MudForm? _form;
    private bool _isValid;

    private Item? SelectedItem
    {
        get => ViewModel.SelectedItem;
        set => ViewModel.SelectedItem = value;
    }

    private RightAsset? SelectedAsset
    {
        get => ViewModel.SelectedAsset;
        set => ViewModel.SelectedAsset = value;
    }

    private IReadOnlyList<RightAsset> ValidAssets => ViewModel.ValidAssets;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateTask;
        ViewModel.CurrentUserId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        ViewModel.Offer = Offer;

        if (Offer.RequiredAssetAmount > 0)
        {
            await ViewModel.LoadMyAssetsAsync();
        }
    }

    private async Task<IEnumerable<Item>> SearchItems(string? value, CancellationToken token)
    {
        return await ViewModel.SearchItemsAsync(value, token);
    }

    private async Task Submit()
    {
        if (_form != null)
        {
            await _form.ValidateAsync();
        }

        if (!_isValid)
        {
            return;
        }

        var result = await ViewModel.SubmitAsync();
        switch (result)
        {
            case Success<int> success:
                MudDialog.Close(DialogResult.Ok(success.Value));
                break;
            case Failure failure:
                Snackbar.Add(failure.ErrorMessage, Severity.Error);
                break;
            default:
                break;
        }
    }

    private void Cancel()
    {
        MudDialog.Cancel();
    }
}