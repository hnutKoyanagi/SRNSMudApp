namespace SRNSMudApp.Components.User;

using System.Security.Claims;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

/// <summary>
///     管理者向けユーザー管理ページコンポーネントのコードビハインド。
///     ユーザー一覧の取得、Admin権限付与・剥奪、アカウント利用停止（BAN）切り替えを制御する。
/// </summary>
public partial class UserManagement : ComponentBase
{
    [CascadingParameter]
    private Task<AuthenticationState>? AuthStateTask { get; set; }

    [Inject]
    private UserManagementViewModel ViewModel { get; set; } = null!;

    protected override async Task OnInitializedAsync()
    {
        string? currentUserId = null;
        if (AuthStateTask is not null)
        {
            var authState = await AuthStateTask;
            currentUserId = authState.User.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        await ViewModel.InitializeAsync(currentUserId);
    }
}