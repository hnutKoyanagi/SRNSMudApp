namespace SRNSMudApp.Components.Bounty;

using System.Security.Claims;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

using MudBlazor;

using SRNSMudApp.Data;
using SRNSMudApp.Services.Dialogs;

/// <summary>
///     バウンティ（タグ付け依頼）掲示板画面コンポーネントのコードビハインド。
///     バウンティ一覧の表示、新規作成ダイアログの起動、依頼完了ダイアログの起動を制御する。
/// </summary>
public partial class BountyBoard : ComponentBase
{
    [CascadingParameter]
    private Task<AuthenticationState> AuthStateTask { get; set; } = default!;

    [Inject]
    private BountyBoardViewModel ViewModel { get; set; } = null!;

    [Inject]
    private IDialogLauncher DialogLauncher { get; set; } = null!;


    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateTask;
        ViewModel.CurrentUserId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

        await ViewModel.LoadDataAsync();
    }

    private async Task OpenCreateDialog()
    {
        var options = BountyBoardViewModel.CreateDialogOptions();
        var dialog = await DialogLauncher.ShowAsync<BountyCreateDialog>("タグ付けの依頼を作成", options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            await ViewModel.LoadDataAsync();
        }
    }

    private async Task FulfillBounty(TaggingRequestEntity bounty)
    {
        var parameters = BountyBoardViewModel.FulfillDialogParameters(bounty);
        var options = BountyBoardViewModel.FulfillDialogOptions();
        var dialog = await DialogLauncher.ShowAsync<FulfillBountyDialog>("依頼を叶える", parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            await ViewModel.LoadDataAsync();
        }
    }
}