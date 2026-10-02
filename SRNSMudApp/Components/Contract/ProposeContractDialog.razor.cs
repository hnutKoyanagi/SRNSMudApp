namespace SRNSMudApp.Components.Contract;

using System.Security.Claims;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

using MudBlazor;

using SRNSMudApp.Data;

using ItemEntity = SRNSMudApp.Data.Item;
using TagEntity = SRNSMudApp.Data.Tag;

/// <summary>
///     タグ契約提案（Propose Contract）ダイアログのコードビハインド。
///     UI レンダリングとユーザー入力を ViewModel へ仲介し、コンポーネントの関心事を分離する。
/// </summary>
public partial class ProposeContractDialog : ComponentBase
{
    [Inject] private ProposeContractViewModel ViewModel { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = default!;
    [CascadingParameter] private Task<AuthenticationState> AuthStateTask { get; set; } = default!;

    /// <summary>
    ///     契約提案の対象となるアイテム。指定されていない場合はダイアログ内でユーザーが選択します。
    /// </summary>
    [Parameter] public ItemEntity? TargetItem { get; set; }

    /// <summary>
    ///     提案対象（付与・変更・削除を希望する）のタグエンティティ。
    /// </summary>
    [Parameter] public TagEntity RequestedTag { get; set; } = null!;

    /// <summary>
    ///     初期設定する Weight 変更量。未指定（0）の場合は 1 がデフォルト値となります。
    /// </summary>
    [Parameter] public int WeightDelta { get; set; }

    /// <summary>
    ///     タグ削除リクエストかどうかを示すフラグ。
    /// </summary>
    [Parameter] public bool IsRemovalRequest { get; set; }

    /// <summary>
    ///     MudForm のバリデーション状態とのバインディング用フラグ。
    /// </summary>
    private bool _isValid = true;
    private int _activeTabIndex;
    private string _currentUserId = string.Empty;
    private int ProposedWeightInput { get; set; } = 1;
    private ItemEntity? _selectedTargetItem;

    // Gratis fields
    private string? _gratisMessage;

    // Mutual fields
    private ItemEntity? _offeredItem;
    private TagEntity? _offeredTag;
    private RightAsset? _selectedRightAsset;
    private IReadOnlyList<RightAsset> _myAssets = [];

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateTask;
        _currentUserId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

        ProposedWeightInput = WeightDelta != 0 ? WeightDelta : 1;
        _selectedTargetItem = TargetItem;

        await LoadMyAssetsAsync();
    }

    private async Task LoadMyAssetsAsync()
    {
        _myAssets = await ViewModel.GetAvailableRightAssetsAsync(_currentUserId);
    }

    private async Task<IEnumerable<ItemEntity>> SearchItems(string? value, CancellationToken token)
    {
        return await ViewModel.SearchItemsAsync(value, token);
    }

    private async Task<IEnumerable<TagEntity>> SearchTags(string? value, CancellationToken token)
    {
        return await ViewModel.SearchTagsByNameAsync(value, token);
    }

    private async Task Submit()
    {
        if (!_isValid)
        {
            Snackbar.Add("入力内容に誤りがあります。確認してください。", Severity.Warning);
            return;
        }

        var effectiveTargetItem = TargetItem ?? _selectedTargetItem;
        if (effectiveTargetItem is null)
        {
            Snackbar.Add("対象のアイテムを選択してください。", Severity.Error);
            return;
        }

        if (ProposedWeightInput == 0 && !IsRemovalRequest)
        {
            Snackbar.Add("0以外のWeight変更値を入力してください。", Severity.Error);
            return;
        }

        var isMutual = _activeTabIndex == 1;
        if (isMutual)
        {
            (bool isMutualValid, string? mutualError) = ProposeContractViewModel.ValidateMutualProposal(
                _offeredItem?.Id ?? 0,
                _offeredTag?.Id ?? 0,
                _selectedRightAsset?.Id);
            if (!isMutualValid)
            {
                Snackbar.Add(mutualError ?? "相互タグ付けに必要な項目を入力してください。", Severity.Error);
                return;
            }
        }

        var requestType = ProposeContractViewModel.ResolveRequestType(IsRemovalRequest, ProposedWeightInput);
        var absoluteWeight = Math.Abs(ProposedWeightInput);

        (bool success, string? errorMessage) = isMutual
            ? await ViewModel.ProposeMutualContractAsync(
                _currentUserId,
                RequestedTag.OwnerId,
                effectiveTargetItem.Id,
                RequestedTag.Id,
                _offeredItem!.Id,
                _offeredTag!.Id,
                _selectedRightAsset!.Id,
                requestType,
                absoluteWeight)
            : await ViewModel.ProposeGratisContractAsync(
                _currentUserId,
                RequestedTag.OwnerId,
                effectiveTargetItem.Id,
                RequestedTag.Id,
                requestType,
                absoluteWeight,
                _gratisMessage);

        if (!success)
        {
            Snackbar.Add(errorMessage ?? "契約提案の送信に失敗しました。", Severity.Error);
            return;
        }

        Snackbar.Add("コントラクトを提案しました。", Severity.Success);
        MudDialog.Close(DialogResult.Ok(true));
    }

    private void Cancel()
    {
        MudDialog.Cancel();
    }
}