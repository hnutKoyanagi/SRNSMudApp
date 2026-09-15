namespace SRNSMudApp.E2ETests;

using System.Threading.Tasks;

using Microsoft.Playwright;

/// <summary>
///     Playwright で IME（日本語入力）のコンポジション挙動をシミュレートするテストヘルパー。
///     DOM イベント（compositionstart, compositionupdate, compositionend）の順序制御をカプセル化する。
/// </summary>
public static class ImeTestHelpers
{
    /// <summary>
    ///     指定したセレクターの contenteditable 要素に対して、
    ///     IME コンポジション（未確定文字列の入力から確定まで）のイベント遷移をシミュレートする。
    /// </summary>
    /// <param name="page">Playwright Page インスタンス</param>
    /// <param name="elementSelector">対象要素のセレクター</param>
    /// <param name="composingText">未確定中に入力される仮テキスト（例: "t"）</param>
    /// <param name="finalText">確定後の最終テキスト（例: "た"）</param>
    /// <param name="compositionIntervalMs">未確定中の待機ミリ秒（デフォルト: 300ms）</param>
    public static async Task SimulateCompositionAsync(
        IPage page,
        string elementSelector,
        string composingText,
        string finalText,
        int compositionIntervalMs = 300)
    {
        await page.EvaluateAsync(@"async ({ selector, composing, final, interval }) => {
            const el = document.querySelector(selector);
            if (!el) throw new Error(`Element not found: ${selector}`);

            const delay = (ms) => new Promise(resolve => setTimeout(resolve, ms));

            // compositionstart: IME入力開始
            el.dispatchEvent(new CompositionEvent('compositionstart', { bubbles: true, cancelable: true, data: '' }));
            await delay(50);

            // 未確定状態（例: 「t」の入力）
            el.dispatchEvent(new InputEvent('input', { bubbles: true, inputType: 'insertCompositionText', data: composing, isComposing: true }));
            const composingNode = document.createTextNode(composing);
            el.appendChild(composingNode);
            el.dispatchEvent(new CompositionEvent('compositionupdate', { bubbles: true, data: composing }));
            
            await delay(interval);

            // 変換中（例: 「ta」->「た」）
            composingNode.textContent = final;
            el.dispatchEvent(new InputEvent('input', { bubbles: true, inputType: 'insertCompositionText', data: final, isComposing: true }));
            el.dispatchEvent(new CompositionEvent('compositionupdate', { bubbles: true, data: final }));
            await delay(50);

            // compositionend: IME確定
            el.dispatchEvent(new CompositionEvent('compositionend', { bubbles: true, data: final }));
            el.dispatchEvent(new InputEvent('input', { bubbles: true, inputType: 'insertText', data: final, isComposing: false }));
        }", new { selector = elementSelector, composing = composingText, final = finalText, interval = compositionIntervalMs });
    }
}