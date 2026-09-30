namespace SRNSMudApp.Components.Tag;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

using MudBlazor;

using SRNSMudApp.Components.Contract;
using SRNSMudApp.Components.UI;
using SRNSMudApp.Data;
using SRNSMudApp.Models;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;
using SRNSMudApp.Services.Dialogs;

using ItemEntity = SRNSMudApp.Data.Item;
using TagEntity = SRNSMudApp.Data.Tag;
using TaggingRequestEntity = SRNSMudApp.Data.TaggingRequestEntity;

/// <summary>
///     タグ詳細ページコンポーネントのコードビハインド。
///     タグの閲覧、編集、ロック操作、フォロー、通報、提案承認等の各種ダイアログ起動および UI 状態管理を担当する。
///     ドメイン操作・データ読み込みは <see cref="TagDetailViewModel"/> に委譲する。
/// </summary>
[SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Blazor template binding naming convention")]
public partial class TagDetail : ComponentBase
{
    [Inject] private TagDetailViewModel ViewModel { get; set; } = null!;
    [Inject] private IDialogLauncher DialogLauncher { get; set; } = null!;
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    [CascadingParameter] private Task<AuthenticationState>? AuthState { get; set; }

    [Parameter] public int TagId { get; set; }

    private TagEntity? _tag => ViewModel.Tag;
    private IEnumerable<ItemEntity> _relatedItems => ViewModel.RelatedItems;
    private IEnumerable<TagEntity> _relatedTags => ViewModel.RelatedTags;
    private IEnumerable<TagWeightLedger> _weightLedgers => ViewModel.WeightLedgers;
    private IEnumerable<PublicTradeOffer> _publicOffers => ViewModel.PublicOffers;
    private IEnumerable<TaggingRequestEntity> _pendingRequests => ViewModel.PendingRequests;
    private IReadOnlyList<TagContentProposal> _pendingProposals => ViewModel.PendingProposals;
    private IReadOnlyList<TagNameProposal> _pendingNameProposals => ViewModel.PendingNameProposals;
    private RightAssetOverviewData? _rightAssetOverview => ViewModel.RightAssetOverview;
    private bool _isFollowing => ViewModel.IsFollowing;
    private string? _currentUserId => ViewModel.CurrentUserId;
    private bool _isAdmin => ViewModel.IsAdmin;
    private bool _areAncestorsLocked => ViewModel.AreAncestorsLocked;
    private bool _isTagOrSiblingLocked => ViewModel.IsTagOrSiblingLocked;
    private bool _isTogglingAncestorsLock => ViewModel.IsTogglingAncestorsLock;
    private bool _isTogglingTagLock => ViewModel.IsTogglingTagLock;

    protected override async Task OnInitializedAsync()
    {
        if (AuthState is not null)
        {
            var authState = await AuthState;
            ViewModel.SetUserContext(authState.User);
        }
        await LoadDataAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (ViewModel.Tag is { Id: var id } && id != TagId)
        {
            await LoadDataAsync();
        }
    }

    private async Task LoadDataAsync()
    {
        await ViewModel.LoadDataAsync(TagId);
    }

    private async Task OnToggleThisTagLockAsync(bool shouldLock)
    {
        var (success, message, _) = await ViewModel.ToggleThisTagLockAsync();
        Snackbar.Add(message, success ? Severity.Success : Severity.Error);
    }

    private async Task OnToggleAncestorsLockAsync(bool shouldLock)
    {
        var (success, message) = await ViewModel.ToggleAncestorsLockAsync(shouldLock);
        Snackbar.Add(message, success ? Severity.Success : Severity.Error);
    }

    private async Task OpenEditDialog()
    {
        if (_tag is null)
        {
            return;
        }

        var parameters = new DialogParameters
        {
            { "Tag", _tag }
        };

        var dialog = await DialogLauncher.ShowAsync<TagEditDialog>("タグを編集", parameters);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            await LoadDataAsync();
        }
    }

    /// <summary>
    ///     タグ削除処理。
    ///     誤操作を防ぐため確認ダイアログを表示し、ユーザー承認後に DataProvider を通じて安全に削除を実行して一覧へ戻る。
    /// </summary>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "UI 層で発生した例外の内容をユーザーへ通知するために広く捕捉する")]
    private async Task DeleteTagAsync()
    {
        if (_tag is null)
        {
            return;
        }

        if (_isTagOrSiblingLocked && !_isAdmin)
        {
            _ = Snackbar.Add("このタグまたはその兄弟タグはロックされているため削除できません。", Severity.Warning);
            return;
        }

        if (!TagTableViewModel.CanDeleteTag(_tag, _currentUserId, _isTagOrSiblingLocked, _isAdmin))
        {
            _ = Snackbar.Add("システムタグ、または権限のないタグは削除できません。", Severity.Error);
            return;
        }

        var parameters = new DialogParameters<ConfirmDeleteDialog>
        {
            { x => x.ContentText, $"タグ「{_tag.Name}」を削除してもよろしいですか？この操作は取り消せません。" }
        };

        var dialog = await DialogLauncher.ShowAsync<ConfirmDeleteDialog>("タグの削除", parameters);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            try
            {
                var deleteResult = await ViewModel.DeleteTagAsync();
                switch (deleteResult)
                {
                    case TagDeleteOperationResult.Success:
                        _ = Snackbar.Add($"タグ「{_tag.Name}」を削除しました。", Severity.Success);
                        NavigationManager.NavigateTo("Item/ItemList");
                        break;
                    case TagDeleteOperationResult.Locked:
                        _ = Snackbar.Add("このタグまたはその兄弟タグはロックされているため削除できません。", Severity.Warning);
                        break;
                    case TagDeleteOperationResult.SystemTag:
                        _ = Snackbar.Add("システムタグは削除できません。", Severity.Error);
                        break;
                    case TagDeleteOperationResult.Unauthorized:
                        _ = Snackbar.Add("タグの作成者本人ではないため、削除する権限がありません。", Severity.Error);
                        break;
                    case TagDeleteOperationResult.NotFound:
                    default:
                        _ = Snackbar.Add("対象のタグが既に削除されているか、見つかりません。", Severity.Warning);
                        break;
                }
            }
            catch (Exception ex)
            {
                _ = Snackbar.Add($"エラーが発生しました: {ex.Message}", Severity.Error);
            }
        }
    }

    private async Task ToggleFollowAsync()
    {
        await ViewModel.ToggleFollowAsync();
    }

    private async Task OnAutoAcceptChangedAsync(bool enabled)
    {
        await ViewModel.UpdateAutoAcceptAsync(enabled);
    }

    private async Task OpenReportDialogAsync()
    {
        if (_currentUserId is null || _tag is null)
        {
            Snackbar.Add("通報するにはログインが必要です。", Severity.Warning);
            return;
        }

        var options = new DialogOptions { CloseOnEscapeKey = true, MaxWidth = MaxWidth.Small, FullWidth = true };
        var parameters = new DialogParameters
        {
            [nameof(ReportContentDialog.TargetType)] = ReportTargetType.Tag,
            [nameof(ReportContentDialog.TagId)] = _tag.Id,
            [nameof(ReportContentDialog.TargetContent)] = $"タグ名: {_tag.Name}\n{_tag.Content}",
            [nameof(ReportContentDialog.TargetOwnerName)] = _tag.Owner?.UserName
        };

        _ = await DialogLauncher.ShowAsync<ReportContentDialog>("不適切な投稿を通報", parameters, options);
    }

    private async Task OpenContentProposalDialog()
    {
        if (_tag is null)
        {
            return;
        }

        var parameters = new DialogParameters
        {
            { "Tag", _tag }
        };

        var dialog = await DialogLauncher.ShowAsync<TagContentProposalDialog>("タグ内容の編集提案", parameters);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            await LoadDataAsync();
        }
    }

    private async Task OpenNameProposalDialog()
    {
        if (_tag is null)
        {
            return;
        }

        var parameters = new DialogParameters
        {
            { "Tag", _tag }
        };

        var dialog = await DialogLauncher.ShowAsync<TagNameProposalDialog>("タグ名の編集提案", parameters);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            await LoadDataAsync();
        }
    }

    private async Task ApproveProposalAsync(int proposalId)
    {
        Result<TagEntity> result = await ViewModel.ApproveContentProposalAsync(proposalId);
        switch (result)
        {
            case Success<TagEntity>:
                Snackbar.Add("編集提案を承認しました。", Severity.Success);
                break;
            case Failure fail:
                Snackbar.Add(fail.ErrorMessage, Severity.Error);
                break;
            default:
                break;
        }
    }

    private async Task RejectProposalAsync(int proposalId)
    {
        var options = new DialogOptions { CloseOnEscapeKey = true, MaxWidth = MaxWidth.Small, FullWidth = true };
        IDialogReference dialog = await DialogLauncher.ShowAsync<RejectRequestDialog>("編集提案を却下", options);
        DialogResult? result = await dialog.Result;

        if (result is { Canceled: false })
        {
            var comment = result.Data as string;
            Result<bool> rejectResult = await ViewModel.RejectContentProposalAsync(proposalId, comment);
            switch (rejectResult)
            {
                case Success<bool>:
                    Snackbar.Add("編集提案を却下しました。", Severity.Success);
                    break;
                case Failure fail:
                    Snackbar.Add(fail.ErrorMessage, Severity.Error);
                    break;
                default:
                    break;
            }
        }
    }

    private async Task CancelProposalAsync(int proposalId)
    {
        Result<bool> result = await ViewModel.CancelContentProposalAsync(proposalId);
        switch (result)
        {
            case Success<bool>:
                Snackbar.Add("編集提案を取り下げました。", Severity.Success);
                break;
            case Failure fail:
                Snackbar.Add(fail.ErrorMessage, Severity.Error);
                break;
            default:
                break;
        }
    }

    private async Task ApproveNameProposalAsync(int proposalId)
    {
        Result<TagEntity> result = await ViewModel.ApproveNameProposalAsync(proposalId);
        switch (result)
        {
            case Success<TagEntity>:
                Snackbar.Add("名前変更提案を承認しました。", Severity.Success);
                break;
            case Failure fail:
                Snackbar.Add(fail.ErrorMessage, Severity.Error);
                break;
            default:
                break;
        }
    }

    private async Task RejectNameProposalAsync(int proposalId)
    {
        var options = new DialogOptions { CloseOnEscapeKey = true, MaxWidth = MaxWidth.Small, FullWidth = true };
        IDialogReference dialog = await DialogLauncher.ShowAsync<RejectRequestDialog>("名前変更提案を却下", options);
        DialogResult? result = await dialog.Result;

        if (result is { Canceled: false })
        {
            var comment = result.Data as string;
            Result<bool> rejectResult = await ViewModel.RejectNameProposalAsync(proposalId, comment);
            switch (rejectResult)
            {
                case Success<bool>:
                    Snackbar.Add("名前変更提案を却下しました。", Severity.Success);
                    break;
                case Failure fail:
                    Snackbar.Add(fail.ErrorMessage, Severity.Error);
                    break;
                default:
                    break;
            }
        }
    }

    private async Task CancelNameProposalAsync(int proposalId)
    {
        Result<bool> result = await ViewModel.CancelNameProposalAsync(proposalId);
        switch (result)
        {
            case Success<bool>:
                Snackbar.Add("名前変更提案を取り下げました。", Severity.Success);
                break;
            case Failure fail:
                Snackbar.Add(fail.ErrorMessage, Severity.Error);
                break;
            default:
                break;
        }
    }

    private Task OpenRequestPermissionDialogAsync() => OpenRequestPermissionDialogAsync(null);

    private async Task OpenRequestPermissionDialogAsync(string? presetUserId)
    {
        if (_tag is null || _currentUserId is null)
        {
            return;
        }

        var parameters = new DialogParameters<RequestTagPermissionDialog>
        {
            { x => x.RequestedTag, _tag },
            { x => x.PresetTargetUserId, presetUserId },
            { x => x.AvailableHolders, _rightAssetOverview?.Holders ?? [] }
        };
        var options = new DialogOptions { CloseOnEscapeKey = true, MaxWidth = MaxWidth.Medium, FullWidth = true };
        IDialogReference dialog = await DialogLauncher.ShowAsync<RequestTagPermissionDialog>("操作権限のリクエスト", parameters, options);
        DialogResult? result = await dialog.Result;

        if (result is { Canceled: false })
        {
            await LoadDataAsync();
        }
    }

    private async Task OpenPurchaseDialogAsync()
    {
        if (_tag is null || _currentUserId is null)
        {
            return;
        }

        var parameters = new DialogParameters<PurchaseRightAssetDialog>
        {
            { x => x.RequestedTag, _tag }
        };
        var options = new DialogOptions { CloseOnEscapeKey = true, MaxWidth = MaxWidth.Medium, FullWidth = true };
        IDialogReference dialog = await DialogLauncher.ShowAsync<PurchaseRightAssetDialog>("操作権限の購入 (JPYC)", parameters, options);
        DialogResult? result = await dialog.Result;

        if (result is { Canceled: false })
        {
            await LoadDataAsync();
        }
    }
}