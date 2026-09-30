using SRNSMudApp.Data;
using SRNSMudApp.Services;

namespace SRNSMudApp.Components.Tag;

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
///     ItemTagChip におけるタグ操作（Weight変更、契約提案分岐）のロジックを集約する ViewModel。
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
}