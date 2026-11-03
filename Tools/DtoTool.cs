using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using ArjuyScaffold.Models;
using ArjuyScaffold.Support;
using ModelContextProtocol.Server;

namespace ArjuyScaffold.Tools;

/// <summary>
/// Generates Business-layer DTO pairs following the real arjuy* convention found in
/// ArjuyTurismo.Business, but ONE FILE PER TYPE: Business/Models/{Plural}/{Entity}ResponseModel.cs and
/// Business/Models/{Plural}/{Entity}RequestModel.cs, both flat classes with auto-implemented
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
        "arjuy* convention. No Data Annotations (prohibited by convention). String properties " +
        "default to '= string.Empty' rather than being nullable. The ResponseModel also carries 'int Id' (the RequestModel does not). Returns a list of {path, content} " +
        "files — ONE FILE PER MODEL (Business/Models/{Plural}/{Entity}ResponseModel.cs and " +
        "Business/Models/{Plural}/{Entity}RequestModel.cs, e.g. Models/Categories/; the namespace follows the folder: {root}.Business.Models.{Plural}; shared models go in Models/Common/). Writes to disk only when 'outputDirectoryPath' " +
        "is provided (opt-in).")]
    public static List<GeneratedFile> GenerateDto(
        [Description("Entity name in PascalCase, e.g. 'Category'. Generates CategoryResponseModel/CategoryRequestModel.")] string entityName,
        [Description("Properties shared by both the response and request model (name + C# type each).")] List<PropertyDefinition> properties,
        [Description("Namespace for the generated classes; must follow the folder: {root}.Business.Models.{PluralEntity} (e.g. MyApp.Business.Models.Categories). Defaults to ArjuyScaffold.Generated.Business.Models.{PluralEntity}.")] string? namespaceName = null,
        [Description("Optional root namespace/project prefix (e.g. 'MyApp') used for the relative file paths of the result. Defaults to 'Generated'.")] string? namespaceRoot = null,
        [Description("Optional absolute path to the SOLUTION root. When provided, the returned files are also written under it. Opt-in, disabled by default.")] string? outputDirectoryPath = null)
    {
        if (string.IsNullOrWhiteSpace(entityName))
        {
            throw new ArgumentException("entityName is required.", nameof(entityName));
        }

        string resolvedNamespace = namespaceName ?? ("ArjuyScaffold.Generated.Business.Models." + FeaturePaths.ModelFolder(entityName));
        List<PropertyDefinition> resolvedProperties = properties ?? new List<PropertyDefinition>();

        string responseName = entityName + "ResponseModel";
        string requestName = entityName + "RequestModel";

        List<GeneratedFile> files = new List<GeneratedFile>
        {
            new GeneratedFile(FeaturePaths.Model(namespaceRoot, entityName, responseName), BuildModelSource(resolvedNamespace, responseName, resolvedProperties, includeId: true)),
            new GeneratedFile(FeaturePaths.Model(namespaceRoot, entityName, requestName), BuildModelSource(resolvedNamespace, requestName, resolvedProperties, includeId: false)),
        };

        GeneratedFileWriter.WriteAll(outputDirectoryPath, files);
        return files;
    }

    private static string BuildModelSource(string namespaceName, string className, List<PropertyDefinition> properties, bool includeId)
    {
        StringBuilder sourceBuilder = new StringBuilder();
        sourceBuilder.AppendLine("namespace " + namespaceName + ";");
        sourceBuilder.AppendLine();
        AppendModel(sourceBuilder, className, properties, includeId);
        return sourceBuilder.ToString();
    }

    private static void AppendModel(StringBuilder sourceBuilder, string className, List<PropertyDefinition> properties, bool includeId)
    {
        sourceBuilder.AppendLine("public class " + className);
        sourceBuilder.AppendLine("{");

        if (includeId)
        {
            sourceBuilder.AppendLine("    public int Id { get; set; }");
        }

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
