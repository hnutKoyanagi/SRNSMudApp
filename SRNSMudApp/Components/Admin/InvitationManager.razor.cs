namespace SRNSMudApp.Components.Admin;

using System;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

using MudBlazor;

using SRNSMudApp.Data;
using SRNSMudApp.Models.Unions;

/// <summary>
///     管理者向け招待管理ページコンポーネントのコードビハインド。
///     新規招待リンクの生成、招待コードのコピー、有効期限判定および招待の削除を制御する。
/// </summary>
public partial class InvitationManager : ComponentBase
{
    [CascadingParameter]
    private Task<AuthenticationState> AuthStateTask { get; set; } = default!;

    [Inject]
    private InvitationManagerViewModel ViewModel { get; set; } = null!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = null!;

    protected override async Task OnInitializedAsync()
    {
        if (AuthStateTask is not null)
        {
            var authState = await AuthStateTask;
            ViewModel.Initialize(authState.User);
        }

        await ViewModel.LoadInvitationsAsync();
    }

    private async Task CreateInvitationAsync()
    {
        var result = await ViewModel.CreateInvitationAsync();
        switch (result)
        {
            case Success<Invitation>:
                Snackbar.Add("Invitation created successfully.", Severity.Success);
                break;
            case Failure failure:
                Snackbar.Add(failure.ErrorMessage, Severity.Error);
                break;
            default:
                break;
        }
    }

    private async Task DeleteInvitationAsync(Invitation invitation)
    {
        var result = await ViewModel.DeleteInvitationAsync(invitation);
        switch (result)
        {
            case Success<bool>:
                Snackbar.Add("Invitation deleted.", Severity.Info);
                break;
            case Failure failure:
                Snackbar.Add(failure.ErrorMessage, Severity.Error);
                break;
            default:
                break;
        }
    }

    private void CopyLink(string code)
    {
        Snackbar.Add($"Link to copy: {InvitationManagerViewModel.BuildInviteLink(code)}", Severity.Info);
    }
}