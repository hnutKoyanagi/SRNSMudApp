using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

using Microsoft.JSInterop;

using SRNSMudApp.Components.Item;
using SRNSMudApp.Data;
using SRNSMudApp.Services;

// 兄弟名前空間 SRNSMudApp.Components.Tag / .Item が同名型と解決されるため、
// エイリアスを名前空間の内側に置く

// IDE0010: union 型・enum の網羅的 switch に対する「Populate switch」は、
// 全ケース列挙済み・default 併記済みでも解消されない解析器の誤検知のため抑制する。
#pragma warning disable IDE0010

namespace SRNSMudApp.Components.UI;

/// <summary>
///     ResourceList のコードビハインド。
///     マークアップ (.razor) 側は表示のみを担い、システムタグ解決・フォーカス管理・
///     URL クエリ同期などの UI オーケストレーションはこちらに集約する。
/// </summary>
public partial class ResourceList : IAsyncDisposable
{
    [CascadingParameter] private Task<AuthenticationState> AuthState { get; set; } = default!;

    [Parameter] public IEnumerable<Data.Item> Items { get; set; } = [];
    [Parameter] public IEnumerable<Data.Tag> Tags { get; set; } = [];
    [Parameter] public EventCallback OnDataChanged { get; set; }
    [Parameter] public bool EnableUrlUpdate { get; set; } = true;
    [Parameter] public bool EnableItemNavigation { get; set; } = true;

    [Inject] private ResourceListViewModel ViewModel { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;

    private string _currentUserId = "";

    // ===== フォーカスステート =====
    private int? _focusTagId;
    private int? _focusItemId;
    private bool _hasScrolledToFocus;

    protected override async Task OnInitializedAsync()
    {
        // URL 形式の知識は ItemListQueryState に一元化
        var state = ItemListQueryState.ParseFromUri(new Uri(NavigationManager.Uri));
        _focusTagId = state.FocusTagId;
        _focusItemId = state.FocusItemId;

        AuthenticationState authState = await AuthState;
        _currentUserId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
        await ViewModel.InitializeAsync(_currentUserId);
    }

    private async Task NotifyChangedAsync()
    {
        await ViewModel.FetchTagsAsync();
        if (OnDataChanged.HasDelegate)
        {
            await OnDataChanged.InvokeAsync();
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_hasScrolledToFocus && (Items.Any() || Tags.Any()))
        {
            _hasScrolledToFocus = true;
            await ScrollToFocusTargetAsync();
        }
    }

    /// <summary>focusTag を優先してスクロールする。タグ未指定の場合は focusItem へスクロールする。</summary>
    private async Task ScrollToFocusTargetAsync()
    {
        var selector = ResourceListViewModel.GetFocusSelector(_focusTagId, _focusItemId);
        if (selector is not null)
        {
            await TryScrollAsync(selector);
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "フォーカス先へのスクロール失敗はページ表示への影響を避けるため無視する")]
    [SuppressMessage("Roslynator", "RCS1075:Avoid empty catch clause that catches System.Exception",
        Justification = "スクロール失敗時は何もしない")]
    private async Task TryScrollAsync(string selector)
    {
        try
        {
            await JS.InvokeVoidAsync("contentOverflowHelper.scrollToElement", selector);
        }
        catch (Exception)
        {
            // ignored
        }
    }

    private void SetFocusTag(int tagId)
    {
        if (_focusTagId == tagId)
        {
            return;
        }
        _focusTagId = tagId;
        _focusItemId = null;
        UpdateFocusUrl();
    }

    private void SetFocusItem(int itemId)
    {
        if (_focusItemId == itemId)
        {
            return;
        }
        _focusItemId = itemId;
        _focusTagId = null;
        UpdateFocusUrl();
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "URL更新失敗はページ表示への影響を避けるため無視する")]
    [SuppressMessage("Roslynator", "RCS1075:Avoid empty catch clause that catches System.Exception",
        Justification = "URL更新失敗時は何もしない")]
    private async void UpdateFocusUrl()
    {
        if (!EnableUrlUpdate)
        {
            return;
        }

        try
        {
            // 現在の URL から状態を読み取り、focus 系パラメータのみ差し替える
            ItemListQueryState updated = ItemListQueryState.ParseFromUri(new Uri(NavigationManager.Uri)) with
            {
                FocusTagId = _focusTagId,
                FocusItemId = _focusItemId
            };
            var newUri = NavigationManager.GetUriWithQueryParameters(updated.BuildParameters());
            await JS.InvokeVoidAsync("contentOverflowHelper.updateUrl", newUri);
        }
        catch (Exception)
        {
            // ignored
        }
    }

    public async Task EnsureSystemTagsExistAsync()
    {
        await ViewModel.EnsureSystemTagsExistAsync();
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}