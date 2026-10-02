namespace SRNSMudApp.Tests.Components.Diagram;

using System.Collections.Generic;
using System.Linq;

using SRNSMudApp.Components.Diagram;
using SRNSMudApp.Data;

using Xunit;

public class CreateEdgeViewModelTests
{
    private static List<Tag> CreateSampleTags() =>
    [
        new Tag { Id = 1, Name = "Alpha", OwnerId = "user1" },
        new Tag { Id = 2, Name = "Beta", OwnerId = "user1" },
        new Tag { Id = 3, Name = "Alphabet", OwnerId = "user1" },
        new Tag { Id = 4, Name = "Gamma", OwnerId = "user1" }
    ];

    [Fact]
    public void Initialize_SetsProperties()
    {
        var tags = CreateSampleTags();
        var vm = new CreateEdgeViewModel();

        vm.Initialize(tags, tags[0], tags[1]);

        Assert.Same(tags, vm.AvailableTags);
        Assert.Same(tags[0], vm.SourceTag);
        Assert.Same(tags[1], vm.TargetTag);
        Assert.True(vm.CanSubmit);
        Assert.Equal((1, 2), vm.SubmitResult);
    }

    [Fact]
    public void CanSubmit_WhenSourceOrTargetNull_ReturnsFalse()
    {
        var tags = CreateSampleTags();
        var vm = new CreateEdgeViewModel();
        vm.Initialize(tags, null, tags[1]);

        Assert.False(vm.CanSubmit);
        Assert.Null(vm.SubmitResult);

        vm.SourceTag = tags[0];
        vm.TargetTag = null;

        Assert.False(vm.CanSubmit);
        Assert.Null(vm.SubmitResult);
    }

    [Fact]
    public void CanSubmit_WhenSourceEqualsTarget_ReturnsFalse()
    {
        var tags = CreateSampleTags();
        var vm = new CreateEdgeViewModel();
        vm.Initialize(tags, tags[0], tags[0]);

        Assert.False(vm.CanSubmit);
        Assert.Null(vm.SubmitResult);
    }

    [Fact]
    public void SearchSourceTags_ExcludesTargetTagAndMatchesName()
    {
        var tags = CreateSampleTags();
        var vm = new CreateEdgeViewModel();
        vm.Initialize(tags, null, tags[0]); // Target は ID 1 (Alpha)

        // "Alph" で検索すると Alpha (ID 1) は除外され、Alphabet (ID 3) のみ返る
        var results = vm.SearchSourceTags("Alph").ToList();

        Assert.Single(results);
        Assert.Equal(3, results[0].Id);
        Assert.Equal("Alphabet", results[0].Name);
    }

    [Fact]
    public void SearchTargetTags_ExcludesSourceTagAndMatchesName()
    {
        var tags = CreateSampleTags();
        var vm = new CreateEdgeViewModel();
        vm.Initialize(tags, tags[2], null); // Source は ID 3 (Alphabet)

        // "Alph" で検索すると Alphabet (ID 3) は除外され、Alpha (ID 1) のみ返る
        var results = vm.SearchTargetTags("Alph").ToList();

        Assert.Single(results);
        Assert.Equal(1, results[0].Id);
        Assert.Equal("Alpha", results[0].Name);
    }

    [Fact]
    public void SearchTags_CapsAt20Results()
    {
        var manyTags = Enumerable.Range(1, 30)
            .Select(i => new Tag { Id = i, Name = $"Tag{i}", OwnerId = "user1" })
            .ToList();

        var vm = new CreateEdgeViewModel();
        vm.Initialize(manyTags, null, null);

        var results = vm.SearchSourceTags("Tag").ToList();

        Assert.Equal(20, results.Count);
    }
}