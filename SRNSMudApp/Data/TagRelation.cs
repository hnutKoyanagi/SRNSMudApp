using System.ComponentModel.DataAnnotations.Schema;

namespace SRNSMudApp.Data;

public class TagRelation : BaseEntity
{
    // required を外す
    public int ItemId { get; set; }
    public Item Item { get; set; } = null!;

    public int TagId { get; set; }
    public Tag Tag { get; set; } = null!;

    public int Weight { get; set; } = 1; // デフォルト値を入れるとさらに記述が減ります

    public int? CommentItemId { get; set; }
    public Item? CommentItem { get; set; }

    [NotMapped]
    public string? Comment
    {
        get => CommentItem?.Content;
        set
        {
            if (value is null)
            {
                CommentItem = null;
                CommentItemId = null;
            }
            else
            {
                CommentItem ??= new Item
                {
                    OwnerId = OwnerId ?? string.Empty,
                    ItemKindJson = System.Text.Json.JsonSerializer.Serialize(new SRNSMudApp.Models.Unions.TagCommentItem(ItemId, TagId))
                };
                CommentItem.Content = value;
                if (string.IsNullOrEmpty(CommentItem.ItemKindJson))
                {
                    CommentItem.ItemKindJson = System.Text.Json.JsonSerializer.Serialize(new SRNSMudApp.Models.Unions.TagCommentItem(ItemId, TagId));
                }
            }
        }
    }
}