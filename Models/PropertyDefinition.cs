using System.ComponentModel;

namespace ArjuyScaffold.Models;

/// <summary>
/// Describes a single property to generate on a scaffolded entity.
/// </summary>
public sealed class PropertyDefinition
{
    [Description("Property name in PascalCase, e.g. 'Title'.")]
    public string Name { get; set; } = string.Empty;

    [Description("C# type name for the property, e.g. 'string', 'int', 'Guid', 'decimal?'.")]
    public string Type { get; set; } = string.Empty;
}
