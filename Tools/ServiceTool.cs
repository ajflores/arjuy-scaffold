using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using ArjuyScaffold.Models;
using ModelContextProtocol.Server;

namespace ArjuyScaffold.Tools;

/// <summary>
/// Generates Business-layer Service pairs following the arjuy* Clean Architecture convention,
/// verified against ArjuyTurismo.Business (Base/BaseService.cs, Interfaces/ICategoryService.cs,
/// Services/CategoryService.cs):
/// - Interface and implementation live in SEPARATE files/folders (Interfaces/I{Entity}Service.cs,
///   Services/{Entity}Service.cs), never in one file.
/// - The class inherits the generic abstract BaseService&lt;TEntity, TResponseModel&gt; (assumed to
///   already exist in the target project, same assumption as generate_entity for the Entity base
///   class), which already implements GetByIdAsync/GetAllAsync/DeleteAsync via a protected
///   IBaseRepository&lt;TEntity&gt; + ILogger, and declares 'protected abstract TResponseModel
///   MapToResponse(TEntity entity)'. The generated class only implements MapToResponse plus any
///   custom methods — it does NOT re-implement the 3 CRUD methods BaseService already provides.
/// - The interface still DECLARES the 3 inherited CRUD signatures (GetAllAsync/GetByIdAsync/
///   DeleteAsync) even though the class doesn't redefine them — BaseService's public virtual
///   methods satisfy the interface implicitly, exactly like the real ICategoryService/
///   CategoryService pair.
/// - The class keeps its OWN specifically-typed repository field (I{Entity}Repository, not just
///   the base's generic IBaseRepository&lt;TEntity&gt;) for custom queries, passing it to base(...)
///   as well — matches CategoryService's '_categoryRepository' field used by
///   GetAllWithExcursionsAsync.
/// - Every method returns MResult&lt;T&gt; (never throws): custom methods wrap their body in
///   try/catch and convert any exception into MResult&lt;T&gt;.Fail(ex.Message).
/// </summary>
[McpServerToolType]
public static class ServiceTool
{
    [McpServerTool(Name = "generate_service")]
    [Description(
        "Generates the C# source for a Business-layer Service pair following the REAL arjuy* " +
        "convention verified against ArjuyTurismo.Business: interface I{Entity}Service and " +
        "implementation class {Entity}Service inheriting BaseService<TEntity, TResponseModel> " +
        "(assumed to already exist in the target project, same assumption generate_entity makes " +
        "for the Entity base class). BaseService already provides GetByIdAsync/GetAllAsync/" +
        "DeleteAsync — the generated class only implements 'protected override TResponseModel " +
        "MapToResponse(TEntity entity)' (using the mapper for 'mapperProvider') plus any custom " +
        "methods you pass. The interface still declares the 3 inherited CRUD signatures (they are " +
        "satisfied implicitly by BaseService's public virtual methods, not re-implemented here). " +
        "Every method returns MResult<T>; custom methods wrap their body in try/catch -> " +
        "MResult<T>.Fail(ex.Message). Returns both files concatenated into one annotated string. " +
        "If 'interfaceOutputFilePath'/'classOutputFilePath' are provided, ALSO writes each one to " +
        "its own file on disk (opt-in, disabled by default) — interface and class are NEVER " +
        "written to the same file, matching the real Interfaces/ vs Services/ folder split.")]
    public static string GenerateService(
        [Description("Entity name in PascalCase, e.g. 'Category'. Generates ICategoryService/CategoryService.")] string entityName,
        [Description("Custom methods beyond the inherited GetByIdAsync/GetAllAsync/DeleteAsync. Each becomes Task<MResult<ReturnType>> Name(params) on both interface and class. Omit or pass an empty list for none.")] List<MethodDefinition>? customMethods,
        [Description("REQUIRED, no default: 'mapperly' or 'automapper' — same conscious licensing choice as generate_mapper, since MapToResponse needs a mapper.")] string mapperProvider,
        [Description("Mapperly only: module name for the mapper type ({Module}Mapper) this service depends on. Defaults to entityName when omitted. Ignored for provider='automapper' (mapper type is always 'IMapper').")] string? moduleName = null,
        [Description("Name of the response DTO type. Defaults to '{entityName}ResponseModel'.")] string? responseModelName = null,
        [Description("Name of the entity-specific repository interface this service depends on. Defaults to 'I{entityName}Repository'.")] string? repositoryInterfaceName = null,
        [Description("Name of the generic base service class entities inherit from. Defaults to 'BaseService', the arjuy* convention (Business/Base/BaseService.cs).")] string? baseServiceClassName = null,
        [Description("Namespace where the entity class lives. Defaults to a generated namespace if omitted.")] string? entityNamespace = null,
        [Description("Namespace for the generated interface. Defaults to a generated namespace if omitted.")] string? interfaceNamespace = null,
        [Description("Namespace for the generated class. Defaults to a generated namespace if omitted.")] string? classNamespace = null,
        [Description("Namespace where the entity-specific repository interface lives. Defaults to a generated namespace if omitted.")] string? repositoryNamespace = null,
        [Description("Namespace where the response DTO lives. Defaults to a generated namespace if omitted.")] string? dtoNamespace = null,
        [Description("Mapperly only: namespace where the {Module}Mapper type lives. Defaults to a generated namespace if omitted. Ignored for provider='automapper' (uses the AutoMapper package's IMapper).")] string? mapperNamespace = null,
        [Description("Namespace where BaseService lives. Defaults to a generated namespace if omitted.")] string? baseNamespace = null,
        [Description("Namespace where MResult<T> lives (Domain.Common convention). Defaults to a generated namespace if omitted.")] string? domainCommonNamespace = null,
        [Description("Optional absolute path to also write the generated interface .cs file to disk. When omitted, no file is written.")] string? interfaceOutputFilePath = null,
        [Description("Optional absolute path to also write the generated class .cs file to disk. When omitted, no file is written.")] string? classOutputFilePath = null)
    {
        if (string.IsNullOrWhiteSpace(entityName))
        {
            throw new ArgumentException("entityName is required.", nameof(entityName));
        }

        if (string.IsNullOrWhiteSpace(mapperProvider))
        {
            throw new ArgumentException("mapperProvider is required and has no default — pass 'mapperly' or 'automapper' explicitly.", nameof(mapperProvider));
        }

        string normalizedProvider = mapperProvider.Trim().ToLowerInvariant();
        if (normalizedProvider != "mapperly" && normalizedProvider != "automapper")
        {
            throw new ArgumentException("mapperProvider must be either 'mapperly' or 'automapper'.", nameof(mapperProvider));
        }

        List<MethodDefinition> resolvedCustomMethods = customMethods ?? new List<MethodDefinition>();

        string interfaceSource = BuildInterfaceSource(
            entityName: entityName,
            customMethods: resolvedCustomMethods,
            responseModelName: responseModelName,
            interfaceNamespace: interfaceNamespace,
            dtoNamespace: dtoNamespace,
            domainCommonNamespace: domainCommonNamespace);

        string classSource = BuildClassSource(
            entityName: entityName,
            customMethods: resolvedCustomMethods,
            normalizedMapperProvider: normalizedProvider,
            moduleName: moduleName,
            responseModelName: responseModelName,
            repositoryInterfaceName: repositoryInterfaceName,
            baseServiceClassName: baseServiceClassName,
            entityNamespace: entityNamespace,
            interfaceNamespace: interfaceNamespace,
            classNamespace: classNamespace,
            repositoryNamespace: repositoryNamespace,
            dtoNamespace: dtoNamespace,
            mapperNamespace: mapperNamespace,
            baseNamespace: baseNamespace,
            domainCommonNamespace: domainCommonNamespace);

        WriteIfRequested(interfaceOutputFilePath, interfaceSource);
        WriteIfRequested(classOutputFilePath, classSource);

        StringBuilder combined = new StringBuilder();
        combined.AppendLine("// ----- Interface (Business/Interfaces/I" + entityName + "Service.cs) -----");
        combined.AppendLine(interfaceSource);
        combined.AppendLine("// ----- Class (Business/Services/" + entityName + "Service.cs) -----");
        combined.AppendLine(classSource);

        return combined.ToString();
    }

