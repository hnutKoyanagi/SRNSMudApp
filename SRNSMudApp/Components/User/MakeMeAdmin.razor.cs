namespace SRNSMudApp.Components.User;

using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.JSInterop;

/// <summary>
///     管理者権限切り替え（開発用デバッグ）ページコンポーネントのコードビハインド。
///     現在のユーザーロール状態の取得および切り替えフォームの送信を制御する。
/// </summary>
public partial class MakeMeAdmin : ComponentBase
{
    [CascadingParameter]
    private Task<AuthenticationState> AuthStateTask { get; set; } = default!;

    [Inject]
    private IWebHostEnvironment Env { get; set; } = null!;

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    [Inject]
    private MakeMeAdminViewModel ViewModel { get; set; } = null!;

    protected override async Task OnInitializedAsync()
    {
        var auth = await AuthStateTask;
        ViewModel.Initialize(auth.User);
    }

    private async Task OnToggleAdminChanged(bool newValue)
    {
        await ViewModel.ToggleAdminAsync(newValue, JS);
    }
}