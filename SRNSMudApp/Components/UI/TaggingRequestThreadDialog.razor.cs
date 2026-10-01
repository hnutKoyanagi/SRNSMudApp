namespace SRNSMudApp.Components.UI;

using System.Security.Claims;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

using MudBlazor;

using SRNSMudApp.Data;

/// <summary>
///     タグ付けリクエストのスレッド表示および承認・却下・返信入力ダイアログコンポーネントのコードビハインド。
/// </summary>
public partial class TaggingRequestThreadDialog : ComponentBase
{
    [CascadingParameter]
    public IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public TaggingRequestEntity TaggingRequest { get; set; } = null!;

    [CascadingParameter]
    private Task<AuthenticationState> AuthenticationStateTask { get; set; } = default!;

    [Inject]
    private TaggingRequestThreadViewModel ViewModel { get; set; } = null!;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthenticationStateTask;
        var user = authState.User;
        string? currentUserId = null;
        if (user.Identity?.IsAuthenticated == true)
        {
            currentUserId = user.FindFirst(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
        }

        await ViewModel.InitializeAsync(TaggingRequest, currentUserId);
    }

    private async Task SubmitReplyAsync()
    {
        await ViewModel.SubmitReplyAsync();
    }

    private async Task ApproveRequestAsync()
    {
        if (await ViewModel.ApproveRequestAsync())
        {
            MudDialog.Close(DialogResult.Ok(true));
        }
    }

    private async Task RejectRequestAsync()
    {
        if (await ViewModel.RejectRequestAsync())
        {
            MudDialog.Close(DialogResult.Ok(true));
        }
    }
}