namespace ArjuyScaffold.Support;

/// <summary>
/// Single source of truth for where each generated file lives (relative to the solution root).
/// Every path ends in '{TypeName}.cs' so file name and public type always match.
/// </summary>
public static class FeaturePaths
{
    public static string Prefix(string? namespaceRoot)
    {
        return string.IsNullOrWhiteSpace(namespaceRoot) ? "Generated" : namespaceRoot.Trim();
    }

    public static string Entity(string? root, string entityName)
    {
        return Prefix(root) + ".Domain/Entities/" + entityName + ".cs";
    }

    public static string RepositoryInterface(string? root, string entityName)
    {
        return Prefix(root) + ".Repository/Interfaces/I" + entityName + "Repository.cs";
    }

    public static string RepositoryClass(string? root, string entityName)
    {
        return Prefix(root) + ".Repository/Repositories/" + entityName + "Repository.cs";
    }

    public static string ServiceInterface(string? root, string entityName)
    {
        return Prefix(root) + ".Business/Interfaces/I" + entityName + "Service.cs";
    }

    public static string ServiceClass(string? root, string entityName)
    {
        return Prefix(root) + ".Business/Services/" + entityName + "Service.cs";
    }

    /// <summary>Folder (plural, so the namespace never collides with the domain type) for an entity's DTOs.</summary>
    public static string ModelFolder(string entityName)
    {
        return Pluralizer.Pluralize(entityName);
    }

    public static string Model(string? root, string entityName, string modelTypeName)
    {
        return Prefix(root) + ".Business/Models/" + ModelFolder(entityName) + "/" + modelTypeName + ".cs";
    }

    public static string EfConfiguration(string? root, string entityName)
    {
        return Prefix(root) + ".Persistence.Database/Configurations/" + entityName + "Configuration.cs";
    }

    public static string MapperlyMapper(string? root, string moduleName)
    {
        return Prefix(root) + ".Business/Mappers/" + moduleName + "Mapper.cs";
    }

    public static string AutoMapperProfile(string? root, string entityName)
    {
        return Prefix(root) + ".Business/Mappings/" + entityName + "Profile.cs";
    }

    public static string ApiConfig(string? root, string className)
    {
        return Prefix(root) + ".Api/ConfigServices/" + className + ".cs";
    }
}
