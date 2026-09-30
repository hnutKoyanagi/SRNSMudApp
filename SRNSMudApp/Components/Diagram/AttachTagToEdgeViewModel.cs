#region

using SRNSMudApp.Data;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;

using TagEntity = SRNSMudApp.Data.Tag;

#endregion

namespace SRNSMudApp.Components.Diagram;

/// <summary>
///     エッジへのタグ紐付けダイアログ用 ViewModel。
///     タグの選択・検索、消費可能な RightAsset の取得、付与 Weight のバリデーションおよび紐付け結果の生成をカプセル化する。
/// </summary>
public sealed class AttachTagToEdgeViewModel
{
    private readonly ITagDiagramDataProvider _diagramDataProvider;

    public AttachTagToEdgeViewModel(ITagDiagramDataProvider diagramDataProvider)
    {
        _diagramDataProvider = diagramDataProvider ?? throw new ArgumentNullException(nameof(diagramDataProvider));
    }

    public TagEdge? Edge { get; private set; }
    public string CurrentUserId { get; private set; } = string.Empty;
    public IReadOnlyList<TagEntity> AvailableTags { get; private set; } = [];

    public TagEntity? SelectedTag { get; set; }
    public RightAsset? SelectedAsset { get; set; }
    public IReadOnlyList<RightAsset> AvailableAssets { get; private set; } = [];
    public int Weight { get; set; } = 1;
    public bool IsLoadingAssets { get; private set; }

    public bool IsOwnerTag =>
        SelectedTag != null &&
        !string.IsNullOrWhiteSpace(CurrentUserId) &&
        SelectedTag.OwnerId == CurrentUserId;

    public bool CanSubmit =>
        SelectedTag != null &&
        SelectedAsset != null &&
        Weight >= 1 &&
        Weight <= SelectedAsset.Amount;

    /// <summary>
    ///     対象エッジ、現在のユーザーID、および選択可能なタグ一覧を設定して初期化する。
    /// </summary>
    public void Initialize(TagEdge edge, string currentUserId, IReadOnlyList<TagEntity> availableTags)
    {
        Edge = edge ?? throw new ArgumentNullException(nameof(edge));
        CurrentUserId = currentUserId ?? string.Empty;
        AvailableTags = availableTags ?? [];
        SelectedTag = null;
        SelectedAsset = null;
        AvailableAssets = [];
        Weight = 1;
        IsLoadingAssets = false;
    }

    /// <summary>
    ///     タグを選択し、利用可能な RightAsset を読み込む。
    /// </summary>
    public async Task SelectTagAsync(TagEntity? tag, CancellationToken cancellationToken = default)
    {
        SelectedTag = tag;
        SelectedAsset = null;
        AvailableAssets = [];
        Weight = 1;

        if (tag == null || string.IsNullOrWhiteSpace(CurrentUserId))
        {
            return;
        }

        IsLoadingAssets = true;
        try
        {
            var assets = await _diagramDataProvider.GetAvailableRightAssetsAsync(CurrentUserId, tag.Id);
            AvailableAssets = assets ?? [];
            SelectedAsset = AvailableAssets.Count > 0 ? AvailableAssets[0] : null;
        }
        finally
        {
            IsLoadingAssets = false;
        }
    }

    /// <summary>
    ///     利用可能なタグから検索を行う。
    /// </summary>
    public IEnumerable<TagEntity> SearchTags(string? query)
    {
        IEnumerable<TagEntity> list = AvailableTags;
        if (!string.IsNullOrWhiteSpace(query))
        {
            list = list.Where(t => t.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        return list.Take(20);
    }

    /// <summary>
    ///     紐付け確定の検証を行い、結果オブジェクトを返す。
    /// </summary>
    public Result<(int TagId, int RightAssetId, int Weight)> Submit()
    {
        if (SelectedTag == null)
        {
            return new Failure("タグを選択してください。");
        }

        if (SelectedAsset == null)
        {
            return new Failure("消費する RightAsset を選択してください。");
        }

        if (Weight < 1)
        {
            return new Failure("Weight は 1 以上を指定してください。");
        }

        if (Weight > SelectedAsset.Amount)
        {
            return new Failure($"Weight はアセット残量（{SelectedAsset.Amount}）以下を指定してください。");
        }

        return new Success<(int TagId, int RightAssetId, int Weight)>((
            TagId: SelectedTag.Id,
            RightAssetId: SelectedAsset.Id,
            Weight: Weight));
    }
}