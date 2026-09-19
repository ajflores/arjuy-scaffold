using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using ArjuyScaffold.Models;
using ModelContextProtocol.Server;

namespace ArjuyScaffold.Tools;

/// <summary>
/// Generates Business-layer DTO pairs following the real arjuy* convention found in
/// ArjuyTurismo.Business: a single file Business/Models/{Entity}Models.cs holding
/// {Entity}ResponseModel and {Entity}RequestModel, both flat classes with auto-implemented
/// properties. Data Annotations are NOT used (prohibited by the arjuy* convention — validation
/// belongs elsewhere), and string properties default to string.Empty rather than being nullable.
/// </summary>
[McpServerToolType]
public static class DtoTool
{
    [McpServerTool(Name = "generate_dto")]
    [Description(
        "Generates the C# source for a Business-layer DTO pair: {Entity}ResponseModel and " +
        "{Entity}RequestModel, both flat classes with auto-implemented properties, following the " +
        "arjuy* convention (Business/Models/{Entity}Models.cs). No Data Annotations (prohibited " +
        "by convention). String properties default to '= string.Empty' rather than being " +
        "nullable. Returns the generated C# source (both classes in one string) — this tool does " +
        "not write files to disk.")]
    public static string GenerateDto(
        [Description("Entity name in PascalCase, e.g. 'Category'. Generates CategoryResponseModel/CategoryRequestModel.")] string entityName,
        [Description("Properties shared by both the response and request model (name + C# type each).")] List<PropertyDefinition> properties,
        [Description("Namespace for the generated classes. Defaults to a generated namespace if omitted.")] string? namespaceName = null)
    {
        if (string.IsNullOrWhiteSpace(entityName))
        {
            throw new ArgumentException("entityName is required.", nameof(entityName));
        }

        string resolvedNamespace = namespaceName ?? "ArjuyScaffold.Generated.Business.Models";
        List<PropertyDefinition> resolvedProperties = properties ?? new List<PropertyDefinition>();

        StringBuilder sourceBuilder = new StringBuilder();
        sourceBuilder.AppendLine("namespace " + resolvedNamespace + ";");
        sourceBuilder.AppendLine();

        AppendModel(sourceBuilder, entityName + "ResponseModel", resolvedProperties);
        sourceBuilder.AppendLine();
        AppendModel(sourceBuilder, entityName + "RequestModel", resolvedProperties);

        return sourceBuilder.ToString();
    }

    private static void AppendModel(StringBuilder sourceBuilder, string className, List<PropertyDefinition> properties)
    {
        sourceBuilder.AppendLine("public class " + className);
        sourceBuilder.AppendLine("{");

        foreach (PropertyDefinition property in properties)
        {
            string propertyType = string.IsNullOrWhiteSpace(property.Type) ? "string" : property.Type;
            string propertyName = string.IsNullOrWhiteSpace(property.Name) ? "PropertyName" : property.Name;

            if (propertyType.Trim() == "string")
            {
                sourceBuilder.AppendLine("    public " + propertyType + " " + propertyName + " { get; set; } = string.Empty;");
            }
            else
            {
                sourceBuilder.AppendLine("    public " + propertyType + " " + propertyName + " { get; set; }");
            }
        }

        sourceBuilder.AppendLine("}");
    }
}
