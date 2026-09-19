using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using ArjuyScaffold.Models;
using ModelContextProtocol.Server;

namespace ArjuyScaffold.Tools;

/// <summary>
/// Generates Repository-layer pairs (I{Name}Repository / {Name}Repository) following the real
/// arjuy* convention found in ArjuyTurismo.Repository: a generic IBaseRepository&lt;TEntity&gt; /
/// BaseRepository&lt;TEntity&gt; (constrained to Entity, i.e. the soft-delete-capable base class)
/// already provides GetByIdAsync, GetAllAsync, CreateAsync, UpdateAsync, DeleteAsync and
/// ExistsAsync — every specific repository interface just extends
/// IBaseRepository&lt;{Name}&gt; and every specific repository class just extends
/// BaseRepository&lt;{Name}&gt;, constructor-injecting the DbContext straight into the base
/// constructor. Custom methods are plain Task/Task&lt;T&gt; (no MResult&lt;T&gt; wrapper, no
/// try/catch — that belongs to the Business layer) and, unlike Service methods, are expected to
/// rely on EF Core's global query filter for soft delete (HasQueryFilter(x => !x.Deleted)) rather
/// than checking entity.Deleted by hand inside the query.
/// </summary>
[McpServerToolType]
public static class RepositoryTool
{
    [McpServerTool(Name = "generate_repository")]
    [Description(
        "Generates the C# source for a Repository-layer pair: interface I{Name}Repository and " +
        "implementation class {Name}Repository. Both extend the generic base " +
        "IBaseRepository<{Name}>/BaseRepository<{Name}> (GetByIdAsync, GetAllAsync, CreateAsync, " +
        "UpdateAsync, DeleteAsync, ExistsAsync already inherited — do not redeclare them). The " +
        "constructor only forwards the DbContext to the base constructor. Optional custom methods " +
        "are generated as plain async Task/Task<T> members (no MResult<T> wrapper and no " +
        "try/catch, unlike generate_service — Repository methods propagate exceptions to the " +
        "Business layer) with a TODO body using _dbSet/_context, matching the real " +
        "ArjuyTurismo.Repository convention where soft delete is handled by EF Core's global " +
        "query filter, not by manual entity.Deleted checks. Returns the generated C# source " +
        "(interface + class in one string) — this tool does not write files to disk.")]
    public static string GenerateRepository(
        [Description("Entity name in PascalCase, e.g. 'Category'. Generates ICategoryRepository/CategoryRepository.")] string entityName,
        [Description("Custom methods beyond the inherited CRUD (GetByIdAsync, GetAllAsync, CreateAsync, UpdateAsync, DeleteAsync, ExistsAsync). Omit or pass an empty list for a repository with no custom queries.")] List<RepositoryMethodDefinition>? customMethods = null,
        [Description("Namespace where the entity class lives, used for the using directive. Defaults to a generated namespace if omitted.")] string? entityNamespace = null,
        [Description("Namespace for the generated interface. Defaults to a generated namespace if omitted.")] string? interfaceNamespace = null,
        [Description("Namespace for the generated implementation class. Defaults to a generated namespace if omitted.")] string? implementationNamespace = null,
        [Description("Namespace where IBaseRepository<T>/BaseRepository<T> live. Defaults to a generated namespace if omitted; override to match the target project's real Base folder namespace, e.g. 'ArjuyTurismo.Repository.Base'.")] string? baseNamespace = null,
        [Description("Namespace where the DbContext class lives, used for the using directive. Defaults to a generated namespace if omitted.")] string? dbContextNamespace = null,
        [Description("DbContext type name injected into the constructor and forwarded to the base constructor. Defaults to 'AppDbContext', the arjuy* convention.")] string? dbContextTypeName = null,
        [Description("Name of the generic base repository interface. Defaults to 'IBaseRepository', the arjuy* convention.")] string? baseRepositoryInterfaceName = null,
        [Description("Name of the generic base repository class. Defaults to 'BaseRepository', the arjuy* convention.")] string? baseRepositoryClassName = null)
    {
        if (string.IsNullOrWhiteSpace(entityName))
        {
            throw new ArgumentException("entityName is required.", nameof(entityName));
        }

        string resolvedEntityNamespace = entityNamespace ?? "ArjuyScaffold.Generated.Domain.Entities";
        string resolvedInterfaceNamespace = interfaceNamespace ?? "ArjuyScaffold.Generated.Repository.Interfaces";
        string resolvedImplementationNamespace = implementationNamespace ?? "ArjuyScaffold.Generated.Repository.Repositories";
        string resolvedBaseNamespace = baseNamespace ?? "ArjuyScaffold.Generated.Repository.Base";
        string resolvedDbContextNamespace = dbContextNamespace ?? "ArjuyScaffold.Generated.Persistence.Database";
        string resolvedDbContextTypeName = string.IsNullOrWhiteSpace(dbContextTypeName) ? "AppDbContext" : dbContextTypeName;
        string resolvedBaseRepositoryInterfaceName = string.IsNullOrWhiteSpace(baseRepositoryInterfaceName) ? "IBaseRepository" : baseRepositoryInterfaceName;
        string resolvedBaseRepositoryClassName = string.IsNullOrWhiteSpace(baseRepositoryClassName) ? "BaseRepository" : baseRepositoryClassName;

        List<RepositoryMethodDefinition> resolvedMethods = customMethods ?? new List<RepositoryMethodDefinition>();

        string interfaceName = "I" + entityName + "Repository";
        string className = entityName + "Repository";

        StringBuilder sourceBuilder = new StringBuilder();

        AppendInterfaceUsings(sourceBuilder, resolvedEntityNamespace, resolvedBaseNamespace);
        sourceBuilder.AppendLine();
        sourceBuilder.AppendLine("namespace " + resolvedInterfaceNamespace + ";");
        sourceBuilder.AppendLine();
        AppendInterface(sourceBuilder, interfaceName, entityName, resolvedBaseRepositoryInterfaceName, resolvedMethods);

        sourceBuilder.AppendLine();

        AppendClassUsings(sourceBuilder, resolvedEntityNamespace, resolvedDbContextNamespace, resolvedBaseNamespace, resolvedInterfaceNamespace);
        sourceBuilder.AppendLine();
        sourceBuilder.AppendLine("namespace " + resolvedImplementationNamespace + ";");
        sourceBuilder.AppendLine();
        AppendClass(sourceBuilder, className, interfaceName, entityName, resolvedBaseRepositoryClassName, resolvedDbContextTypeName, resolvedMethods);

        return sourceBuilder.ToString();
    }

