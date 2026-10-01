namespace SRNSMudApp.Components.UI;

using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;

using SRNSMudApp.Models;

/// <summary>
///     ItemCard のリプライスレッド表示を担う表示専用コンポーネントのコードビハインド。
///     サービス注入を持たず、データとコールバックのみで駆動する。
/// </summary>
public partial class ItemReplyThread : ComponentBase
{
    [Parameter] public int ItemId { get; set; }

    [Parameter] public bool IsExpanded { get; set; }

    [Parameter] public int? ReplyCount { get; set; }

    [Parameter] public IEnumerable<Data.Item>? Replies { get; set; }

    /// <summary>リプライ 1 件の描画テンプレート (通常はネストした ItemCard)。</summary>
    [Parameter] public RenderFragment<Data.Item>? ReplyTemplate { get; set; }

    [Parameter] public string NewReplyContent { get; set; } = "";

    [Parameter] public EventCallback<string> NewReplyContentChanged { get; set; }

    [Parameter] public bool IsSubmittingReply { get; set; }

    [Parameter] public int QuoteCount { get; set; }

    [Parameter] public EventCallback OnOpenQuoteDialog { get; set; }

    [Parameter] public EventCallback OnShowQuotes { get; set; }

    [Parameter] public EventCallback OnToggleReplies { get; set; }

    [Parameter] public EventCallback OnSubmitReply { get; set; }

    [Parameter] public IReadOnlyList<ReplyTargetCandidate>? TargetCandidates { get; set; }

    [Parameter] public IReadOnlyCollection<string>? SelectedTargetUserIds { get; set; }

    [Parameter] public EventCallback<(string UserId, bool IsSelected)> OnTargetToggled { get; set; }

    /// <summary>親アイテムがプライベートモードかどうか。</summary>
    [Parameter] public bool ParentIsPrivate { get; set; }

    /// <summary>親アイテムの対象ユーザーグループID。</summary>
    [Parameter] public int? ParentUserGroupId { get; set; }

    /// <summary>親アイテムの対象ユーザーグループ名。</summary>
    [Parameter] public string? ParentUserGroupName { get; set; }

    /// <summary>現在リプライにプライベートモードが適用されているかどうか。</summary>
    [Parameter] public bool IsPrivateReply { get; set; }

    /// <summary>プライベートモード切り替え時のコールバック。</summary>
    [Parameter] public EventCallback<bool> OnPrivateReplyChanged { get; set; }

    private async Task HandleNewReplyContentChanged(string value)
    {
        NewReplyContent = value ?? string.Empty;
        if (NewReplyContentChanged.HasDelegate)
        {
            await NewReplyContentChanged.InvokeAsync(NewReplyContent);
        }
    }

    private async Task HandleTargetToggled(string userId, bool isSelected)
    {
        if (OnTargetToggled.HasDelegate)
        {
            await OnTargetToggled.InvokeAsync((userId, isSelected));
        }
    }

    private async Task HandlePrivateReplyToggled(bool isPrivate)
    {
        IsPrivateReply = isPrivate;
        if (OnPrivateReplyChanged.HasDelegate)
        {
            await OnPrivateReplyChanged.InvokeAsync(isPrivate);
        }
    }
}