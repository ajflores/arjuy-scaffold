using System.IO;
using ArjuyScaffold.Models;
using ArjuyScaffold.Support;
using ArjuyScaffold.Tools;
using Xunit;

namespace ArjuyScaffold.Tests;

public class VerifyConventionsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "verify-" + Guid.NewGuid().ToString("N"));

    public VerifyConventionsTests()
    {
        Directory.CreateDirectory(_root);
        Add("Demo.Domain/Demo.Domain.csproj", "<Project />");
        Add("Demo.Business/Demo.Business.csproj", "<Project />");
        Add("Demo.Repository/Demo.Repository.csproj", "<Project />");
        Add("Demo.Domain/Entities/Edition.cs", "namespace Demo.Domain.Entities;\npublic class Edition { }\n");
    }

    public void Dispose()
    {
        Directory.Delete(_root, true);
    }

    private void Add(string relative, string content)
    {
        string full = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    [Fact]
    public void GeneratedFeature_Passes()
    {
        FeatureTool.GenerateFeature("Edition", new List<FeaturePropertyDefinition> { new() { Name = "Name", Type = "string" } }, "mapperly", namespaceRoot: "Demo", outputDirectoryPath: _root);
        Add("Demo.Business/Demo.Business.csproj", "<Project />");
        Add("Demo.Repository/Demo.Repository.csproj", "<Project />");
        Add("Demo.Persistence.Database/Demo.Persistence.Database.csproj", "<Project />");
        Add("Demo.Business/Base/BaseService.cs", "namespace Demo.Business.Base;\npublic abstract class BaseService<TEntity, TResponse> { }\n");
        Add("Demo.Business/Base/IBaseService.cs", "namespace Demo.Business.Base;\npublic interface IBaseService<TEntity, TResponse> { }\n");

        VerifyConventionsResult result = VerifyConventionsTool.VerifyConventions(_root);

        Assert.True(result.Passed, result.Output);
    }

    [Fact]
    public void Violations_AreReportedWithCodes()
    {
        Add("Demo.Business/Models/EditionModels.cs", "namespace Demo.Business.Models;\npublic class EditionRequestModel { public string Nombre { get; set; } = \"\"; }\n");
        Add("Demo.Business/Common/IClock.cs", "namespace Demo.Business.Common;\npublic interface IClock { }\n");
        Add("Demo.Business/Services/EditionService.cs", "namespace Demo.Business.Services;\npublic class EditionService : ServiceBase { }\n");
        Add("Demo.Business/Mappers/AdminMapper.cs", "namespace Demo.Business.Mappers;\npublic partial class AdminMapper { }\n");
        Add("Demo.Repository/Repositories/EditionRepository.cs", "namespace Demo.Repository.Repositories;\npublic class EditionRepository { }\n");

        VerifyConventionsResult result = VerifyConventionsTool.VerifyConventions(_root);

        Assert.False(result.Passed);
        foreach (string code in new[] { "[V1]", "[V2]", "[V3]", "[V4]", "[V5]", "[V6]", "[V7]" })
        {
            Assert.Contains(code, result.Output);
        }
    }

    [Fact]
    public void DeviationRegisteredInSlices_IsAccepted()
    {
        Add("Demo.Business/Common/IClock.cs", "namespace Demo.Business.Common;\npublic interface IClock { }\n");
        Add("SLICES.md", "## Desvios de la skill/MCP\n| IClock | test | none |\n");

        Assert.True(VerifyConventionsTool.VerifyConventions(_root).Passed);
    }

    [Fact]
    public void ApiControllerBase_MapsEveryErrorType()
    {
        string source = ApiControllerBaseTemplate.Build("Demo");
        foreach (string code in new[] { "400", "401", "403", "404", "409", "423", "500" })
        {
            Assert.Contains("Status" + code, source);
        }
    }

    [Fact]
    public void EnglishComment_IsNonBlockingWarning()
    {
        Add("Demo.Domain/Entities/Commented.cs", "namespace Demo.Domain.Entities;\n// Returns the value when it is set\npublic class Commented { }\n");

        VerifyConventionsResult result = VerifyConventionsTool.VerifyConventions(_root);

        Assert.True(result.Passed, result.Output);
        Assert.Contains("[V8]", result.Output);
    }
}
