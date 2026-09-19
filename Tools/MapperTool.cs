using System;
using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Server;

namespace ArjuyScaffold.Tools;

/// <summary>
/// Generates the mapping code between an entity and its DTO pair, for EITHER of the two mapper
/// providers actually used across arjuy* projects — the caller must pass 'provider' explicitly
/// (no default), because this is a deliberate business/licensing decision, not a technical
/// default: AutoMapper is free for personal/small use but requires a paid commercial license for
/// larger companies (Eyrium's tiered pricing), while Mapperly is a free, MIT-licensed Roslyn
/// source generator with no runtime reflection cost. Which one to use depends on the target
/// client's situation, so the caller (a human, or an orchestrator relaying a human's decision)
/// must choose consciously every time.
///
/// - Mapperly convention (verified against ArjuyTurismo.Business): granularity is per MODULE, not
///   per entity — [Mapper] public partial class {Module}Mapper with partial methods
///   To{Entity}Entity(...)/Update{Entity}Entity(...)/To{Entity}ResponseModel(...). A response with
///   derived/computed fields (not a 1:1 property match) uses an explicit method with
///   'return new {...}' instead of 'partial', since Mapperly can only auto-generate flat mappings.
/// - AutoMapper convention (verified against arjuyGym: ArjuyGymApp.Business/Mappings/*Profile.cs +
///   ArjuyGymApp.Api/ConfigServices/AutoMapperConfig.cs): granularity is per ENTITY — one
///   {Entity}Profile : Profile class per entity, with CreateMap<...>() calls in the constructor
///   (ForMember(...) only for the entities whose response model needs a computed/nested field,
///   e.g. ClientProfile — the flat case, e.g. CategoryProfile, is a bare CreateMap<>() with no
///   further configuration). Registration is centralized in one AutoMapperConfig.AddServiceAutoMapper()
///   extension method that only needs ONE marker type from the assembly
///   (services.AddAutoMapper(_ => { }, typeof(SomeProfile))) — AutoMapper 16 requires this
///   specific overload, not AddAutoMapper(assembly).
/// </summary>
[McpServerToolType]
public static class MapperTool
{
    [McpServerTool(Name = "generate_mapper")]
    [Description(
        "Generates the C# mapping code between an entity and its {Entity}ResponseModel/" +
        "{Entity}RequestModel DTO pair, using EITHER Mapperly OR AutoMapper depending on the " +
        "REQUIRED 'provider' parameter (no default — a conscious business/licensing choice: " +
        "AutoMapper needs a paid commercial license for larger companies, Mapperly is free). " +
        "provider='mapperly' generates a [Mapper] partial class (verified against " +
        "ArjuyTurismo.Business convention: granularity per MODULE, partial methods " +
        "To{Entity}Entity/Update{Entity}Entity/To{Entity}ResponseModel — the response mapping " +
        "becomes an explicit 'return new {...}' method instead of 'partial' when " +
        "'hasDerivedResponseFields' is true). provider='automapper' generates a {Entity}Profile : " +
        "Profile class (verified against the real arjuyGym convention: " +
        "ArjuyGymApp.Business/Mappings/*Profile.cs + " +
        "ArjuyGymApp.Api/ConfigServices/AutoMapperConfig.cs — granularity per ENTITY, plain " +
        "CreateMap<>() calls, registered via a single AddAutoMapper(_ => { }, typeof(AnyProfile)) " +
        "marker-type call per assembly, which this tool documents as a one-time manual step in a " +
        "trailing comment rather than regenerating it for every entity). Returns the generated C# " +
        "source as a string; this tool does not write files to disk.")]
    public static string GenerateMapper(
        [Description("REQUIRED, no default: 'mapperly' or 'automapper'. Forces a conscious choice between the free source-generator (Mapperly) and the license-restricted runtime mapper (AutoMapper) rather than silently defaulting to one.")] string provider,
        [Description("Entity name in PascalCase, e.g. 'Category'. Maps Category <-> CategoryResponseModel/CategoryRequestModel.")] string entityName,
        [Description("Mapperly only: module name used for the generated class ({Module}Mapper), since the real convention groups multiple entities' mappings under one per-module partial class. Defaults to entityName when omitted (i.e. one class per entity) — pass the real module name if you intend to merge this output into an existing per-module Mapper class by hand. Ignored for provider='automapper' (which is per-entity by convention).")] string? moduleName = null,
        [Description("When true, the entity -> ResponseModel mapping cannot be a flat 1:1 property copy (it has derived/computed fields), so Mapperly generates an explicit method with a 'return new {...}' TODO body instead of a 'partial' method signature. Defaults to false. Ignored for provider='automapper', where CreateMap<>() always works and ForMember(...) TODOs are not auto-generated by this tool — add them by hand for computed fields, as the real ClientProfile does.")] bool hasDerivedResponseFields = false,
        [Description("Namespace for the generated mapper/profile class. Defaults to a generated namespace (different per provider) if omitted.")] string? namespaceName = null,
        [Description("Namespace where the entity class lives, used for the using directive. Defaults to a generated namespace if omitted.")] string? entityNamespace = null,
        [Description("Namespace where {Entity}ResponseModel/{Entity}RequestModel live, used for the using directive. Defaults to a generated namespace if omitted.")] string? dtoNamespace = null)
    {
        if (string.IsNullOrWhiteSpace(entityName))
        {
            throw new ArgumentException("entityName is required.", nameof(entityName));
        }

        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new ArgumentException("provider is required and has no default — pass 'mapperly' or 'automapper' explicitly.", nameof(provider));
        }

        string normalizedProvider = provider.Trim().ToLowerInvariant();

