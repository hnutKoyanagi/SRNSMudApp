using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

using SRNSMudApp.Components.UI;

using Xunit;

namespace SRNSMudApp.Tests.Components.UI;

public class ReactionBarViewModelTests
{
    private sealed class FakeNavManager : NavigationManager
    {
        public FakeNavManager()
        {
            Initialize("http://localhost/", "http://localhost/");
        }
    }

    [Theory]
    [InlineData("Enter", true)]
    [InlineData(" ", true)]
    [InlineData("Escape", false)]
    [InlineData("a", false)]
    [InlineData("Tab", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ShouldTriggerAction_EvaluatesKeysCorrectly(string? key, bool expected)
    {
        var e = key == null ? null : new KeyboardEventArgs { Key = key };
        var actual = ReactionBarViewModel.ShouldTriggerAction(e);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void BuildItemDetailUri_WhenNavigationManagerNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ReactionBarViewModel.BuildItemDetailUri(1, "真実", null!));
    }

    [Fact]
    public void BuildItemDetailUri_ConstructsCorrectUriWithParameters()
    {
        var nav = new FakeNavManager();
        var uri = ReactionBarViewModel.BuildItemDetailUri(42, "真実", nav);

        Assert.StartsWith("/ItemDetail/42", uri, StringComparison.Ordinal);
        Assert.Contains("tab=tags", uri, StringComparison.Ordinal);
        Assert.Contains("真実", Uri.UnescapeDataString(uri), StringComparison.Ordinal);
    }
}