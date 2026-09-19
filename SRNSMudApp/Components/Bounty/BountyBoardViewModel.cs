// Components/Bounty/BountyBoardViewModel.cs
#region

using SRNSMudApp.Data;
using SRNSMudApp.Models.Unions;

#endregion

namespace SRNSMudApp.Components.Bounty;

/// <summary>
///     BountyBoard コンポーネントの表示・判定ロジックを担当する ViewModel。
///     UI への依存を持たないため、単体テストで高速かつ確実に検証可能。
/// </summary>
public static class BountyBoardViewModel
{
    /// <summary>
    ///     バウンティのペイロードから報酬となる RightAsset を解決する。
    /// </summary>
    /// <param name="bounty">対象のバウンティ依頼。</param>
    /// <param name="rewardAssets">取得済みの報酬アセット辞書。</param>
    /// <returns>解決された報酬アセット。無償依頼または未解決の場合は null。</returns>
    public static RightAsset? ResolveRewardAsset(
        TaggingRequestEntity bounty,
        IReadOnlyDictionary<int, RightAsset> rewardAssets)
    {
        ArgumentNullException.ThrowIfNull(bounty);
        ArgumentNullException.ThrowIfNull(rewardAssets);

        if (bounty.Payload is BountyPayload { OfferedRewardAssetId: not 0 } bp &&
            rewardAssets.TryGetValue(bp.OfferedRewardAssetId, out RightAsset? asset))
        {
            return asset;
        }

        return null;
    }

    /// <summary>
    ///     指定ユーザーがバウンティを完了可能かどうか（自身の依頼でないか）を判定する。
    /// </summary>
    /// <param name="bounty">対象のバウンティ依頼。</param>
    /// <param name="currentUserId">現在ログインしているユーザーの ID。</param>
    /// <returns>完了可能な場合は true。</returns>
    public static bool CanFulfillBounty(TaggingRequestEntity bounty, string? currentUserId)
    {
        ArgumentNullException.ThrowIfNull(bounty);

        if (string.IsNullOrEmpty(currentUserId))
        {
            return false;
        }

        return bounty.RequesterUserId != currentUserId;
    }
}

