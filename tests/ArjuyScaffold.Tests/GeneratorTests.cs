using System.Collections.Generic;
using System.Linq;
using ArjuyScaffold.Models;
using ArjuyScaffold.Tools;
using Xunit;

namespace ArjuyScaffold.Tests;

public class GeneratorTests
{
    private static readonly List<FeaturePropertyDefinition> Props = new()
    {
        new FeaturePropertyDefinition { Name = "Name", Type = "string", IsRequired = true, MaxLength = 100 },
        new FeaturePropertyDefinition { Name = "Year", Type = "int" },
    };

    [Fact]
    public void Repository_GeneratesInterfaceAndClassInSeparateFiles()
    {
        List<GeneratedFile> files = RepositoryTool.GenerateRepository("Category", namespaceRoot: "App");

        Assert.Equal(2, files.Count);
        Assert.Contains(files, f => f.Path == "App.Repository/Interfaces/ICategoryRepository.cs" && f.Content.Contains("interface ICategoryRepository") && !f.Content.Contains("class "));
        Assert.Contains(files, f => f.Path == "App.Repository/Repositories/CategoryRepository.cs" && f.Content.Contains("class CategoryRepository") && !f.Content.Contains("interface "));
    }

    [Fact]
    public void Dto_GeneratesOneFilePerModelInPluralFolder()
    {
        List<GeneratedFile> files = DtoTool.GenerateDto("Category", new List<PropertyDefinition> { new() { Name = "Name", Type = "string" } }, "App.Business.Models.Categories", "App");

        Assert.Equal(2, files.Count);
        Assert.Contains(files, f => f.Path == "App.Business/Models/Categories/CategoryResponseModel.cs" && f.Content.Contains("namespace App.Business.Models.Categories;"));
        Assert.Contains(files, f => f.Path == "App.Business/Models/Categories/CategoryRequestModel.cs");
        Assert.All(files, f => Assert.Equal(1, f.Content.Split("public class ").Length - 1));
    }

    [Theory]
    [InlineData("mapperly", "App.Business/Mappers/CategoryMapper.cs")]
    [InlineData("automapper", "App.Business/Mappings/CategoryProfile.cs")]
    public void Mapper_GeneratesOneFilePerEntity(string provider, string expectedPath)
    {
        List<GeneratedFile> files = MapperTool.GenerateMapper(provider, "Category", namespaceRoot: "App");

        GeneratedFile file = Assert.Single(files);
        Assert.Equal(expectedPath, file.Path);
    }

    [Fact]
    public void EfConfiguration_UsesEnglishPlural()
    {
        GeneratedFile file = Assert.Single(EfConfigurationTool.GenerateEfConfiguration("Category", new List<EfPropertyDefinition>()));
        Assert.Contains("ToTable(\"Categories\")", file.Content);
    }

    [Fact]
    public void Feature_GeneratesOneTypePerFileAndServiceInheritsBaseService()
    {
        List<GeneratedFile> files = FeatureTool.GenerateFeature("Edition", Props, "mapperly", namespaceRoot: "App");

        string[] expected =
        {
            "App.Domain/Entities/Edition.cs",
            "App.Repository/Interfaces/IEditionRepository.cs",
            "App.Repository/Repositories/EditionRepository.cs",
            "App.Business/Interfaces/IEditionService.cs",
            "App.Business/Services/EditionService.cs",
            "App.Persistence.Database/Configurations/EditionConfiguration.cs",
            "App.Business/Models/Editions/EditionResponseModel.cs",
            "App.Business/Models/Editions/EditionRequestModel.cs",
            "App.Business/Mappers/EditionMapper.cs",
        };

        Assert.Equal(expected.OrderBy(x => x), files.Select(f => f.Path).OrderBy(x => x));
        GeneratedFile service = files.Single(f => f.Path.EndsWith("/EditionService.cs"));
        Assert.Contains("BaseService<Edition, EditionResponseModel>", service.Content);
        Assert.Contains("using App.Business.Models.Editions;", service.Content);
    }

    [Fact]
    public void SolutionScaffold_PinsPkcsToFixedVersionForVulnerability()
    {
        Assert.Equal("System.Security.Cryptography.Pkcs", SolutionScaffoldTool.PkcsPackageName);
        Assert.True(System.Version.Parse(SolutionScaffoldTool.PkcsPackageVersion) >= new System.Version(10, 0, 14));
    }
}
