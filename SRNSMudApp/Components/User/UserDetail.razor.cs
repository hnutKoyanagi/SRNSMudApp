namespace SRNSMudApp.Components.User;

using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

using SRNSMudApp.Components.Tag;
using SRNSMudApp.Services;

/// <summary>
///     ユーザー詳細画面コンポーネントのコードビハインド。
///     ユーザー基本情報の表示、フォロー/フォロー解除、タグ階層ツリー (jqTree) のレンダリング制御を担当する。
///     ドメイン操作・データ読み込みは <see cref="UserDetailViewModel"/> に委譲する。
/// </summary>
public partial class UserDetail : ComponentBase, IAsyncDisposable
{
    [Inject] private UserDetailViewModel ViewModel { get; set; } = null!;
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter] private Task<AuthenticationState> AuthStateTask { get; set; } = default!;

    [Parameter] public string UserId { get; set; } = string.Empty;

    private string? _lastLoadedUserId;
    private int _activeTabIndex;
    private DotNetObjectReference<UserDetail>? _dotNetRef;
    private bool _isTreeInitialized;

    private void OnTabChanged(int index)
    {
        _activeTabIndex = index;
    }

    protected override async Task OnInitializedAsync()
    {
        await InitializeViewModelAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (_lastLoadedUserId != UserId)
        {
            _isTreeInitialized = false;
            await InitializeViewModelAsync();
        }
    }

    private async Task InitializeViewModelAsync()
    {
        _lastLoadedUserId = UserId;
        var authState = await AuthStateTask;
        var isLoggedIn = authState.User.Identity?.IsAuthenticated ?? false;
        var currentUserId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        await ViewModel.InitializeAsync(UserId, currentUserId, isLoggedIn);
    }

    private async Task ToggleFollowAsync()
    {
        await ViewModel.ToggleFollowAsync();
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "jqTree の初期化失敗はページ表示を停止させないため握りつぶす")]
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!ViewModel.IsLoading && !_isTreeInitialized && ViewModel.User is not null && ViewModel.UserTags.Count > 0 && _activeTabIndex == 1)
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            var treeDataJson = GetSerializedTreeData();
            // isLoggedIn = false にしてドラッグ＆ドロップや追加を無効化（閲覧専用）
            try
            {
                await JSRuntime.InvokeVoidAsync("jqTreeInterop.init", "jqtree-container-user-detail", treeDataJson, _dotNetRef, false);
            }
            catch (Exception)
            {
            }

            _isTreeInitialized = true;
        }
    }

    private string GetSerializedTreeData()
    {
        // ルート判定・ツリー構築・JSON 化は TagTreeViewModel に一元化
        return TagTreeViewModel.SerializeTreeData(ViewModel.UserTags);
    }

    [JSInvokable]
    public void NavigateToTagDetail(int tagId)
    {
        NavigationManager.NavigateTo($"/TagDetail/{tagId}");
    }

    // jqTreeInterop が呼ばないように isLoggedIn = false で初期化しているが、念のため定義
    [JSInvokable]
    public Task OnTreeMove(int movedNodeId, int targetNodeId, string position)
    {
        return Task.CompletedTask;
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "非同期破棄時の jqTree 破棄エラーは握りつぶす")]
    public async ValueTask DisposeAsync()
    {
        _dotNetRef?.Dispose();
        try
        {
            if (_isTreeInitialized)
            {
                try
                {
                    await JSRuntime.InvokeVoidAsync("jqTreeInterop.destroy", "jqtree-container-user-detail");
                }
                catch (Exception)
                {
                }
            }
        }
        catch
        {
            // Ignore during teardown
        }

        GC.SuppressFinalize(this);
    }
}