using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Text;
using ArjuyScaffold.Models;
using ArjuyScaffold.Support;
using ModelContextProtocol.Server;

namespace ArjuyScaffold.Tools;

/// <summary>
/// Generates Api/ConfigServices/{Name}Config.cs extension-method classes. In the arjuy*
/// convention "middleware" means "IServiceCollection / WebApplication extension method used
/// to configure a cross-cutting concern (Jwt, Cors, Swagger, Database, ...)", NOT a classic
/// ASP.NET Core request-pipeline middleware. Configuration stays centralized, one file per
/// responsibility, wired from Program.cs via these extension methods.
/// </summary>
[McpServerToolType]
public static class MiddlewareConfigTool
{
    [McpServerTool(Name = "generate_middleware_config")]
    [Description(
        "Generates an Api/ConfigServices/{Name}Config.cs extension-method class following the " +
        "arjuy* convention: a static class {Name}Config with an AddXxxConfig-style " +
        "IServiceCollection extension method (and, when 'includeAppExtension' is true, a matching " +
        "UseXxxConfig-style WebApplication extension method). No expression-bodied members — " +
        "explicit block bodies with 'return' everywhere. Note: 'middleware' here means a " +
        "configuration extension method, not a classic ASP.NET Core pipeline middleware. Returns " +
        "a list with ONE {path, content} file (Api/ConfigServices/{Name}Config.cs); writes to disk only when 'outputDirectoryPath' is provided.")]
    public static List<GeneratedFile> GenerateMiddlewareConfig(
        [Description("Config name in PascalCase, e.g. 'Jwt', 'Cors', 'Swagger'. Generates {Name}Config.")] string configName,
        [Description("Namespace for the generated static class. Defaults to a generated ConfigServices namespace if omitted.")] string? namespaceName = null,
        [Description("When true, also generates a WebApplication 'Use{Name}Config' extension method for pipeline wiring, in addition to the IServiceCollection 'Add{Name}Config' one.")] bool includeAppExtension = false,
        [Description("Optional root namespace/project prefix (e.g. 'MyApp') used for the relative file path of the result. Defaults to 'Generated'.")] string? namespaceRoot = null,
        [Description("Optional absolute path to the SOLUTION root. When provided, the returned file is also written under it. Opt-in, disabled by default.")] string? outputDirectoryPath = null)
    {
        if (string.IsNullOrWhiteSpace(configName))
        {
            throw new ArgumentException("configName is required.", nameof(configName));
        }

        string resolvedNamespace = namespaceName ?? "ArjuyScaffold.Generated.Api.ConfigServices";
        string className = configName + "Config";

        StringBuilder sourceBuilder = new StringBuilder();
        sourceBuilder.AppendLine("using Microsoft.AspNetCore.Builder;");
        sourceBuilder.AppendLine("using Microsoft.Extensions.Configuration;");
        sourceBuilder.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sourceBuilder.AppendLine();
        sourceBuilder.AppendLine("namespace " + resolvedNamespace + ";");
        sourceBuilder.AppendLine();
        sourceBuilder.AppendLine("public static class " + className);
        sourceBuilder.AppendLine("{");
        sourceBuilder.AppendLine("    public static IServiceCollection Add" + className + "(this IServiceCollection services, IConfiguration configuration)");
        sourceBuilder.AppendLine("    {");
        sourceBuilder.AppendLine("        // TODO: registrar los servicios de " + configName + " aquí, leyendo las opciones desde 'configuration'.");
        sourceBuilder.AppendLine("        return services;");
        sourceBuilder.AppendLine("    }");

        if (includeAppExtension)
        {
            sourceBuilder.AppendLine();
            sourceBuilder.AppendLine("    public static WebApplication Use" + className + "(this WebApplication app)");
            sourceBuilder.AppendLine("    {");
            sourceBuilder.AppendLine("        // TODO: conectar " + configName + " al pipeline de requests aquí.");
            sourceBuilder.AppendLine("        return app;");
            sourceBuilder.AppendLine("    }");
        }

        sourceBuilder.AppendLine("}");

        List<GeneratedFile> files = new List<GeneratedFile>
        {
            new GeneratedFile(FeaturePaths.ApiConfig(namespaceRoot, className), sourceBuilder.ToString()),
        };

        GeneratedFileWriter.WriteAll(outputDirectoryPath, files);
        return files;
    }
}
