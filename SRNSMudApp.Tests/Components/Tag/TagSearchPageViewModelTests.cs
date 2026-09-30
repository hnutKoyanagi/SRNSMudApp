#region

using SRNSMudApp.Components.Tag;
using SRNSMudApp.Data;

using Xunit;

using TagEntity = SRNSMudApp.Data.Tag;

#endregion

namespace SRNSMudApp.Tests.Components.Tag;

public class TagSearchPageViewModelTests
{
    [Fact]
    public void InitialState_SelectedTagIsNull_HasSelectedTagIsFalse()
    {
        var sut = new TagSearchPageViewModel();

        Assert.Null(sut.SelectedTag);
        Assert.False(sut.HasSelectedTag);
        Assert.Equal(string.Empty, sut.SelectedTagSummary);
    }

    [Fact]
    public void WhenSelectedTagIsSet_HasSelectedTagIsTrue_AndReturnsCorrectSummary()
    {
        var sut = new TagSearchPageViewModel
        {
            SelectedTag = new TagEntity
            {
                Id = 1,
                Name = "AI",
                CachedWeight = 42,
                OwnerId = "user1"
            }
        };

        Assert.True(sut.HasSelectedTag);
        Assert.Equal("選択されたタグ: 名前 = AI, ウェイト = 42", sut.SelectedTagSummary);
    }
}