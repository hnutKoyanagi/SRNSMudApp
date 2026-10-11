using SRNSMudApp.Components.UI;
using SRNSMudApp.Models;

namespace SRNSMudApp.Tests.Models;

public class DisplayHelpersTests
{
    [Theory]
    [InlineData("alice", "alice")]
    [InlineData("1234567", "1234567")]
    [InlineData("longusernamefoo", "longuse")]
    [InlineData("", "不明")]
    [InlineData(null, "不明")]
    public void GetShortOwnerName_TruncatesOrDefaultsCorrectly(string? input, string expected)
    {
        var actual = DisplayHelpers.GetShortOwnerName(input);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ViewModels_DelegateGetShortOwnerName_ToDisplayHelpers()
    {
        Assert.Equal(DisplayHelpers.GetShortOwnerName("testlongname"), TagCardViewModel.GetShortOwnerName("testlongname"));
        Assert.Equal(DisplayHelpers.GetShortOwnerName("testlongname"), ItemCardViewModel.GetShortOwnerName("testlongname"));
        Assert.Equal(DisplayHelpers.GetShortOwnerName(null), TagCardViewModel.GetShortOwnerName(null));
        Assert.Equal(DisplayHelpers.GetShortOwnerName(null), ItemCardViewModel.GetShortOwnerName(null));
    }
}