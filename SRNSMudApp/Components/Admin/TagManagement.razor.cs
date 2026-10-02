namespace SRNSMudApp.Components.Admin;

using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;

/// <summary>
///     管理者向けタグ管理画面コンポーネントのコードビハインド。
///     階層ロック設定の変更・保存、タグ一覧の検索・ページネーション、個別ロック切り替えを制御する。
///     階層計算・ロック更新ロジックは <see cref="TagManagementViewModel"/> に委譲する。
/// </summary>
public partial class TagManagement : ComponentBase
{
    [Inject] private TagManagementViewModel ViewModel { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    protected override async Task OnInitializedAsync()
    {
        await ViewModel.LoadDataAsync();
    }

    private async Task SaveHierarchyLockSettingAsync()
    {
        var result = await ViewModel.SaveHierarchyLockSettingAsync();
        switch (result)
        {
            case Success<int> success:
                Snackbar.Add($"階層ロック設定を {success.Value} 階層目までに更新しました。", Severity.Success);
                break;
            case Failure failure:
                Snackbar.Add(failure.ErrorMessage, Severity.Error);
                break;
            default:
                break;
        }
    }

    private async Task ToggleTagLockAsync(int tagId)
    {
        var result = await ViewModel.ToggleTagLockAsync(tagId);
        switch (result)
        {
            case Success<bool> success:
                Snackbar.Add(success.Value ? "タグを個別ロックしました。" : "タグの個別ロックを解除しました。", Severity.Success);
                break;
            case Failure failure:
                Snackbar.Add(failure.ErrorMessage, Severity.Error);
                break;
            default:
                break;
        }
    }
}