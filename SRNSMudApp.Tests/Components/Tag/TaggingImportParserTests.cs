using System.Text.Json;

using SRNSMudApp.Models;

namespace SRNSMudApp.Tests.Components.Tag;

public sealed class TaggingImportParserTests
{
    private const string SampleJson = """
    {
      "TaggingRequestEntity": {
        "Item": [
          {
            "ItemId": "item-001",
            "SequenceOrder": 1,
            "Content": "全ての[人格](https://www.google.com/search?q=rel-001)が[自由](https://www.google.com/search?q=rel-002)を持ち、[独り](https://www.google.com/search?q=rel-003)の[宇宙](https://www.google.com/search?q=rel-004)に[引きこもる](https://www.google.com/search?q=rel-005)ことも、[多面的](https://www.google.com/search?q=rel-006)な[評価](https://www.google.com/search?q=rel-007)で[自己](https://www.google.com/search?q=rel-008)[承認](https://www.google.com/search?q=rel-009)を満たすこともできる。"
          }
        ],
        "TagRelations": [
          {
            "RelationId": "rel-ndc-140",
            "SourceItemId": "item-001",
            "Tag": {
              "HierarchyId": "140",
              "Name": "心理学",
              "TagKind": "SystemClassificationTag"
            }
          },
          {
            "RelationId": "rel-001",
            "SourceItemId": "item-001",
            "Tag": {
              "HierarchyId": "custom-001",
              "Name": "人格",
              "TagKind": "UserCustomTag"
            }
          }
        ],
        "TagEdges": [
          {
            "EdgeId": "edge-001",
            "SourceTagId": "rel-001",
            "TargetTagId": "rel-002",
            "AppliedTags": [
              "持つ"
            ]
          }
        ]
      }
    }
    """;

    [Fact]
    public void Deserialize_SampleJson_SuccessfullyParsesAllEntities()
    {
        // Act
        TaggingImportRoot? root = JsonSerializer.Deserialize<TaggingImportRoot>(SampleJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Assert
        Assert.NotNull(root);
        Assert.NotNull(root.TaggingRequestEntity);

        TaggingImportPayload payload = root.TaggingRequestEntity;
        Assert.Single(payload.Item);
        Assert.Equal("item-001", payload.Item[0].ItemId);
        Assert.Contains("[人格]", payload.Item[0].Content);

        Assert.Equal(2, payload.TagRelations.Count);
        Assert.Equal("rel-ndc-140", payload.TagRelations[0].RelationId);
        Assert.Equal("心理学", payload.TagRelations[0].Tag.Name);
        Assert.Equal("SystemClassificationTag", payload.TagRelations[0].Tag.TagKind);

        Assert.Single(payload.TagEdges);
        Assert.Equal("edge-001", payload.TagEdges[0].EdgeId);
        Assert.Equal("rel-001", payload.TagEdges[0].SourceTagId);
        Assert.Equal("rel-002", payload.TagEdges[0].TargetTagId);
        Assert.Single(payload.TagEdges[0].AppliedTags);
        Assert.Equal("持つ", payload.TagEdges[0].AppliedTags[0]);
    }
}