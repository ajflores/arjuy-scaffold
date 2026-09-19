using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using ArjuyScaffold.Models;
using ModelContextProtocol.Server;

namespace ArjuyScaffold.Tools;

/// <summary>
/// Generates Business-layer Service pairs (I{Name}Service / {Name}Service) following the
/// arjuy* convention: every method returns MResult&lt;T&gt; instead of throwing, so callers
/// never need try/catch around service calls. Method bodies use explicit block syntax with
/// an explicit return — no expression-bodied members (=>).
/// </summary>
[McpServerToolType]
public static class ServiceTool
{
    [McpServerTool(Name = "generate_service")]
    [Description(
        "Generates the C# source for a Business-layer Service pair: interface I{Name}Service " +
        "and implementation class {Name}Service. Every method returns MResult<T> (never throws): " +
        "the body wraps the real logic in try/catch and converts any exception into " +
        "MResult<T>.Fail(ex.Message), following the arjuy* Clean Architecture convention. " +
        "The generated class takes an I{Name}Repository dependency via constructor injection, " +
        "matching the RegisterAssembly DI-by-reflection convention. Returns the generated C# " +
        "source (interface + class in one string) — this tool does not write files to disk.")]
    public static string GenerateService(
        [Description("Service name in PascalCase without the 'Service' suffix, e.g. 'Ticket' generates ITicketService/TicketService.")] string serviceName,
        [Description("Methods to generate on the service. Each method returns MResult<ReturnType>.")] List<MethodDefinition> methods,
        [Description("Namespace for the generated interface and class. Defaults to a generated namespace if omitted.")] string? namespaceName = null)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            throw new ArgumentException("serviceName is required.", nameof(serviceName));
        }

        string resolvedNamespace = namespaceName ?? "ArjuyScaffold.Generated.Business.Services";
        string interfaceName = "I" + serviceName + "Service";
        string className = serviceName + "Service";
        string repositoryInterfaceName = "I" + serviceName + "Repository";
        string repositoryFieldName = "_" + ToCamelCase(serviceName) + "Repository";
        string repositoryParameterName = ToCamelCase(serviceName) + "Repository";

        StringBuilder sourceBuilder = new StringBuilder();
        sourceBuilder.AppendLine("using System;");
        sourceBuilder.AppendLine("using System.Collections.Generic;");
        sourceBuilder.AppendLine();
        sourceBuilder.AppendLine("namespace " + resolvedNamespace + ";");
        sourceBuilder.AppendLine();

        AppendInterface(sourceBuilder, interfaceName, methods);
        sourceBuilder.AppendLine();
        AppendClass(sourceBuilder, className, interfaceName, repositoryInterfaceName, repositoryFieldName, repositoryParameterName, methods);

        return sourceBuilder.ToString();
    }

    private static void AppendInterface(StringBuilder sourceBuilder, string interfaceName, List<MethodDefinition> methods)
    {
        sourceBuilder.AppendLine("public interface " + interfaceName);
        sourceBuilder.AppendLine("{");

        if (methods != null)
        {
            foreach (MethodDefinition method in methods)
            {
                string parameterList = BuildParameterList(method.Parameters);
                sourceBuilder.AppendLine("    MResult<" + method.ReturnType + "> " + method.Name + "(" + parameterList + ");");
            }
        }

        sourceBuilder.AppendLine("}");
    }

    private static void AppendClass(
        StringBuilder sourceBuilder,
        string className,
        string interfaceName,
        string repositoryInterfaceName,
        string repositoryFieldName,
        string repositoryParameterName,
        List<MethodDefinition> methods)
    {
        sourceBuilder.AppendLine("public class " + className + " : " + interfaceName);
        sourceBuilder.AppendLine("{");
        sourceBuilder.AppendLine("    private readonly " + repositoryInterfaceName + " " + repositoryFieldName + ";");
        sourceBuilder.AppendLine();
        sourceBuilder.AppendLine("    public " + className + "(" + repositoryInterfaceName + " " + repositoryParameterName + ")");
        sourceBuilder.AppendLine("    {");
        sourceBuilder.AppendLine("        " + repositoryFieldName + " = " + repositoryParameterName + ";");
        sourceBuilder.AppendLine("    }");

        if (methods != null)
        {
            foreach (MethodDefinition method in methods)
            {
                sourceBuilder.AppendLine();
                AppendMethod(sourceBuilder, method);
            }
        }

        sourceBuilder.AppendLine("}");
    }

    private static void AppendMethod(StringBuilder sourceBuilder, MethodDefinition method)
    {
        string parameterList = BuildParameterList(method.Parameters);
        string returnTypeWrapped = "MResult<" + method.ReturnType + ">";

        sourceBuilder.AppendLine("    public " + returnTypeWrapped + " " + method.Name + "(" + parameterList + ")");
        sourceBuilder.AppendLine("    {");
        sourceBuilder.AppendLine("        try");
        sourceBuilder.AppendLine("        {");
        sourceBuilder.AppendLine("            // TODO: implement " + method.Name + " business logic.");
        sourceBuilder.AppendLine("            throw new NotImplementedException();");
        sourceBuilder.AppendLine("        }");
        sourceBuilder.AppendLine("        catch (Exception ex)");
        sourceBuilder.AppendLine("        {");
        sourceBuilder.AppendLine("            return " + returnTypeWrapped + ".Fail(ex.Message);");
        sourceBuilder.AppendLine("        }");
        sourceBuilder.AppendLine("    }");
    }

    private static string BuildParameterList(List<ParameterDefinition> parameters)
    {
        if (parameters == null || parameters.Count == 0)
        {
            return string.Empty;
        }

        IEnumerable<string> parameterFragments = parameters.Select(parameter => parameter.Type + " " + parameter.Name);
        return string.Join(", ", parameterFragments);
    }

    private static string ToCamelCase(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        if (value.Length == 1)
        {
            return value.ToLowerInvariant();
        }

        return char.ToLowerInvariant(value[0]) + value.Substring(1);
    }
}
