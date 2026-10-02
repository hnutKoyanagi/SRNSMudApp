namespace SRNSMudApp.Components.Tag;

using System.Threading.Tasks;

using SRNSMudApp.Data;
using SRNSMudApp.Services;

/// <summary>
///     タグのウェイト変更操作の結果。
/// </summary>
public enum TagWeightOperationType
{
    ExecutedDirectly,
    ProposedContract,
    Ignored
}

public sealed record TagWeightActionResult(
    TagWeightOperationType OperationType,
    UpdateWeightResult? UpdateResult = null,
    string? ErrorMessage = null)
{
    public bool IsSuccess => OperationType switch
    {
        TagWeightOperationType.ExecutedDirectly => UpdateResult == UpdateWeightResult.Success,
        TagWeightOperationType.ProposedContract => true,
        TagWeightOperationType.Ignored => false,
        _ => false
    };
}

/// <summary>
///     ItemTagChip におけるタグ操作（Weight変更、関連付け解除・変更、階層タグ操作、契約提案分岐）のロジックを集約する ViewModel。
///     bUnit を介さずに直接単体テスト可能。
/// </summary>
public class ItemTagChipActionViewModel
{
    private readonly IItemTagService _itemTagService;

    public ItemTagChipActionViewModel(IItemTagService itemTagService)
    {
        _itemTagService = itemTagService;
    }

    /// <summary>
    ///     タグの関連付けに対して Weight 変更または契約提案を実行する。
    /// </summary>
    public async Task<TagWeightActionResult> UpdateWeightAsync(
        TagRelation relation,
        int delta,
        string currentUserId)
    {
        if (relation == null)
        {
            return new TagWeightActionResult(TagWeightOperationType.Ignored, ErrorMessage: "リレーションが指定されていません。");
        }

        bool isOwner = relation.OwnerId == currentUserId;
        bool hasTag = relation.Tag != null;

        if (isOwner)
        {
            UpdateWeightResult result = await _itemTagService.UpdateTagWeightAsync(relation.Id, delta, currentUserId);
            string? errorMessage = result switch
            {
                UpdateWeightResult.Success => null,
                UpdateWeightResult.NoPermission => "関連付けた本人ではないため、Weightを変更する権限がありません。",
                UpdateWeightResult.NotFound => "タグの関連付けが見つかりません。",
                _ => "不明なエラーが発生しました。"
            };

            return new TagWeightActionResult(TagWeightOperationType.ExecutedDirectly, result, errorMessage);
        }

        if (hasTag)
        {
            // 他人のタグの場合は契約提案（Propose Contract）へルーティング
            return new TagWeightActionResult(TagWeightOperationType.ProposedContract);
        }

        return new TagWeightActionResult(TagWeightOperationType.Ignored);
    }

    /// <summary>
    ///     タグの関連付けを解除する。成功時は null、失敗時はエラーメッセージを返す。
    /// </summary>
    public async Task<string?> RemoveTagRelationAsync(int relationId, string currentUserId)
    {
        return await _itemTagService.RemoveTagRelationAsync(relationId, currentUserId);
    }

    /// <summary>
    ///     タグのウェイトを絶対値で設定する。成功時は null、失敗時はエラーメッセージを返す。
    /// </summary>
    public async Task<string?> SetTagWeightAsync(int relationId, int newWeight, string currentUserId)
    {
        return await _itemTagService.SetTagWeightAsync(relationId, newWeight, currentUserId);
    }

    /// <summary>
    ///     アイテムのタグ関連付けを変更する。本人権限チェック付き。成功時は null、失敗時はエラーメッセージを返す。
    /// </summary>
    public async Task<string?> ChangeItemTagAsync(int relationId, int newTagId, int itemId, string currentUserId, string relationOwnerId)
    {
        if (relationOwnerId != currentUserId)
        {
            return "関連付けた本人ではないため、変更する権限がありません。";
        }

        return await _itemTagService.ChangeItemTagAsync(relationId, newTagId, itemId, currentUserId);
    }

    /// <summary>
    ///     タグ同士を関連付ける。成功時は null、失敗時はエラーメッセージを返す。
    /// </summary>
    public async Task<string?> AddTagToTagAsync(int targetTagId, int selectedTagId, string currentUserId)
    {
        return await _itemTagService.AddTagToTagAsync(targetTagId, selectedTagId, currentUserId);
    }

    /// <summary>
    ///     タグ同士の関連付けを解除する。本人権限チェック付き。成功時は null、失敗時はエラーメッセージを返す。
    /// </summary>
    public async Task<string?> RemoveTagToTagRelationAsync(int relationId, string currentUserId, string relationOwnerId)
    {
        if (relationOwnerId != currentUserId)
        {
            return "関連付けた本人ではないため、解除する権限がありません。";
        }

        return await _itemTagService.RemoveTagToTagRelationAsync(relationId, currentUserId);
    }
}