    internal static string BuildInterfaceSource(
        string entityName,
        List<MethodDefinition> customMethods,
        string? responseModelName,
        string? interfaceNamespace,
        string? dtoNamespace,
        string? domainCommonNamespace)
    {
        string resolvedInterfaceNamespace = interfaceNamespace ?? "ArjuyScaffold.Generated.Business.Interfaces";
        string resolvedDtoNamespace = dtoNamespace ?? "ArjuyScaffold.Generated.Business.Models";
        string resolvedDomainCommonNamespace = domainCommonNamespace ?? "ArjuyScaffold.Generated.Domain.Common";
        string resolvedResponseModel = string.IsNullOrWhiteSpace(responseModelName) ? entityName + "ResponseModel" : responseModelName;
        string interfaceName = "I" + entityName + "Service";

        StringBuilder sourceBuilder = new StringBuilder();
        sourceBuilder.AppendLine("using System.Collections.Generic;");
        sourceBuilder.AppendLine("using System.Threading.Tasks;");
        sourceBuilder.AppendLine("using " + resolvedDtoNamespace + ";");
        sourceBuilder.AppendLine("using " + resolvedDomainCommonNamespace + ";");
        sourceBuilder.AppendLine();
        sourceBuilder.AppendLine("namespace " + resolvedInterfaceNamespace + ";");
        sourceBuilder.AppendLine();
        sourceBuilder.AppendLine("public interface " + interfaceName);
        sourceBuilder.AppendLine("{");
        sourceBuilder.AppendLine("    Task<MResult<List<" + resolvedResponseModel + ">>> GetAllAsync();");
        sourceBuilder.AppendLine("    Task<MResult<" + resolvedResponseModel + ">> GetByIdAsync(int id);");
        sourceBuilder.AppendLine("    Task<MResult<" + resolvedResponseModel + ">> DeleteAsync(int id);");

        foreach (MethodDefinition method in customMethods)
        {
            string parameterList = BuildParameterList(method.Parameters);
            sourceBuilder.AppendLine("    Task<MResult<" + method.ReturnType + ">> " + method.Name + "(" + parameterList + ");");
        }

        sourceBuilder.AppendLine("}");

        return sourceBuilder.ToString();
    }

