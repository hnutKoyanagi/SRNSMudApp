namespace SRNSMudApp.Components.UI;

/// <summary>
///     タグの重み編集ダイアログ (WeightEditDialog) の入力状態およびバリデーションを担う ViewModel。
/// </summary>
public sealed class WeightEditViewModel
{
    public WeightEditViewModel()
    {
    }

    public WeightEditViewModel(int initialWeight)
    {
        Weight = initialWeight;
    }

    public int Weight { get; set; }

    /// <summary>
    ///     重みが確定可能（正または非負）かどうかを判定する。
    /// </summary>
    public bool CanSubmit => Weight >= 0;

    /// <summary>
    ///     確定結果の重み。
    /// </summary>
    public int SubmitResult => Weight;
}