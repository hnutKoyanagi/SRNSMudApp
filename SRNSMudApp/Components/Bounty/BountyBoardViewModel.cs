// Components/Bounty/BountyBoardViewModel.cs
#region

using MudBlazor;

using SRNSMudApp.Data;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;

#endregion

namespace SRNSMudApp.Components.Bounty;

/// <summary>
///     BountyBoard コンポーネントの表示・判定・データ読み込みを担当する ViewModel。
///     バウンティ一覧の取得、報酬アセットの解決、完了可否判定、ダイアログ設定をカプセル化する。
/// </summary>
public class BountyBoardViewModel
{
    private readonly IBountyDataProvider _bountyData;

    /// <summary>
    ///     コンストラクタ。
    /// </summary>
    public BountyBoardViewModel(IBountyDataProvider bountyData)
    {
        _bountyData = bountyData ?? throw new ArgumentNullException(nameof(bountyData));
    }

    /// <summary>読み込み中フラグ。</summary>
    public bool IsLoading { get; private set; } = true;

    /// <summary>アクティブなバウンティ一覧。</summary>
    public IReadOnlyList<TaggingRequestEntity> Bounties { get; private set; } = [];

    /// <summary>報酬アセットの辞書。</summary>
    public Dictionary<int, RightAsset> RewardAssets { get; private set; } = [];

    /// <summary>現在ログインしているユーザーの ID。</summary>
    public string CurrentUserId { get; set; } = string.Empty;

    /// <summary>
    ///     バウンティ一覧と報酬アセットを非同期で読み込む。
    /// </summary>
    public async Task LoadDataAsync()
    {
        IsLoading = true;
        try
        {
            BountyBoardData board = await _bountyData.GetActiveBountiesAsync();
            Bounties = board.Bounties;
            RewardAssets = board.RewardAssets;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    ///     指定バウンティの報酬となる RightAsset を解決する。
    /// </summary>
    /// <param name="bounty">対象のバウンティ依頼。</param>
    /// <returns>解決された報酬アセット。無償依頼または未解決の場合は null。</returns>
    public RightAsset? ResolveRewardAsset(TaggingRequestEntity bounty) =>
        ResolveRewardAsset(bounty, RewardAssets);

    /// <summary>
    ///     指定バウンティを現在のユーザーが完了可能かどうか（自身の依頼でないか）を判定する。
    /// </summary>
    /// <param name="bounty">対象のバウンティ依頼。</param>
    /// <returns>完了可能な場合は true。</returns>
    public bool CanFulfillBounty(TaggingRequestEntity bounty) =>
        CanFulfillBounty(bounty, CurrentUserId);

    /// <summary>
    ///     バウンティ作成ダイアログ用のオプションを生成する。
    /// </summary>
    public static DialogOptions CreateDialogOptions() => new()
    {
        CloseOnEscapeKey = true,
        MaxWidth = MaxWidth.Small,
        FullWidth = true
    };

    /// <summary>
    ///     バウンティ完了ダイアログ用のパラメータを生成する。
    /// </summary>
    public static DialogParameters<FulfillBountyDialog> FulfillDialogParameters(TaggingRequestEntity bounty)
    {
        ArgumentNullException.ThrowIfNull(bounty);
        return new DialogParameters<FulfillBountyDialog>
        {
            { x => x.Bounty, bounty }
        };
    }

    /// <summary>
    ///     バウンティ完了ダイアログ用のオプションを生成する。
    /// </summary>
    public static DialogOptions FulfillDialogOptions() => new()
    {
        CloseOnEscapeKey = true,
        MaxWidth = MaxWidth.Small,
        FullWidth = true
    };

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