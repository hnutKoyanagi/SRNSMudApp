namespace SRNSMudApp.Models;

using SRNSMudApp.Data;

/// <summary>
///     新規アイテム投稿の保存データを表現する不変モデル。
///     バリデーション済みの本文、公開範囲 (ItemVisibility)、宛先通知先、付与タグ一覧をカプセル化する。
/// </summary>
public sealed record ItemPostDraft(
    string Content,
    string OwnerId,
    ItemVisibility Visibility,
    IReadOnlyList<string> RecipientUserIds,
    IReadOnlyList<int> TagIds,
    int? ParentItemId = null,
    int? RootItemId = null)
{
    /// <summary>
    ///     データベース保存用の Item エンティティを生成する。
    /// </summary>
    public Item ToItemEntity()
    {
        var item = new Item
        {
            Content = Content,
            OwnerId = OwnerId,
            IsPrivate = Visibility.IsPrivate,
            TargetUserGroupId = Visibility.TargetUserGroupId,
            ParentItemId = ParentItemId,
            RootItemId = RootItemId
        };

        var recipients = RecipientUserIds
            .Distinct()
            .Select(id => new ItemReplyNotificationRecipient
            {
                RecipientUserId = id,
                CreatedDate = DateTimeOffset.UtcNow
            })
            .ToList();

        if (recipients.Count > 0)
        {
            item.NotificationRecipients = recipients;
        }

        return item;
    }
}