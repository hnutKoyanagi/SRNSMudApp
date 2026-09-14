using System.ComponentModel.DataAnnotations;

namespace SRNSMudApp.Data;

/// <summary>
///     特定の会話（リプライツリーのルートアイテム）からの通知をオプトアウト（会話から抜ける）したユーザーを記録するエンティティ。
/// </summary>
public class ConversationOptOut
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser User { get; set; } = null!;

    [Required]
    public int RootItemId { get; set; }

    public Item RootItem { get; set; } = null!;

    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.UtcNow;
}