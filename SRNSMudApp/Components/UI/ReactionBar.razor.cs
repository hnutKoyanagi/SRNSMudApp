namespace SRNSMudApp.Components.UI;

using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

/// <summary>
///     真・善・美リアクションバーコンポーネントのコードビハインド。
///     Upvote/Downvote 操作およびタグクリックによる詳細遷移を制御する。
/// </summary>
public partial class ReactionBar : ComponentBase
{
    [Inject]
    private NavigationManager NavigationManager { get; set; } = null!;

    [Parameter]
    public int ItemId { get; set; }

    [Parameter]
    public int ShinjiScore { get; set; }

    [Parameter]
    public int ZenScore { get; set; }

    [Parameter]
    public int BiScore { get; set; }

    [Parameter]
    public bool IsShinjiUpvoted { get; set; }

    [Parameter]
    public bool IsShinjiDownvoted { get; set; }

    [Parameter]
    public bool IsZenUpvoted { get; set; }

    [Parameter]
    public bool IsZenDownvoted { get; set; }

    [Parameter]
    public bool IsBiUpvoted { get; set; }

    [Parameter]
    public bool IsBiDownvoted { get; set; }

    [Parameter]
    public EventCallback<(string ReactionTagName, int TargetWeight)> OnReactionVoteClicked { get; set; }

    [Parameter]
    public EventCallback<string> OnReactionTagClicked { get; set; }

    private async Task HandleTagClickAsync(string tagName)
    {
        if (OnReactionTagClicked.HasDelegate)
        {
            await OnReactionTagClicked.InvokeAsync(tagName);
        }

        if (ItemId > 0)
        {
            // 他人に該当タグをつけてもらう（リクエストやタグ確認）ため、タグ管理タブ(tab=tags)を開いた状態でアイテム詳細に遷移する
            var uri = ReactionBarViewModel.BuildItemDetailUri(ItemId, tagName, NavigationManager);
            NavigationManager.NavigateTo(uri);
        }
    }

    private async Task HandleKeyDownAsync(KeyboardEventArgs e, string tagName)
    {
        if (ReactionBarViewModel.ShouldTriggerAction(e))
        {
            await HandleTagClickAsync(tagName);
        }
    }
}