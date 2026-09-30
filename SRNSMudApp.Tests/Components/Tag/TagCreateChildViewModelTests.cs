namespace SRNSMudApp.Tests.Components.Tag;

using SRNSMudApp.Components.Tag;

using Xunit;

public class TagCreateChildViewModelTests
{
    [Fact]
    public void InitialState_IsEmptyAndCannotSubmit()
    {
        var vm = new TagCreateChildViewModel();

        Assert.Equal(string.Empty, vm.Name);
        Assert.Equal(string.Empty, vm.Content);
        Assert.False(vm.CanSubmit);
        Assert.Null(vm.CreateResult());
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("ValidName", true)]
    [InlineData("  ValidName  ", true)]
    public void CanSubmit_ValidatesNameNonEmpty(string name, bool expectedCanSubmit)
    {
        var vm = new TagCreateChildViewModel { Name = name };

        Assert.Equal(expectedCanSubmit, vm.CanSubmit);
    }

    [Fact]
    public void CreateResult_TrimsNameAndContent()
    {
        var vm = new TagCreateChildViewModel
        {
            Name = "  ChildTag  ",
            Content = "  Some description  "
        };

        var result = vm.CreateResult();

        Assert.NotNull(result);
        Assert.Equal("ChildTag", result.Name);
        Assert.Equal("Some description", result.Content);
    }

    [Fact]
    public void CreateResult_WhenContentNull_DefaultsToEmptyString()
    {
        var vm = new TagCreateChildViewModel
        {
            Name = "TagOnly",
            Content = null!
        };

        var result = vm.CreateResult();

        Assert.NotNull(result);
        Assert.Equal("TagOnly", result.Name);
        Assert.Equal(string.Empty, result.Content);
    }

    [Fact]
    public void CreateResult_WhenNameInvalid_ReturnsNull()
    {
        var vm = new TagCreateChildViewModel
        {
            Name = "   ",
            Content = "Valid content"
        };

        var result = vm.CreateResult();

        Assert.Null(result);
    }
}