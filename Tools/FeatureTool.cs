using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using ArjuyScaffold.Models;
using ArjuyScaffold.Support;
using ModelContextProtocol.Server;

namespace ArjuyScaffold.Tools;

/// <summary>
/// Composite tool that orchestrates the 6 individual generators (Entity, Repository, Service,
/// EF Configuration, DTOs, Mapper) for a single entity in one call, reusing the exact same static
/// generation methods the individual tools expose — no logic is duplicated. Returns every
/// generated file concatenated into one annotated string, and optionally writes each one to disk
/// under the standard 5-layer folder convention when 'outputDirectoryPath' is provided.
///
/// Deliberately NOT automated by this tool (documented as pending manual steps in the returned
/// result, matching the "stub/pending" style already used by generate_di_registration):
///   1. Adding the new DbSet&lt;{Entity}&gt; to AppDbContext.
///   2. Registering the mapper (AutoMapper needs one AddAutoMapper(_ => { }, typeof(AnyProfile))
///      marker call per ASSEMBLY, not per entity; Mapperly needs no DI registration at all).
///   3. A CRUD controller — out of scope for this iteration.
/// </summary>
[McpServerToolType]
public static class FeatureTool
{
    [McpServerTool(Name = "generate_feature")]
    [Description(
        "Composite tool: generates a full vertical slice for one entity in a single call — " +
        "Entity, Repository (I{Entity}Repository/{Entity}Repository), Service (I{Entity}Service " +
        "interface + {Entity}Service class inheriting BaseService<TEntity, TResponseModel> — " +
        "verified real convention, interface and class in SEPARATE files), EF Core configuration " +
        "(IEntityTypeConfiguration<{Entity}>), DTOs ({Entity}ResponseModel/{Entity}RequestModel) " +
        "and the mapper for the chosen 'mapperProvider' ('mapperly' or 'automapper', REQUIRED, no " +
        "default — same conscious licensing choice as generate_mapper). Internally calls the same " +
        "static generation methods used by generate_entity/generate_repository/generate_service/" +
        "generate_ef_configuration/generate_dto/generate_mapper — nothing is reimplemented. Returns " +
        "a list of {path, content} files — ONE FILE PER PUBLIC TYPE, file name = type name (entity, " +
        "I{Entity}Repository, {Entity}Repository, I{Entity}Service, {Entity}Service, " +
        "{Entity}Configuration, {Entity}ResponseModel, {Entity}RequestModel under " +
        "Business/Models/{PluralEntity}/, mapper); when 'outputDirectoryPath' is " +
        "provided, ALSO writes each file to disk under the standard 5-layer folder layout (opt-in, " +
        "disabled by default, same pattern as generate_entity's 'outputFilePath'). Does NOT " +
        "generate a CRUD controller, does NOT touch AppDbContext (no DbSet is added), and does NOT " +
        "register the mapper in DI. PENDING MANUAL STEPS: (1) add 'public DbSet<{Entity}> {PluralEntity} " +
        "{ get; set; }' to AppDbContext; (2) AutoMapper only: ONE AddAutoMapper(_ => { }, " +
        "typeof(AnyProfile)) per assembly (Mapperly needs no DI registration); (3) the CRUD " +
        "controller. Use the files returned here as-is — do NOT rewrite them by hand.")]
    public static List<GeneratedFile> GenerateFeature(
        [Description("Entity name in PascalCase, e.g. 'Category'.")] string entityName,
        [Description("Properties shared across Entity, DTOs and EF configuration (name, type, plus EF-only IsRequired/MaxLength). Do NOT include Id, CreatedAt, UpdatedAt or Deleted — those are inherited from the base entity.")] List<FeaturePropertyDefinition> properties,
        [Description("REQUIRED, no default: 'mapperly' or 'automapper'. See generate_mapper for the full rationale — this is a licensing decision, not a technical default.")] string mapperProvider,
        [Description("Custom repository methods beyond the inherited CRUD. Omit or pass an empty list for none.")] List<RepositoryMethodDefinition>? repositoryCustomMethods = null,
        [Description("Service methods to generate (each returns MResult<T>). Omit or pass an empty list for none.")] List<MethodDefinition>? serviceMethods = null,
        [Description("Optional EF Core relationships to configure (HasMany/HasOne + WithOne/WithMany + optional HasForeignKey + OnDelete). Omit or pass an empty list for an entity with no navigation properties.")] List<EfRelationDefinition>? efRelations = null,
        [Description("When true, the EF configuration OMITS HasQueryFilter(x => !x.Deleted) — use for lookup/catalog entities. Defaults to false.")] bool isLookup = false,
        [Description("Optional root namespace (e.g. 'ArjuyTurismo') used to derive every layer's namespace automatically: {root}.Domain.Entities, {root}.Repository.Interfaces/.Repositories/.Base, {root}.Persistence.Database(.Configurations), {root}.Business.Services/.Models.{PluralEntity}/.Mappers(or .Mappings). When omitted, every layer falls back to its own tool's default 'ArjuyScaffold.Generated.*' namespace.")] string? namespaceRoot = null,
        [Description("Name of the base class entities inherit from. Defaults to 'Entity', the arjuy* Clean Architecture convention.")] string? baseEntityName = null,
        [Description("DbContext type name injected into the repository constructor. Defaults to 'AppDbContext'.")] string? dbContextTypeName = null,
        [Description("Name of the generic base repository interface. Defaults to 'IBaseRepository'.")] string? baseRepositoryInterfaceName = null,
        [Description("Name of the generic base repository class. Defaults to 'BaseRepository'.")] string? baseRepositoryClassName = null,
        [Description("Mapperly only: true when the entity -> ResponseModel mapping has derived/computed fields (not a flat 1:1 copy), generating an explicit method instead of 'partial'. Defaults to false. Ignored for provider='automapper'.")] bool hasDerivedResponseFields = false,
        [Description("Optional absolute path to the SOLUTION root directory. When provided, also writes every generated file to disk under separate PROJECT folders (not a single project's subfolders) matching real Clean Architecture: {namespaceRoot}.Domain/Entities, {namespaceRoot}.Repository, {namespaceRoot}.Business/Services, {namespaceRoot}.Persistence.Database/Configurations, {namespaceRoot}.Business/Models, {namespaceRoot}.Business/Mappers or /Mappings. Domain, Repository and Persistence.Database are separate projects; Business is a single project holding Services/Models/Mappers as subfolders. If 'namespaceRoot' is omitted, the prefix falls back to 'Generated'. Opt-in, disabled by default — when omitted, no files are written and only the combined string is returned.")] string? outputDirectoryPath = null)
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