    internal static string BuildClassSource(
        string entityName,
        List<MethodDefinition> customMethods,
        string normalizedMapperProvider,
        string? moduleName,
        string? responseModelName,
        string? repositoryInterfaceName,
        string? baseServiceClassName,
        string? entityNamespace,
        string? interfaceNamespace,
        string? classNamespace,
        string? repositoryNamespace,
        string? dtoNamespace,
        string? mapperNamespace,
        string? baseNamespace,
        string? domainCommonNamespace)
    {
        string resolvedClassNamespace = classNamespace ?? "ArjuyScaffold.Generated.Business.Services";
        string resolvedInterfaceNamespace = interfaceNamespace ?? "ArjuyScaffold.Generated.Business.Interfaces";
        string resolvedEntityNamespace = entityNamespace ?? "ArjuyScaffold.Generated.Domain.Entities";
        string resolvedRepositoryNamespace = repositoryNamespace ?? "ArjuyScaffold.Generated.Repository.Interfaces";
        string resolvedDtoNamespace = dtoNamespace ?? "ArjuyScaffold.Generated.Business.Models";
        string resolvedBaseNamespace = baseNamespace ?? "ArjuyScaffold.Generated.Business.Base";
        string resolvedDomainCommonNamespace = domainCommonNamespace ?? "ArjuyScaffold.Generated.Domain.Common";

        string resolvedResponseModel = string.IsNullOrWhiteSpace(responseModelName) ? entityName + "ResponseModel" : responseModelName;
        string resolvedRepositoryInterface = string.IsNullOrWhiteSpace(repositoryInterfaceName) ? "I" + entityName + "Repository" : repositoryInterfaceName;
        string resolvedBaseServiceClassName = string.IsNullOrWhiteSpace(baseServiceClassName) ? "BaseService" : baseServiceClassName;

        string interfaceName = "I" + entityName + "Service";
        string className = entityName + "Service";
        string repositoryFieldName = "_" + ToCamelCase(entityName) + "Repository";
        string repositoryParameterName = ToCamelCase(entityName) + "Repository";

        string mapperTypeName;
        string mapperUsingNamespace;
        string mapToResponseCall;

        if (normalizedMapperProvider == "mapperly")
        {
            string resolvedModuleName = string.IsNullOrWhiteSpace(moduleName) ? entityName : moduleName;
            mapperTypeName = resolvedModuleName + "Mapper";
            mapperUsingNamespace = mapperNamespace ?? "ArjuyScaffold.Generated.Business.Mappers";
            mapToResponseCall = "_mapper.To" + entityName + "ResponseModel(entity)";
        }
        else
        {
            mapperTypeName = "IMapper";
            mapperUsingNamespace = "AutoMapper";
            mapToResponseCall = "_mapper.Map<" + resolvedResponseModel + ">(entity)";
        }

        StringBuilder sourceBuilder = new StringBuilder();
        sourceBuilder.AppendLine("using System;");
        sourceBuilder.AppendLine("using System.Collections.Generic;");
        sourceBuilder.AppendLine("using System.Linq;");
        sourceBuilder.AppendLine("using System.Threading.Tasks;");
        sourceBuilder.AppendLine("using Microsoft.Extensions.Logging;");
        sourceBuilder.AppendLine("using " + resolvedBaseNamespace + ";");
        sourceBuilder.AppendLine("using " + resolvedInterfaceNamespace + ";");
        sourceBuilder.AppendLine("using " + mapperUsingNamespace + ";");
        sourceBuilder.AppendLine("using " + resolvedDtoNamespace + ";");
        sourceBuilder.AppendLine("using " + resolvedDomainCommonNamespace + ";");
        sourceBuilder.AppendLine("using " + resolvedEntityNamespace + ";");
        sourceBuilder.AppendLine("using " + resolvedRepositoryNamespace + ";");
        sourceBuilder.AppendLine();
        sourceBuilder.AppendLine("namespace " + resolvedClassNamespace + ";");
        sourceBuilder.AppendLine();
        sourceBuilder.AppendLine("public class " + className + " : " + resolvedBaseServiceClassName + "<" + entityName + ", " + resolvedResponseModel + ">, " + interfaceName);
        sourceBuilder.AppendLine("{");
        sourceBuilder.AppendLine("    private readonly " + resolvedRepositoryInterface + " " + repositoryFieldName + ";");
        sourceBuilder.AppendLine("    private readonly " + mapperTypeName + " _mapper;");
        sourceBuilder.AppendLine();
        sourceBuilder.AppendLine("    public " + className + "(" + resolvedRepositoryInterface + " " + repositoryParameterName + ", " + mapperTypeName + " mapper, ILogger<" + className + "> logger)");
        sourceBuilder.AppendLine("        : base(" + repositoryParameterName + ", logger)");
        sourceBuilder.AppendLine("    {");
        sourceBuilder.AppendLine("        " + repositoryFieldName + " = " + repositoryParameterName + ";");
        sourceBuilder.AppendLine("        _mapper = mapper;");
        sourceBuilder.AppendLine("    }");
        sourceBuilder.AppendLine();
        sourceBuilder.AppendLine("    protected override " + resolvedResponseModel + " MapToResponse(" + entityName + " entity)");
        sourceBuilder.AppendLine("    {");
        sourceBuilder.AppendLine("        return " + mapToResponseCall + ";");
        sourceBuilder.AppendLine("    }");

        foreach (MethodDefinition method in customMethods)
        {
            sourceBuilder.AppendLine();
            AppendCustomMethod(sourceBuilder, method);
        }

        sourceBuilder.AppendLine("}");

        return sourceBuilder.ToString();
    }

