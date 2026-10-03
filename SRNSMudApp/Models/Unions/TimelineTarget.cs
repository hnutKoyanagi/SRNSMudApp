using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace SRNSMudApp.Models.Unions;

public record ItemTarget(int TargetItemId);
public record TagTarget(int TargetTagId);

[JsonConverter(typeof(TimelineTargetConverter))]
[SuppressMessage("Performance", "CA1815:Override equals and operator equals on value types", Justification = "Union type handled by C# compiler")]
public readonly union TimelineTarget(ItemTarget, TagTarget);