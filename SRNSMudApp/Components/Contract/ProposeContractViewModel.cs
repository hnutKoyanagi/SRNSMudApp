using SRNSMudApp.Data;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;

namespace SRNSMudApp.Components.Contract;

/// <summary>
///     タグ契約提案（Propose Contract）の入力検証および提案実行ロジックを集約する ViewModel。
///     bUnit を使わずに xUnit で直接単体テスト可能。
/// </summary>
public class ProposeContractViewModel
{
    private readonly ITaggingContractService _contractService;

    public ProposeContractViewModel(ITaggingContractService contractService)
    {
        _contractService = contractService;
    }

    /// <summary>
    ///     契約提案入力値の検証を行う。
    /// </summary>
    public static (bool IsValid, string? ErrorMessage) ValidateProposal(
        int itemId,
        int tagId,
        int proposedWeight,
        string? requesterId)
    {
        if (itemId <= 0)
        {
            return (false, "対象アイテムが無効です。");
        }

        if (tagId <= 0)
        {
            return (false, "対象タグが無効です。");
        }

        if (string.IsNullOrEmpty(requesterId))
        {
            return (false, "申請者情報が必要です。");
        }

        if (proposedWeight == 0)
        {
            return (false, "変更する Weight (差分) を 0 以外で指定してください。");
        }

        return (true, null);
    }

    /// <summary>
    ///     提案のタイプ（追加・削除・減量）を解決する。
    /// </summary>
    public static TaggingRequestType ResolveRequestType(bool isRemoval, int proposedWeightInput)
    {
        if (isRemoval)
        {
            return TaggingRequestType.Remove;
        }

        return proposedWeightInput > 0
            ? TaggingRequestType.Add
            : TaggingRequestType.DecreaseWeight;
    }

    /// <summary>
    ///     相互提案の入力検証を行う。
    /// </summary>
    public static (bool IsValid, string? ErrorMessage) ValidateMutualProposal(int offeredItemId, int offeredTagId, int? assetId)
    {
        if (offeredItemId <= 0 || offeredTagId <= 0 || assetId is null or <= 0)
        {
            return (false, "相互タグ付けに必要な項目を入力してください。");
        }

        return (true, null);
    }

    /// <summary>
    ///     Gratis 契約提案を実行する。
    /// </summary>
    public async Task<(bool Success, string? ErrorMessage)> ProposeGratisContractAsync(
        string requesterUserId,
        string tagOwnerUserId,
        int targetItemId,
        int requestedTagId,
        TaggingRequestType requestType,
        int proposedWeight,
        string? message = null)
    {
        Result<TaggingRequestEntity> result = await _contractService.ProposeGratisContractAsync(
            requesterUserId: requesterUserId,
            tagOwnerUserId: tagOwnerUserId,
            targetItemId: targetItemId,
            requestedTagId: requestedTagId,
            requestType: requestType,
            proposedWeight: proposedWeight,
            message: message);

        return result switch
        {
            Success<TaggingRequestEntity> => (true, null),
            Failure failure => (false, failure.ErrorMessage),
            _ => (false, "契約提案の送信に失敗しました。")
        };
    }

    /// <summary>
    ///     Mutual 契約提案を実行する。
    /// </summary>
    public async Task<(bool Success, string? ErrorMessage)> ProposeMutualContractAsync(
        string requesterUserId,
        string tagOwnerUserId,
        int targetItemId,
        int requestedTagId,
        int offeredTargetItemId,
        int offeredTagId,
        int consumedRightAssetId,
        TaggingRequestType requestType,
        int proposedWeight)
    {
        Result<TaggingRequestEntity> result = await _contractService.ProposeMutualContractAsync(
            requesterUserId: requesterUserId,
            tagOwnerUserId: tagOwnerUserId,
            targetItemId: targetItemId,
            requestedTagId: requestedTagId,
            offeredTargetItemId: offeredTargetItemId,
            offeredTagId: offeredTagId,
            consumedRightAssetId: consumedRightAssetId,
            requestType: requestType,
            proposedWeight: proposedWeight);

        return result switch
        {
            Success<TaggingRequestEntity> => (true, null),
            Failure failure => (false, failure.ErrorMessage),
            _ => (false, "相互契約提案の送信に失敗しました。")
        };
    }

    /// <summary>
    ///     Gratis 契約提案を実行する（レガシー互換）。
    /// </summary>
    public async Task<(bool Success, string? ErrorMessage)> ProposeContractAsync(
        int itemId,
        int tagId,
        string tagOwnerUserId,
        int proposedWeight,
        string requesterId,
        string? comment = null)
    {
        (bool isValid, string? validationError) = ValidateProposal(itemId, tagId, proposedWeight, requesterId);
        if (!isValid)
        {
            return (false, validationError);
        }

        var requestType = ResolveRequestType(false, proposedWeight);
        return await ProposeGratisContractAsync(
            requesterId,
            tagOwnerUserId,
            itemId,
            tagId,
            requestType,
            Math.Abs(proposedWeight),
            comment);
    }
}