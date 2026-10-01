namespace SRNSMudApp.Components.PublicOffer;

using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

using MudBlazor;

using SRNSMudApp.Data;
using SRNSMudApp.Models.Unions;

/// <summary>
///     パブリックオファー新規作成ダイアログコンポーネントのコードビハインド。
///     提供タグの検索・選択、要求アセット量の指定、オファー公開処理を制御する。
/// </summary>
public partial class CreatePublicOfferDialog : ComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    [CascadingParameter]
    private Task<AuthenticationState> AuthStateTask { get; set; } = default!;

    [Inject]
    private CreatePublicOfferViewModel ViewModel { get; set; } = null!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = null!;

    private MudForm? _form;
    private bool _isValid;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateTask;
        ViewModel.CurrentUserId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
    }

    private async Task<IEnumerable<Tag>> SearchMyTags(string? value, CancellationToken token)
    {
        return await ViewModel.SearchMyTagsAsync(value, token);
    }

    private async Task Submit()
    {
        if (_form != null)
        {
            await _form.ValidateAsync();
        }

        if (!_isValid || ViewModel.SelectedTag is null)
        {
            return;
        }

        var result = await ViewModel.SubmitAsync();
        switch (result)
        {
            case Success<bool>:
                Snackbar.Add("公開オファーを作成しました。", Severity.Success);
                MudDialog.Close(DialogResult.Ok(true));
                break;
            case Failure failure:
                Snackbar.Add(failure.ErrorMessage, Severity.Error);
                break;
            default:
                break;
        }
    }

    private void Cancel()
    {
        MudDialog.Cancel();
    }
}