using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web.Virtualization;

using MudBlazor;

using SRNSMudApp.Models;
using SRNSMudApp.Services.Dialogs;

namespace SRNSMudApp.Components.Pages;

/// <summary>
///     Home ページのコードビハインド。
///     タイムラインの初期化・データ供給、およびモバイル向けアイテム追加ダイアログ等の
///     UI オーケストレーションを担当する。
/// </summary>
public partial class Home : ComponentBase
{
    [CascadingParameter]
    protected internal Task<AuthenticationState> AuthStateTask { get; set; } = default!;

    [Inject]
    protected internal HomeViewModel ViewModel { get; set; } = null!;

    [Inject]
    protected internal IDialogLauncher DialogLauncher { get; set; } = null!;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateTask;
        await ViewModel.InitializeAsync(authState.User);
    }

    protected internal async ValueTask<ItemsProviderResult<TimelineFeedGroup>> LoadTimeline(ItemsProviderRequest request)
    {
        var (groups, totalCount) = await ViewModel.LoadTimelineAsync(request.StartIndex, request.Count);
        return new ItemsProviderResult<TimelineFeedGroup>(groups, totalCount);
    }

    protected internal Virtualize<TimelineFeedGroup>? TimelineVirtualize { get; set; }

    /// <summary>
    ///     モバイル表示時にフローティングボタンからアイテム追加ダイアログを表示する。
    /// </summary>
    protected internal async Task OpenAddItemDialogAsync()
    {
        var dialog = await DialogLauncher.ShowAddItemDialogAsync();
        var result = await dialog.Result;
        if (result is { Canceled: false })
        {
            if (TimelineVirtualize is not null)
            {
                await TimelineVirtualize.RefreshDataAsync();
            }
            await InvokeAsync(StateHasChanged);
        }
    }
}