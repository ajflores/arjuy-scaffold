using System.ComponentModel;

namespace ArjuyScaffold.Models;

/// <summary>
/// Describes a single navigation-property relationship for an EF Core
/// IEntityTypeConfiguration&lt;T&gt;, following the real arjuy* pattern: HasMany/HasOne +
/// WithOne/WithMany + optional HasForeignKey + OnDelete(DeleteBehavior.X).
/// </summary>
public sealed class EfRelationDefinition
{
    [Description("Navigation property name on THIS entity, e.g. 'Products' for a HasMany, or 'Category' for a HasOne.")]
    public string PropertyName { get; set; } = string.Empty;

    [Description("Relation kind from this entity's side: 'HasMany' or 'HasOne'.")]
    public string RelationType { get; set; } = "HasOne";

    [Description("Related entity type name, e.g. 'Product'. Used only to name the inverse lambda's target type in a comment-free way — the actual C# type is inferred by the compiler from the navigation property.")]
    public string TargetEntity { get; set; } = string.Empty;

    [Description("Inverse navigation property name on the target entity, e.g. 'Category' (paired with a HasMany here) or 'Products' (paired with a HasOne here). Omit for WithMany()/WithOne() with no argument (no inverse navigation).")]
    public string? InverseNavigationProperty { get; set; }

    [Description("Foreign key property name, e.g. 'CategoryId'. Omit to let EF Core infer the convention-based FK instead of emitting .HasForeignKey(...).")]
    public string? ForeignKeyProperty { get; set; }

    [Description("Delete behavior: 'Restrict', 'Cascade' or 'SetNull'. Defaults to 'Restrict'.")]
    public string DeleteBehavior { get; set; } = "Restrict";
}