    private static void AppendCustomMethod(StringBuilder sourceBuilder, MethodDefinition method)
    {
        string parameterList = BuildParameterList(method.Parameters);
        string returnTypeWrapped = "Task<MResult<" + method.ReturnType + ">>";

        sourceBuilder.AppendLine("    public async " + returnTypeWrapped + " " + method.Name + "(" + parameterList + ")");
        sourceBuilder.AppendLine("    {");
        sourceBuilder.AppendLine("        try");
        sourceBuilder.AppendLine("        {");
        sourceBuilder.AppendLine("            // TODO: implement " + method.Name + " business logic.");
        sourceBuilder.AppendLine("            throw new NotImplementedException();");
        sourceBuilder.AppendLine("        }");
        sourceBuilder.AppendLine("        catch (Exception ex)");
        sourceBuilder.AppendLine("        {");
        sourceBuilder.AppendLine("            _logger.LogError(ex, \"Error en {Service}\", GetType().Name);");
        sourceBuilder.AppendLine("            return MResult<" + method.ReturnType + ">.Fail(ex.Message);");
        sourceBuilder.AppendLine("        }");
        sourceBuilder.AppendLine("    }");
    }

    private static void WriteIfRequested(string? outputFilePath, string content)
    {
        if (string.IsNullOrWhiteSpace(outputFilePath))
        {
            return;
        }

        string? directory = System.IO.Path.GetDirectoryName(outputFilePath);
        if (!string.IsNullOrWhiteSpace(directory) && !System.IO.Directory.Exists(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
        }

        System.IO.File.WriteAllText(outputFilePath, content);
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
