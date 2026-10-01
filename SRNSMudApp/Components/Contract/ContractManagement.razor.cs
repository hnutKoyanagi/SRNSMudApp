namespace SRNSMudApp.Components.Contract;

using System.Security.Claims;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

using MudBlazor;

using SRNSMudApp.Data;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;

/// <summary>
///     コントラクト管理画面コンポーネントのコードビハインド。
///     受信・送信済みコントラクト提案のタブ表示、承認・拒否・取り下げ操作および通知トースト表示を制御する。
///     データ取得・更新処理は <see cref="ContractManagementViewModel"/> に委譲する。
/// </summary>
public partial class ContractManagement : ComponentBase
{
    [Inject] private ContractManagementViewModel ViewModel { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    [CascadingParameter] private Task<AuthenticationState> AuthStateTask { get; set; } = default!;

    private string _currentUserId = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateTask;
        _currentUserId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

        await ViewModel.LoadDataAsync(_currentUserId);
    }

    private async Task AcceptContract(TaggingRequestEntity contract)
    {
        var result = await ViewModel.AcceptContractAsync(contract.Id, _currentUserId);
        switch (result)
        {
            case Success<string>:
                Snackbar.Add("コントラクトを承認しました。", Severity.Success);
                break;
            case Failure f:
                Snackbar.Add($"エラーが発生しました: {f.ErrorMessage}", Severity.Error);
                break;
            default:
                break;
        }
    }

    private async Task RejectContract(TaggingRequestEntity contract)
    {
        var result = await ViewModel.RejectContractAsync(contract.Id, _currentUserId);
        switch (result)
        {
            case Success<string>:
                Snackbar.Add("コントラクトを拒否しました。", Severity.Success);
                break;
            case Failure f:
                Snackbar.Add($"エラーが発生しました: {f.ErrorMessage}", Severity.Error);
                break;
            default:
                break;
        }
    }

    private async Task CancelContract(TaggingRequestEntity contract)
    {
        var result = await ViewModel.CancelContractAsync(contract.Id, _currentUserId);
        switch (result)
        {
            case Success<string>:
                Snackbar.Add("コントラクトを取り下げました。", Severity.Success);
                break;
            case Failure f:
                Snackbar.Add($"エラーが発生しました: {f.ErrorMessage}", Severity.Error);
                break;
            default:
                break;
        }
    }
}