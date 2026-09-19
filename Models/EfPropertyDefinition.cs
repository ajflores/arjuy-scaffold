using System.ComponentModel;

namespace ArjuyScaffold.Models;

/// <summary>
/// Describes a single scalar property configuration for an EF Core IEntityTypeConfiguration&lt;T&gt;
/// (Fluent API constraints only — the property itself is assumed to already exist on the entity,
/// generated separately by generate_entity).
/// </summary>
public sealed class EfPropertyDefinition
{
    [Description("Property name, matching the entity property exactly, e.g. 'Name'.")]
    public string Name { get; set; } = string.Empty;

    [Description("Whether to emit .IsRequired() for this property. Defaults to true.")]
    public bool IsRequired { get; set; } = true;

    [Description("Optional .HasMaxLength(N) for string properties. Omit (null) for no length constraint.")]
    public int? MaxLength { get; set; }
}