        List<FeaturePropertyDefinition> resolvedProperties = properties ?? new List<FeaturePropertyDefinition>();

        string? entityNamespace = null;
        string? repositoryInterfaceNamespace = null;
        string? repositoryImplementationNamespace = null;
        string? repositoryBaseNamespace = null;
        string? dbContextNamespace = null;
        string? serviceInterfaceNamespace = null;
        string? serviceClassNamespace = null;
        string? serviceBaseNamespace = null;
        string? dtoNamespace = null;
        string? mapperNamespace = null;
        string? efConfigurationNamespace = null;

        if (!string.IsNullOrWhiteSpace(namespaceRoot))
        {
            entityNamespace = namespaceRoot + ".Domain.Entities";
            repositoryInterfaceNamespace = namespaceRoot + ".Repository.Interfaces";
            repositoryImplementationNamespace = namespaceRoot + ".Repository.Repositories";
            repositoryBaseNamespace = namespaceRoot + ".Repository.Base";
            dbContextNamespace = namespaceRoot + ".Persistence.Database";
            serviceInterfaceNamespace = namespaceRoot + ".Business.Interfaces";
            serviceClassNamespace = namespaceRoot + ".Business.Services";
            serviceBaseNamespace = namespaceRoot + ".Business.Base";
            dtoNamespace = namespaceRoot + ".Business.Models." + FeaturePaths.ModelFolder(entityName);
            mapperNamespace = normalizedProvider == "mapperly" ? namespaceRoot + ".Business.Mappers" : namespaceRoot + ".Business.Mappings";
            efConfigurationNamespace = namespaceRoot + ".Persistence.Database.Configurations";
        }

        List<PropertyDefinition> plainProperties = resolvedProperties
            .Select(property => new PropertyDefinition { Name = property.Name, Type = property.Type })
            .ToList();

        List<EfPropertyDefinition> efProperties = resolvedProperties
            .Select(property => new EfPropertyDefinition { Name = property.Name, IsRequired = property.IsRequired, MaxLength = property.MaxLength })
            .ToList();

        string? root = namespaceRoot;
        string entitySource = EntityTool.BuildEntitySource(
            entityName: entityName,
            properties: plainProperties,
            namespaceName: entityNamespace,
            baseEntityName: baseEntityName,
            baseEntityNamespace: null);

