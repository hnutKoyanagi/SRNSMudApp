namespace SRNSMudApp.Tests.Components.UI;

using SRNSMudApp.Components.UI;

using Xunit;

public class RejectRequestViewModelTests
{
    [Fact]
    public void DefaultConstructor_HasNullCommentAndEmptyNormalized()
    {
        var vm = new RejectRequestViewModel();
        Assert.Null(vm.Comment);
        Assert.Equal(string.Empty, vm.NormalizedComment);
        Assert.Equal(string.Empty, vm.SubmitResult);
    }

    [Fact]
    public void ParameterizedConstructor_SetsComment()
    {
        var vm = new RejectRequestViewModel("Not aligned with guidelines");
        Assert.Equal("Not aligned with guidelines", vm.Comment);
        Assert.Equal("Not aligned with guidelines", vm.SubmitResult);
    }

    [Theory]
    [InlineData("  Some reason  ", "Some reason")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData(null, "")]
    public void NormalizedComment_TrimsOrDefaultsToEmpty(string? input, string expected)
    {
        var vm = new RejectRequestViewModel(input);
        Assert.Equal(expected, vm.NormalizedComment);
        Assert.Equal(expected, vm.SubmitResult);
    }
}