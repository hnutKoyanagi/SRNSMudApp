namespace SRNSMudApp.Tests.Components.Tag;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Moq;

using SRNSMudApp.Components.Tag;
using SRNSMudApp.Services;

using Xunit;

using TagEntity = SRNSMudApp.Data.Tag;

public class TagAutocompleteViewModelTests
{
    private readonly Mock<ITagSearchQueryService> _queryServiceMock = new();

    private TagAutocompleteViewModel CreateViewModel()
    {
        return new TagAutocompleteViewModel(_queryServiceMock.Object);
    }

    [Fact]
    public async Task SearchTagsAsync_WhenCustomSearchFuncProvided_UsesCustomFuncWithoutCallingService()
    {
        var vm = CreateViewModel();
        var customTag = new TagEntity { Id = 1, Name = "CustomTag", OwnerId = "u1" };

        var result = await vm.SearchTagsAsync(
            "custom",
            (q, ct) => Task.FromResult<IEnumerable<TagEntity>>([customTag]),
            CancellationToken.None);

        var list = Assert.Single(result);
        Assert.Equal("CustomTag", list.Name);
        _queryServiceMock.Verify(s => s.SearchTagsWithFallbackAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SearchTagsAsync_WhenCustomSearchFuncNull_CallsSearchTagsWithFallbackAsync()
    {
        var vm = CreateViewModel();
        var fallbackTag = new TagEntity { Id = 2, Name = "FallbackTag", OwnerId = "u1" };
        _queryServiceMock.Setup(s => s.SearchTagsWithFallbackAsync("fall", It.IsAny<CancellationToken>()))
            .ReturnsAsync([fallbackTag]);

        var result = await vm.SearchTagsAsync("fall", null, CancellationToken.None);

        var list = Assert.Single(result);
        Assert.Equal("FallbackTag", list.Name);
        _queryServiceMock.Verify(s => s.SearchTagsWithFallbackAsync("fall", It.IsAny<CancellationToken>()), Times.Once);
    }
}