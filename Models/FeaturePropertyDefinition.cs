using System.ComponentModel;

namespace ArjuyScaffold.Models;

/// <summary>
/// Describes a single property for generate_feature: the composite tool needs both the plain
/// (name, type) shape used by generate_entity/generate_dto and the EF-specific
/// (IsRequired, MaxLength) shape used by generate_ef_configuration, so it carries both instead of
/// forcing the caller to repeat the same property list twice with two different shapes.
/// </summary>
public sealed class FeaturePropertyDefinition
{
    [Description("Property name in PascalCase, e.g. 'Name'.")]
    public string Name { get; set; } = string.Empty;

    [Description("C# type name, e.g. 'string', 'int', 'decimal?'.")]
    public string Type { get; set; } = string.Empty;

    [Description("EF Core only: whether to emit .IsRequired() in the generated configuration. Defaults to true. Ignored by the entity/DTO generation, which only cares about Name and Type.")]
    public bool IsRequired { get; set; } = true;

    [Description("EF Core only: optional .HasMaxLength(N) in the generated configuration, for string properties. Ignored by the entity/DTO generation.")]
    public int? MaxLength { get; set; }
}
