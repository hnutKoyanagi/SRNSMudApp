namespace SRNSMudApp.Components.Tag;

using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

using MudBlazor;

using SRNSMudApp.Data;
using SRNSMudApp.Models;

/// <summary>
///     タグ操作権限（RightAsset）リクエスト送信ダイアログコンポーネントのコードビハインド。
/// </summary>
public partial class RequestTagPermissionDialog : ComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [CascadingParameter]
    private Task<AuthenticationState>? AuthState { get; set; }

    [Inject]
    private RequestTagPermissionViewModel ViewModel { get; set; } = null!;

    [Parameter]
    public Tag RequestedTag { get; set; } = null!;

    [Parameter]
    public string? PresetTargetUserId { get; set; }

    [Parameter]
    public IReadOnlyList<RightAssetHolderSummary> AvailableHolders { get; set; } = [];

    private MudForm? _form;
    private bool _isValid = true;
    private string _currentUserId = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        if (AuthState is not null)
        {
            var auth = await AuthState;
            _currentUserId = auth.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        }

        await ViewModel.InitializeAsync(RequestedTag, PresetTargetUserId, AvailableHolders, _currentUserId);
    }

    private void Cancel()
    {
        MudDialog.Cancel();
    }

    private async Task SubmitAsync()
    {
        if (_form is not null)
        {
            await _form.ValidateAsync();
            if (!_isValid)
            {
                return;
            }
        }

        bool success = await ViewModel.SubmitAsync(RequestedTag.Id, _currentUserId);
        if (success)
        {
            MudDialog.Close(DialogResult.Ok(true));
        }
    }
}