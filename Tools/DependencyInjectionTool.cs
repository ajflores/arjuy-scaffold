using System;
using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Server;

namespace ArjuyScaffold.Tools;

/// <summary>
/// INCOMPLETE / STUB for this iteration. Intended to generate or validate the
/// Api/ConfigServices/DependencyInjectionConfig.cs RegisterAssembly(...) extension method that
/// scans an assembly by reflection and wires I{Name}Repository -> {Name}Repository and
/// I{Name}Service -> {Name}Service by naming convention. Today it only emits a fixed
/// reference implementation; it does NOT inspect a real assembly, and it does not offer a
/// "validate an existing file" mode yet. Left documented as pending for the next iteration.
/// </summary>
[McpServerToolType]
public static class DependencyInjectionTool
{
    [McpServerTool(Name = "generate_di_registration")]
    [Description(
        "STUB/INCOMPLETE (documented pending work, not a full implementation): emits a fixed " +
        "reference RegisterAssembly(...) extension method for Api/ConfigServices/" +
        "DependencyInjectionConfig.cs, which scans an assembly and registers " +
        "I{Name}Repository -> {Name}Repository and I{Name}Service -> {Name}Service by naming " +
        "convention. It ignores the 'lifetime' parameter beyond choosing the registration call " +
        "and does not read any real assembly to confirm matches, nor validate an existing file. " +
        "Returns generated C# source as a string; does not write files to disk.")]
    public static string GenerateDependencyInjectionRegistration(
        [Description("Service lifetime to register with: 'Scoped', 'Transient' or 'Singleton'. Defaults to 'Scoped'.")] string lifetime = "Scoped",
        [Description("Namespace for the generated static class. Defaults to a generated ConfigServices namespace if omitted.")] string? namespaceName = null)
    {
        string resolvedLifetime = string.IsNullOrWhiteSpace(lifetime) ? "Scoped" : lifetime;
        string resolvedNamespace = namespaceName ?? "ArjuyScaffold.Generated.Api.ConfigServices";

        StringBuilder sourceBuilder = new StringBuilder();
        sourceBuilder.AppendLine("using System;");
        sourceBuilder.AppendLine("using System.Linq;");
        sourceBuilder.AppendLine("using System.Reflection;");
        sourceBuilder.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sourceBuilder.AppendLine();
        sourceBuilder.AppendLine("namespace " + resolvedNamespace + ";");
        sourceBuilder.AppendLine();
        sourceBuilder.AppendLine("public static class DependencyInjectionConfig");
        sourceBuilder.AppendLine("{");
        sourceBuilder.AppendLine("    public static IServiceCollection RegisterAssembly(this IServiceCollection services, Assembly assembly)");
        sourceBuilder.AppendLine("    {");
        sourceBuilder.AppendLine("        var candidateTypes = assembly.GetTypes().Where(type => type.IsClass && !type.IsAbstract).ToList();");
        sourceBuilder.AppendLine();
        sourceBuilder.AppendLine("        foreach (var implementationType in candidateTypes)");
        sourceBuilder.AppendLine("        {");
        sourceBuilder.AppendLine("            var conventionInterface = implementationType.GetInterface(\"I\" + implementationType.Name);");
        sourceBuilder.AppendLine("            if (conventionInterface == null)");
        sourceBuilder.AppendLine("            {");
        sourceBuilder.AppendLine("                continue;");
        sourceBuilder.AppendLine("            }");
        sourceBuilder.AppendLine();
        sourceBuilder.AppendLine("            bool isRepositoryOrService = implementationType.Name.EndsWith(\"Repository\") || implementationType.Name.EndsWith(\"Service\");");
        sourceBuilder.AppendLine("            if (!isRepositoryOrService)");
        sourceBuilder.AppendLine("            {");
        sourceBuilder.AppendLine("                continue;");
        sourceBuilder.AppendLine("            }");
        sourceBuilder.AppendLine();
        sourceBuilder.AppendLine("            services.Add" + resolvedLifetime + "(conventionInterface, implementationType);");
        sourceBuilder.AppendLine("        }");
        sourceBuilder.AppendLine();
        sourceBuilder.AppendLine("        return services;");
        sourceBuilder.AppendLine("    }");
        sourceBuilder.AppendLine("}");

        return sourceBuilder.ToString();
    }
}