        if (normalizedProvider == "mapperly")
        {
            return GenerateMapperlyMapper(entityName, moduleName, hasDerivedResponseFields, namespaceName, entityNamespace, dtoNamespace);
        }

        if (normalizedProvider == "automapper")
        {
            return GenerateAutoMapperProfile(entityName, namespaceName, entityNamespace, dtoNamespace);
        }

        throw new ArgumentException("provider must be either 'mapperly' or 'automapper'.", nameof(provider));
    }

    private static string GenerateMapperlyMapper(
        string entityName,
        string? moduleName,
        bool hasDerivedResponseFields,
        string? namespaceName,
        string? entityNamespace,
        string? dtoNamespace)
    {
        string resolvedModuleName = string.IsNullOrWhiteSpace(moduleName) ? entityName : moduleName;
        string resolvedNamespace = namespaceName ?? "ArjuyScaffold.Generated.Business.Mappers";
        string resolvedEntityNamespace = entityNamespace ?? "ArjuyScaffold.Generated.Domain.Entities";
        string resolvedDtoNamespace = dtoNamespace ?? "ArjuyScaffold.Generated.Business.Models";
        string className = resolvedModuleName + "Mapper";

        StringBuilder sourceBuilder = new StringBuilder();
        sourceBuilder.AppendLine("using Riok.Mapperly.Abstractions;");
        sourceBuilder.AppendLine("using " + resolvedEntityNamespace + ";");
        sourceBuilder.AppendLine("using " + resolvedDtoNamespace + ";");
        sourceBuilder.AppendLine();
        sourceBuilder.AppendLine("namespace " + resolvedNamespace + ";");
        sourceBuilder.AppendLine();
        sourceBuilder.AppendLine("[Mapper]");
        sourceBuilder.AppendLine("public partial class " + className);
        sourceBuilder.AppendLine("{");
        sourceBuilder.AppendLine("    public partial " + entityName + " To" + entityName + "Entity(" + entityName + "RequestModel source);");
        sourceBuilder.AppendLine();
        sourceBuilder.AppendLine("    public partial void Update" + entityName + "Entity(" + entityName + "RequestModel source, " + entityName + " target);");
        sourceBuilder.AppendLine();

        if (hasDerivedResponseFields)
        {
            sourceBuilder.AppendLine("    public " + entityName + "ResponseModel To" + entityName + "ResponseModel(" + entityName + " source)");
            sourceBuilder.AppendLine("    {");
            sourceBuilder.AppendLine("        // TODO: this response has derived/computed fields, so Mapperly cannot fully");
            sourceBuilder.AppendLine("        // generate this mapping as 'partial' — map every field explicitly here.");
            sourceBuilder.AppendLine("        return new " + entityName + "ResponseModel");
            sourceBuilder.AppendLine("        {");
            sourceBuilder.AppendLine("            // TODO: map properties from 'source' here.");
            sourceBuilder.AppendLine("        };");
            sourceBuilder.AppendLine("    }");
        }
        else
        {
            sourceBuilder.AppendLine("    public partial " + entityName + "ResponseModel To" + entityName + "ResponseModel(" + entityName + " source);");
        }

        sourceBuilder.AppendLine("}");

        return sourceBuilder.ToString();
    }

    private static string GenerateAutoMapperProfile(
        string entityName,
        string? namespaceName,
        string? entityNamespace,
        string? dtoNamespace)
    {
        string resolvedNamespace = namespaceName ?? "ArjuyScaffold.Generated.Business.Mappings";
        string resolvedEntityNamespace = entityNamespace ?? "ArjuyScaffold.Generated.Domain.Entities";
        string resolvedDtoNamespace = dtoNamespace ?? "ArjuyScaffold.Generated.Business.Models";
        string className = entityName + "Profile";

        StringBuilder sourceBuilder = new StringBuilder();
        sourceBuilder.AppendLine("using AutoMapper;");
        sourceBuilder.AppendLine("using " + resolvedEntityNamespace + ";");
        sourceBuilder.AppendLine("using " + resolvedDtoNamespace + ";");
        sourceBuilder.AppendLine();
        sourceBuilder.AppendLine("namespace " + resolvedNamespace + ";");
        sourceBuilder.AppendLine();
        sourceBuilder.AppendLine("public class " + className + " : Profile");
        sourceBuilder.AppendLine("{");
        sourceBuilder.AppendLine("    public " + className + "()");
        sourceBuilder.AppendLine("    {");
        sourceBuilder.AppendLine("        CreateMap<" + entityName + ", " + entityName + "ResponseModel>();");
        sourceBuilder.AppendLine("        CreateMap<" + entityName + "RequestModel, " + entityName + ">();");
        sourceBuilder.AppendLine("    }");
        sourceBuilder.AppendLine("}");
        sourceBuilder.AppendLine();
        sourceBuilder.AppendLine("// PENDING MANUAL STEP (verified against arjuyGym: ArjuyGymApp.Api/ConfigServices/AutoMapperConfig.cs):");
        sourceBuilder.AppendLine("// AutoMapper only needs ONE marker type per assembly to discover every Profile in it —");
        sourceBuilder.AppendLine("// services.AddAutoMapper(_ => { }, typeof(" + className + ")); — so do this only the FIRST time");
        sourceBuilder.AppendLine("// a Profile is added to this assembly, not again for every subsequent entity.");
        sourceBuilder.AppendLine("// Use ForMember(...) by hand for any response field that isn't a flat 1:1 property copy");
        sourceBuilder.AppendLine("// (see the real ClientProfile in arjuyGym for worked examples).");

        return sourceBuilder.ToString();
    }
}
