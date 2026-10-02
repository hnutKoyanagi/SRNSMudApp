namespace SRNSMudApp.Components.Tag;

using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

using MudBlazor;

using SRNSMudApp.Components.UI;
using SRNSMudApp.Data;
using SRNSMudApp.Services;
using SRNSMudApp.Services.Dialogs;

/// <summary>
///     タギングリクエスト一覧コンポーネントのコードビハインド。
///     未処理リクエストのテーブル表示、選択、承認/却下アクション、スレッドダイアログ表示を制御する。
///     リクエストの承認・却下ロジックは <see cref="TaggingRequestActionViewModel"/> に委譲する。
/// </summary>
public partial class TaggingRequestList : ComponentBase
{
    [Inject] private IDialogLauncher DialogLauncher { get; set; } = null!;
    [Inject] private TaggingRequestActionViewModel ViewModel { get; set; } = null!;

    [CascadingParameter] private Task<AuthenticationState> AuthenticationStateTask { get; set; } = default!;

    [Parameter] public IEnumerable<TaggingRequestEntity> Requests { get; set; } = [];
    [Parameter] public EventCallback OnRequestChanged { get; set; }
    [Parameter] public RenderFragment<TaggingRequestEntity>? RejectButtonTemplate { get; set; }
    [Parameter] public TaggingRequestEntity? SelectedRequest { get; set; }
    [Parameter] public EventCallback<TaggingRequestEntity?> SelectedRequestChanged { get; set; }

    private string? _currentUserId;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthenticationStateTask;
        _currentUserId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    private string SelectedRowClassFunc(TaggingRequestEntity element, int rowNumber)
    {
        return SelectedRequest is not null && SelectedRequest.Id == element.Id
            ? "mud-table-row-selected"
            : string.Empty;
    }

    private async Task OnRowClicked(TableRowClickEventArgs<TaggingRequestEntity> args)
    {
        SelectedRequest = args.Item;
        await SelectedRequestChanged.InvokeAsync(args.Item);
    }

    private bool CanApprove(TaggingRequestEntity request)
    {
        return ViewModel.CanApprove(request, _currentUserId ?? "");
    }

    private async Task ApproveRequestAsync(TaggingRequestEntity request)
    {
        if (_currentUserId is null)
        {
            return;
        }

        if (await ViewModel.ApproveRequestAsync(request.Id, _currentUserId))
        {
            await OnRequestChanged.InvokeAsync();
        }
    }

    private async Task RejectRequestAsync(TaggingRequestEntity request)
    {
        if (_currentUserId is null)
        {
            return;
        }

        if (await ViewModel.RejectRequestAsync(request.Id, _currentUserId))
        {
            await OnRequestChanged.InvokeAsync();
        }
    }

    public async Task OpenThreadDialog(TaggingRequestEntity request)
    {
        var parameters = new DialogParameters<TaggingRequestThreadDialog>
        {
            { x => x.TaggingRequest, request }
        };
        var options = new DialogOptions { CloseOnEscapeKey = true, MaxWidth = MaxWidth.Medium, FullWidth = true };
        var dialog = await DialogLauncher.ShowAsync<TaggingRequestThreadDialog>("リクエストスレッド", parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            await OnRequestChanged.InvokeAsync();
        }
    }
}