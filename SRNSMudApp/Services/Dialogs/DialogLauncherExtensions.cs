using Microsoft.AspNetCore.Components;

using MudBlazor;

namespace SRNSMudApp.Services.Dialogs;

/// <summary>
///     <see cref="IDialogLauncher" /> の利便性拡張。既存の
///     <c>DialogService.ShowAsync&lt;T&gt;(title, parameters, options)</c> 呼び出しと同じ形で書けるようにする。
/// </summary>
public static class DialogLauncherExtensions
{
    public static Task<IDialogReference> ShowAsync<TDialog>(
        this IDialogLauncher launcher,
        string title,
        DialogParameters? parameters = null,
        DialogOptions? options = null)
        where TDialog : IComponent => launcher.ShowAsync(typeof(TDialog), title, parameters, options);

    public static Task<IDialogReference> ShowAsync<TDialog>(
        this IDialogLauncher launcher,
        string title,
        DialogOptions options)
        where TDialog : IComponent => launcher.ShowAsync(typeof(TDialog), title, null, options);

    /// <summary>
    ///     モバイル向けのアイテム追加ダイアログ（全画面）を表示する。
    /// </summary>
    /// <param name="launcher">ダイアログランチャー。</param>
    /// <returns>起動したダイアログへの参照。</returns>
    public static Task<IDialogReference> ShowAddItemDialogAsync(this IDialogLauncher launcher)
    {
        ArgumentNullException.ThrowIfNull(launcher);
        return launcher.ShowAsync<Components.Item.AddItemDialog>(
            string.Empty,
            new DialogOptions
            {
                FullScreen = true,
                CloseButton = false,
                CloseOnEscapeKey = true,
                MaxWidth = MaxWidth.False
            });
    }
}