using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

using SRNSMudApp.Components.Item;
using SRNSMudApp.Models;

namespace SRNSMudApp.Components.UI;

/// <summary>
///     真善美リアクションバー (ReactionBar) のキー入力判定やアイテム詳細遷移 URL 生成を担当する ViewModel。
/// </summary>
public static class ReactionBarViewModel
{
    /// <summary>
    ///     キーボードイベント（Enter または Space）がアクションを発火すべきキーかどうかを判定する。
    /// </summary>
    public static bool ShouldTriggerAction(KeyboardEventArgs? e) =>
        e?.Key is "Enter" or " ";

    /// <summary>
    ///     指定されたアイテム ID およびリアクションタグ名に基づいて、タグ管理タブを開くクエリ付き URI を生成する。
    /// </summary>
    [SuppressMessage("Design", "CA1054:UriParametersShouldNotBeStrings", Justification = "NavigationManager URL pattern")]
    [SuppressMessage("Design", "CA1055:UriReturnValuesShouldNotBeStrings", Justification = "NavigationManager URL pattern")]
    public static string BuildItemDetailUri(int itemId, string tagName, NavigationManager navigationManager)
    {
        ArgumentNullException.ThrowIfNull(navigationManager);

        var state = ItemDetailQueryStateFactory.Create(
            tabIndex: 1,
            selectedRequestId: null,
            [FilterEntry.FromName(tagName)]);
        var parameters = ItemDetailQueryStateFactory.BuildParameters(state);
        return navigationManager.GetUriWithQueryParameters($"/ItemDetail/{itemId}", parameters);
    }
}