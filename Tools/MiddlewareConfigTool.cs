using System;
using System.ComponentModel;
using System.Text;
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
        "the generated C# source as a string; does not write files to disk.")]
    public static string GenerateMiddlewareConfig(
        [Description("Config name in PascalCase, e.g. 'Jwt', 'Cors', 'Swagger'. Generates {Name}Config.")] string configName,
        [Description("Namespace for the generated static class. Defaults to a generated ConfigServices namespace if omitted.")] string? namespaceName = null,
        [Description("When true, also generates a WebApplication 'Use{Name}Config' extension method for pipeline wiring, in addition to the IServiceCollection 'Add{Name}Config' one.")] bool includeAppExtension = false)
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
        sourceBuilder.AppendLine("        // TODO: register " + configName + " services here, reading options from 'configuration'.");
        sourceBuilder.AppendLine("        return services;");
        sourceBuilder.AppendLine("    }");

        if (includeAppExtension)
        {
            sourceBuilder.AppendLine();
            sourceBuilder.AppendLine("    public static WebApplication Use" + className + "(this WebApplication app)");
            sourceBuilder.AppendLine("    {");
            sourceBuilder.AppendLine("        // TODO: wire " + configName + " into the request pipeline here.");
            sourceBuilder.AppendLine("        return app;");
            sourceBuilder.AppendLine("    }");
        }

        sourceBuilder.AppendLine("}");

        return sourceBuilder.ToString();
    }
}