    private static void AppendInterfaceUsings(StringBuilder sourceBuilder, string entityNamespace, string baseNamespace)
    {
        sourceBuilder.AppendLine("using System.Collections.Generic;");
        sourceBuilder.AppendLine("using System.Threading.Tasks;");
        sourceBuilder.AppendLine("using " + entityNamespace + ";");
        sourceBuilder.AppendLine("using " + baseNamespace + ";");
    }

    private static void AppendClassUsings(StringBuilder sourceBuilder, string entityNamespace, string dbContextNamespace, string baseNamespace, string interfaceNamespace)
    {
        sourceBuilder.AppendLine("using System;");
        sourceBuilder.AppendLine("using System.Collections.Generic;");
        sourceBuilder.AppendLine("using System.Threading.Tasks;");
        sourceBuilder.AppendLine("using " + entityNamespace + ";");
        sourceBuilder.AppendLine("using " + dbContextNamespace + ";");
        sourceBuilder.AppendLine("using " + baseNamespace + ";");
        sourceBuilder.AppendLine("using " + interfaceNamespace + ";");
    }

    private static void AppendInterface(
        StringBuilder sourceBuilder,
        string interfaceName,
        string entityName,
        string baseRepositoryInterfaceName,
        List<RepositoryMethodDefinition> methods)
    {
        sourceBuilder.AppendLine("public interface " + interfaceName + " : " + baseRepositoryInterfaceName + "<" + entityName + ">");
        sourceBuilder.AppendLine("{");

        for (int index = 0; index < methods.Count; index++)
        {
            RepositoryMethodDefinition method = methods[index];
            string parameterList = BuildParameterList(method.Parameters);
            string wrappedReturnType = BuildTaskReturnType(method.ReturnType);
            sourceBuilder.AppendLine("    " + wrappedReturnType + " " + method.Name + "(" + parameterList + ");");
        }

        sourceBuilder.AppendLine("}");
    }

    private static void AppendClass(
        StringBuilder sourceBuilder,
        string className,
        string interfaceName,
        string entityName,
        string baseRepositoryClassName,
        string dbContextTypeName,
        List<RepositoryMethodDefinition> methods)
    {
        sourceBuilder.AppendLine("public class " + className + " : " + baseRepositoryClassName + "<" + entityName + ">, " + interfaceName);
        sourceBuilder.AppendLine("{");
        sourceBuilder.AppendLine("    public " + className + "(" + dbContextTypeName + " context) : base(context)");
        sourceBuilder.AppendLine("    {");
        sourceBuilder.AppendLine("    }");

        foreach (RepositoryMethodDefinition method in methods)
        {
            sourceBuilder.AppendLine();
            AppendMethod(sourceBuilder, method);
        }

        sourceBuilder.AppendLine("}");
    }

    private static void AppendMethod(StringBuilder sourceBuilder, RepositoryMethodDefinition method)
    {
        string parameterList = BuildParameterList(method.Parameters);
        string wrappedReturnType = BuildTaskReturnType(method.ReturnType);

        sourceBuilder.AppendLine("    public async " + wrappedReturnType + " " + method.Name + "(" + parameterList + ")");
        sourceBuilder.AppendLine("    {");
        sourceBuilder.AppendLine("        // TODO: implement " + method.Name + " query logic using _dbSet/_context.");
        sourceBuilder.AppendLine("        // Soft-deleted rows are already excluded by the EF Core global query filter.");
        sourceBuilder.AppendLine("        throw new NotImplementedException();");
        sourceBuilder.AppendLine("    }");
    }

    private static string BuildTaskReturnType(string rawReturnType)
    {
        if (string.IsNullOrWhiteSpace(rawReturnType) || rawReturnType.Trim() == "void")
        {
            return "Task";
        }

        return "Task<" + rawReturnType.Trim() + ">";
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
}
