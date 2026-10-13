namespace SRNSMudApp.Components.Tag;

using System.Security.Claims;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

/// <summary>
///     タグ一覧ページコンポーネントのコードビハインド。
///     タグ一覧データの初期ロードおよび更新を制御する。
/// </summary>
public partial class TagList : ComponentBase
{
    [Inject]
    private TagListViewModel ViewModel { get; set; } = null!;

    [CascadingParameter]
    private Task<AuthenticationState>? AuthState { get; set; }

    protected override async Task OnInitializedAsync()
    {
        string? currentUserId = null;
        var isAdmin = false;
        if (AuthState is not null)
        {
            AuthenticationState authState = await AuthState;
            currentUserId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            isAdmin = authState.User.IsInRole("Admin");
        }

        ViewModel.SetUser(currentUserId, isAdmin);
        await ViewModel.LoadDataAsync();
    }
}