        string resolvedEntityNs = entityNamespace ?? "ArjuyScaffold.Generated.Domain.Entities";
        string resolvedRepoInterfaceNs = repositoryInterfaceNamespace ?? "ArjuyScaffold.Generated.Repository.Interfaces";
        string resolvedRepoImplementationNs = repositoryImplementationNamespace ?? "ArjuyScaffold.Generated.Repository.Repositories";
        string resolvedRepoBaseNs = repositoryBaseNamespace ?? "ArjuyScaffold.Generated.Repository.Base";
        string resolvedDbContextNs = dbContextNamespace ?? "ArjuyScaffold.Generated.Persistence.Database";
        string resolvedDbContextName = string.IsNullOrWhiteSpace(dbContextTypeName) ? "AppDbContext" : dbContextTypeName;
        string resolvedBaseRepoInterface = string.IsNullOrWhiteSpace(baseRepositoryInterfaceName) ? "IBaseRepository" : baseRepositoryInterfaceName;
        string resolvedBaseRepoClass = string.IsNullOrWhiteSpace(baseRepositoryClassName) ? "BaseRepository" : baseRepositoryClassName;
        List<RepositoryMethodDefinition> repositoryMethods = repositoryCustomMethods ?? new List<RepositoryMethodDefinition>();

        string repositoryInterfaceSource = RepositoryTool.BuildInterfaceSource(
            "I" + entityName + "Repository", entityName, resolvedEntityNs, resolvedRepoInterfaceNs, resolvedRepoBaseNs, resolvedBaseRepoInterface, repositoryMethods);

        string repositoryClassSource = RepositoryTool.BuildClassSource(
            entityName + "Repository", "I" + entityName + "Repository", entityName, resolvedEntityNs, resolvedRepoImplementationNs, resolvedRepoBaseNs,
            resolvedRepoInterfaceNs, resolvedDbContextNs, resolvedBaseRepoClass, resolvedDbContextName, repositoryMethods);

        List<MethodDefinition> resolvedServiceMethods = serviceMethods ?? new List<MethodDefinition>();

        string serviceInterfaceSource = ServiceTool.BuildInterfaceSource(
            entityName: entityName,
            customMethods: resolvedServiceMethods,
            responseModelName: null,
            interfaceNamespace: serviceInterfaceNamespace,
            dtoNamespace: dtoNamespace,
            domainCommonNamespace: namespaceRoot == null ? null : namespaceRoot + ".Domain.Common");

        string serviceClassSource = ServiceTool.BuildClassSource(
            entityName: entityName,
            customMethods: resolvedServiceMethods,
            normalizedMapperProvider: normalizedProvider,
            responseModelName: null,
            repositoryInterfaceName: null,
            baseServiceClassName: null,
            entityNamespace: entityNamespace,
            interfaceNamespace: serviceInterfaceNamespace,
            classNamespace: serviceClassNamespace,
            repositoryNamespace: repositoryInterfaceNamespace,
            dtoNamespace: dtoNamespace,
            mapperNamespace: mapperNamespace,
            baseNamespace: serviceBaseNamespace,
            domainCommonNamespace: namespaceRoot == null ? null : namespaceRoot + ".Domain.Common");

        string efConfigurationSource = EfConfigurationTool.BuildEfConfigurationSource(
            entityName: entityName,
            properties: efProperties,
            relations: efRelations,
            isLookup: isLookup,
            tableName: null,
            namespaceName: efConfigurationNamespace,
            entityNamespace: entityNamespace);

        List<GeneratedFile> dtoFiles = DtoTool.GenerateDto(
            entityName: entityName,
            properties: plainProperties,
            namespaceName: dtoNamespace,
            namespaceRoot: root,
            outputDirectoryPath: null);

        GeneratedFile mapperFile = MapperTool.BuildMapperFile(
            normalizedProvider: normalizedProvider,
            entityName: entityName,
            hasDerivedResponseFields: hasDerivedResponseFields,
            namespaceName: mapperNamespace,
            entityNamespace: entityNamespace,
            dtoNamespace: dtoNamespace,
            namespaceRoot: root);

        List<GeneratedFile> files = new List<GeneratedFile>
        {
            new GeneratedFile(FeaturePaths.Entity(root, entityName), entitySource),
            new GeneratedFile(FeaturePaths.RepositoryInterface(root, entityName), repositoryInterfaceSource),
            new GeneratedFile(FeaturePaths.RepositoryClass(root, entityName), repositoryClassSource),
            new GeneratedFile(FeaturePaths.ServiceInterface(root, entityName), serviceInterfaceSource),
            new GeneratedFile(FeaturePaths.ServiceClass(root, entityName), serviceClassSource),
            new GeneratedFile(FeaturePaths.EfConfiguration(root, entityName), efConfigurationSource),
        };
        files.AddRange(dtoFiles);
        files.Add(mapperFile);

        GeneratedFileWriter.WriteAll(outputDirectoryPath, files);

        return files;
    }
}
