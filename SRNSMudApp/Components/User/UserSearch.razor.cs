namespace SRNSMudApp.Components.User;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;

using SRNSMudApp.Data;

/// <summary>
///     ユーザー検索ページコンポーネントのコードビハインド。
///     ユーザー名オートコンプリート検索および選択されたユーザー情報の表示を制御する。
/// </summary>
public partial class UserSearch : ComponentBase
{
    [Inject]
    private UserSearchViewModel ViewModel { get; set; } = null!;

    private Task<IEnumerable<ApplicationUser>> SearchUsersAsync(string? value, CancellationToken token)
    {
        return ViewModel.SearchUsersAsync(value, token);
    }
}