using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using ModelContextProtocol.Server;

namespace ArjuyScaffold.Tools;

/// <summary>
/// Scaffolds the real 4-project Clean Architecture layout (Domain, Persistence.Database,
/// Repository, Business as separate class library projects, wired with the exact
/// ProjectReference graph) verified against the real solution arjuyTurismo/ArjuyTurismoApp.
/// Unlike the other tools (which only return generated source as strings), this tool executes
/// the .NET CLI (dotnet new classlib / dotnet sln add / dotnet add reference) because creating
/// real projects is not something that can be done by writing a string to a file.
/// </summary>
[McpServerToolType]
public static class SolutionScaffoldTool
{
    private static readonly string[] LayerOrder = { "Domain", "Persistence.Database", "Repository", "Business" };

    private static readonly Dictionary<string, string[]> LayerDependencies = new()
    {
        ["Domain"] = Array.Empty<string>(),
        ["Persistence.Database"] = new[] { "Domain" },
        ["Repository"] = new[] { "Domain", "Persistence.Database" },
        ["Business"] = new[] { "Domain", "Persistence.Database", "Repository" },
    };

    [McpServerTool(Name = "generate_solution_layout")]
    [Description(
        "Scaffolds the real arjuy* Clean Architecture layout as SEPARATE class library projects " +
        "(not folders inside one project), verified against arjuyTurismo/ArjuyTurismoApp: " +
        "{namespaceRoot}.Domain (no dependencies), {namespaceRoot}.Persistence.Database (-> Domain), " +
        "{namespaceRoot}.Repository (-> Domain, Persistence.Database), {namespaceRoot}.Business " +
        "(-> Domain, Persistence.Database, Repository). Creates each project with 'dotnet new " +
        "classlib', deletes its template Class1.cs, adds it to the solution file (.slnx by default " +
        "on this SDK, created with 'dotnet new sln' if none exists yet in solutionRootPath), and " +
        "wires ProjectReference entries via 'dotnet add reference' following the exact graph above. " +
        "If 'apiProjectPath' is provided (an existing .csproj, e.g. a Web API project you already " +
        "created), it is also added to the solution and given a direct ProjectReference to all 4 " +
        "layers (verified real convention: Api references Domain, Persistence.Database, Repository " +
        "AND Business directly, not just Business). When 'includeBaseInfrastructure' is true " +
        "(default), also seeds the base classes every other tool in this server ASSUMES already " +
        "exist — verified against the real files: Domain/Common/Entity.cs, ArgentinaTime.cs, " +
        "MResult.cs; Persistence.Database/AppDbContext.cs (empty, no DbSets yet — those are added " +
        "per entity as documented by generate_feature — but WITH " +
        "modelBuilder.ApplyConfigurationsFromAssembly(...) wired in OnModelCreating, so " +
        "generate_ef_configuration's output needs no manual registration); Repository/Base/" +
        "IBaseRepository.cs + BaseRepository.cs; Business/Base/IBaseService.cs + BaseService.cs " +
        "(note: unlike the rest of this seed, IBaseService is NOT part of the verified real " +
        "ArjuyTurismoApp convention, which has no interface for BaseService — added deliberately " +
        "for symmetry with the Repository pair, by explicit request). Also adds the " +
        "Microsoft.EntityFrameworkCore(.SqlServer) package references AppDbContext/BaseRepository " +
        "need. Idempotent throughout: re-running skips projects, references, packages and seed " +
        "files that already exist instead of failing or overwriting. This tool runs real dotnet CLI " +
        "commands (creates files and folders on disk) — it is the only tool in this server with " +
        "that side effect; every other tool only returns generated source as a string.")]
    public static string GenerateSolutionLayout(
        [Description("Absolute path to the directory where the 4 layer projects (and the solution file, if none exists yet) will live.")] string solutionRootPath,
        [Description("Prefix used for every project and namespace, e.g. 'TestMcp' -> TestMcp.Domain, TestMcp.Business, etc.")] string namespaceRoot,
        [Description("Optional absolute path to an existing .csproj (e.g. a Web API project) to add to the solution and wire with direct ProjectReference to all 4 layers, matching the real Api project convention.")] string? apiProjectPath = null,
        [Description("When true (default), also seeds Entity/ArgentinaTime/MResult (Domain.Common), an empty AppDbContext (Persistence.Database), IBaseRepository/BaseRepository (Repository.Base) and BaseService (Business.Base) — the base classes generate_entity/generate_repository/generate_service assume already exist. Existing files are never overwritten.")] bool includeBaseInfrastructure = true)
    {
        if (string.IsNullOrWhiteSpace(solutionRootPath))
        {
            throw new ArgumentException("solutionRootPath is required.", nameof(solutionRootPath));
        }

        if (string.IsNullOrWhiteSpace(namespaceRoot))
        {
            throw new ArgumentException("namespaceRoot is required.", nameof(namespaceRoot));
        }

        StringBuilder log = new StringBuilder();

        Directory.CreateDirectory(solutionRootPath);

        string solutionFilePath = FindOrCreateSolutionFile(solutionRootPath, namespaceRoot, log);

        Dictionary<string, string> csprojPathByLayer = new();

        foreach (string layer in LayerOrder)
        {
            string projectName = namespaceRoot + "." + layer;
            string projectDirectory = Path.Combine(solutionRootPath, projectName);
            string csprojPath = Path.Combine(projectDirectory, projectName + ".csproj");
            csprojPathByLayer[layer] = csprojPath;

            if (File.Exists(csprojPath))
            {
                log.AppendLine("SKIP (already exists): " + projectName);
            }
            else
            {
                RunDotnet(log, solutionRootPath, "new classlib -n " + projectName + " -o \"" + projectDirectory + "\"");

                string stubFile = Path.Combine(projectDirectory, "Class1.cs");
                if (File.Exists(stubFile))
                {
                    File.Delete(stubFile);
                    log.AppendLine("Removed template stub: " + projectName + "/Class1.cs");
                }
            }

            RunDotnet(log, solutionRootPath, "sln \"" + solutionFilePath + "\" add \"" + csprojPath + "\"");

            foreach (string dependency in LayerDependencies[layer])
            {
                string dependencyCsprojPath = csprojPathByLayer[dependency];
                RunDotnet(log, solutionRootPath, "add \"" + csprojPath + "\" reference \"" + dependencyCsprojPath + "\"");
            }
        }

        if (!string.IsNullOrWhiteSpace(apiProjectPath))
        {
            if (!File.Exists(apiProjectPath))
            {
                log.AppendLine("WARNING: apiProjectPath does not exist, skipped: " + apiProjectPath);
            }
            else
            {
                RunDotnet(log, solutionRootPath, "sln \"" + solutionFilePath + "\" add \"" + apiProjectPath + "\"");

                foreach (string layer in LayerOrder)
                {
                    RunDotnet(log, solutionRootPath, "add \"" + apiProjectPath + "\" reference \"" + csprojPathByLayer[layer] + "\"");
                }
            }
        }

        if (includeBaseInfrastructure)
        {
            SeedBaseInfrastructure(log, namespaceRoot, csprojPathByLayer);
        }

        return log.ToString();
    }

    private static void SeedBaseInfrastructure(StringBuilder log, string namespaceRoot, Dictionary<string, string> csprojPathByLayer)
    {
        string domainDirectory = Path.GetDirectoryName(csprojPathByLayer["Domain"])!;
        string persistenceDirectory = Path.GetDirectoryName(csprojPathByLayer["Persistence.Database"])!;
        string repositoryDirectory = Path.GetDirectoryName(csprojPathByLayer["Repository"])!;
        string businessDirectory = Path.GetDirectoryName(csprojPathByLayer["Business"])!;

        WriteIfMissing(log, Path.Combine(domainDirectory, "Common", "ArgentinaTime.cs"), BuildArgentinaTimeSource(namespaceRoot));
        WriteIfMissing(log, Path.Combine(domainDirectory, "Common", "Entity.cs"), BuildEntitySource(namespaceRoot));
        WriteIfMissing(log, Path.Combine(domainDirectory, "Common", "MResult.cs"), BuildMResultSource(namespaceRoot));
        WriteIfMissing(log, Path.Combine(persistenceDirectory, "AppDbContext.cs"), BuildAppDbContextSource(namespaceRoot));
        WriteIfMissing(log, Path.Combine(repositoryDirectory, "Base", "IBaseRepository.cs"), BuildIBaseRepositorySource(namespaceRoot));
        WriteIfMissing(log, Path.Combine(repositoryDirectory, "Base", "BaseRepository.cs"), BuildBaseRepositorySource(namespaceRoot));
        WriteIfMissing(log, Path.Combine(businessDirectory, "Base", "IBaseService.cs"), BuildIBaseServiceSource(namespaceRoot));
        WriteIfMissing(log, Path.Combine(businessDirectory, "Base", "BaseService.cs"), BuildBaseServiceSource(namespaceRoot));

        RunDotnet(log, persistenceDirectory, "add \"" + csprojPathByLayer["Persistence.Database"] + "\" package Microsoft.EntityFrameworkCore");
        RunDotnet(log, persistenceDirectory, "add \"" + csprojPathByLayer["Persistence.Database"] + "\" package Microsoft.EntityFrameworkCore.SqlServer");
    }

    private static string BuildArgentinaTimeSource(string namespaceRoot)
    {
        return "namespace " + namespaceRoot + ".Domain.Common;\n\n"
            + "public static class ArgentinaTime\n"
            + "{\n"
            + "    private static readonly TimeZoneInfo Zone =\n"
            + "        TimeZoneInfo.FindSystemTimeZoneById(\"Argentina Standard Time\");\n\n"
            + "    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone);\n"
            + "}\n";
    }

    private static string BuildEntitySource(string namespaceRoot)
    {
        return "namespace " + namespaceRoot + ".Domain.Common;\n\n"
            + "public abstract class Entity\n"
            + "{\n"
            + "    public int Id { get; set; }\n\n"
            + "    public DateTime CreatedAt { get; set; } = ArgentinaTime.Now;\n\n"
            + "    public DateTime? UpdatedAt { get; set; }\n\n"
            + "    public bool Deleted { get; set; } = false;\n"
            + "}\n";
    }

    private static string BuildMResultSource(string namespaceRoot)
    {
        return "namespace " + namespaceRoot + ".Domain.Common;\n\n"
            + "public class MResult<T>\n"
            + "{\n"
            + "    public bool IsSuccess { get; private set; }\n\n"
            + "    public T? Data { get; private set; }\n\n"
            + "    public string? Message { get; private set; }\n\n"
            + "    private MResult()\n"
            + "    {\n"
            + "    }\n\n"
            + "    public static MResult<T> Success(T data)\n"
            + "    {\n"
            + "        return new MResult<T> { IsSuccess = true, Data = data };\n"
            + "    }\n\n"
            + "    public static MResult<T> Success()\n"
            + "    {\n"
            + "        return new MResult<T> { IsSuccess = true };\n"
            + "    }\n\n"
            + "    public static MResult<T> Fail(string message)\n"
            + "    {\n"
            + "        return new MResult<T> { IsSuccess = false, Message = message };\n"
            + "    }\n"
            + "}\n";
    }

    private static string BuildAppDbContextSource(string namespaceRoot)
    {
        return "using Microsoft.EntityFrameworkCore;\n\n"
            + "namespace " + namespaceRoot + ".Persistence.Database;\n\n"
            + "public class AppDbContext : DbContext\n"
            + "{\n"
            + "    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)\n"
            + "    {\n"
            + "    }\n\n"
            + "    // TODO: add one DbSet<{Entity}> per entity here as they get generated by generate_feature.\n\n"
            + "    protected override void OnModelCreating(ModelBuilder modelBuilder)\n"
            + "    {\n"
            + "        base.OnModelCreating(modelBuilder);\n\n"
            + "        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);\n"
            + "    }\n"
            + "}\n";
    }

    private static string BuildIBaseRepositorySource(string namespaceRoot)
    {
        return "using " + namespaceRoot + ".Domain.Common;\n\n"
            + "namespace " + namespaceRoot + ".Repository.Base;\n\n"
            + "public interface IBaseRepository<TEntity> where TEntity : Entity\n"
            + "{\n"
            + "    Task<TEntity?> GetByIdAsync(int id);\n"
            + "    Task<List<TEntity>> GetAllAsync();\n"
            + "    Task<TEntity> CreateAsync(TEntity entity);\n"
            + "    Task<TEntity> UpdateAsync(TEntity entity);\n"
            + "    Task DeleteAsync(TEntity entity);\n"
            + "    Task<bool> ExistsAsync(int id);\n"
            + "}\n";
    }

    private static string BuildBaseRepositorySource(string namespaceRoot)
    {
        return "using " + namespaceRoot + ".Domain.Common;\n"
            + "using " + namespaceRoot + ".Persistence.Database;\n"
            + "using Microsoft.EntityFrameworkCore;\n\n"
            + "namespace " + namespaceRoot + ".Repository.Base;\n\n"
            + "public class BaseRepository<TEntity> : IBaseRepository<TEntity> where TEntity : Entity\n"
            + "{\n"
            + "    protected readonly AppDbContext _context;\n"
            + "    protected readonly DbSet<TEntity> _dbSet;\n\n"
            + "    public BaseRepository(AppDbContext context)\n"
            + "    {\n"
            + "        _context = context;\n"
            + "        _dbSet = context.Set<TEntity>();\n"
            + "    }\n\n"
            + "    public virtual async Task<TEntity?> GetByIdAsync(int id)\n"
            + "    {\n"
            + "        return await _dbSet.FirstOrDefaultAsync(x => x.Id == id);\n"
            + "    }\n\n"
            + "    public virtual async Task<List<TEntity>> GetAllAsync()\n"
            + "    {\n"
            + "        return await _dbSet.ToListAsync();\n"
            + "    }\n\n"
            + "    public virtual async Task<TEntity> CreateAsync(TEntity entity)\n"
            + "    {\n"
            + "        entity.CreatedAt = ArgentinaTime.Now;\n"
            + "        _dbSet.Add(entity);\n"
            + "        await _context.SaveChangesAsync();\n"
            + "        return entity;\n"
            + "    }\n\n"
            + "    public virtual async Task<TEntity> UpdateAsync(TEntity entity)\n"
            + "    {\n"
            + "        entity.UpdatedAt = ArgentinaTime.Now;\n"
            + "        _dbSet.Update(entity);\n"
            + "        await _context.SaveChangesAsync();\n"
            + "        return entity;\n"
            + "    }\n\n"
            + "    public virtual async Task DeleteAsync(TEntity entity)\n"
            + "    {\n"
            + "        entity.Deleted = true;\n"
            + "        entity.UpdatedAt = ArgentinaTime.Now;\n"
            + "        _dbSet.Update(entity);\n"
            + "        await _context.SaveChangesAsync();\n"
            + "    }\n\n"
            + "    public virtual async Task<bool> ExistsAsync(int id)\n"
            + "    {\n"
            + "        return await _dbSet.AnyAsync(x => x.Id == id);\n"
            + "    }\n"
            + "}\n";
    }

    private static string BuildIBaseServiceSource(string namespaceRoot)
    {
        return "using " + namespaceRoot + ".Domain.Common;\n\n"
            + "namespace " + namespaceRoot + ".Business.Base;\n\n"
            + "public interface IBaseService<TEntity, TResponseModel>\n"
            + "    where TEntity : Entity\n"
            + "{\n"
            + "    Task<MResult<TResponseModel>> GetByIdAsync(int id);\n"
            + "    Task<MResult<List<TResponseModel>>> GetAllAsync();\n"
            + "    Task<MResult<TResponseModel>> DeleteAsync(int id);\n"
            + "}\n";
    }

    private static string BuildBaseServiceSource(string namespaceRoot)
    {
        return "using " + namespaceRoot + ".Domain.Common;\n"
            + "using " + namespaceRoot + ".Repository.Base;\n"
            + "using Microsoft.Extensions.Logging;\n\n"
            + "namespace " + namespaceRoot + ".Business.Base;\n\n"
            + "public abstract class BaseService<TEntity, TResponseModel> : IBaseService<TEntity, TResponseModel>\n"
            + "    where TEntity : Entity\n"
            + "{\n"
            + "    protected readonly IBaseRepository<TEntity> _repository;\n"
            + "    protected readonly ILogger _logger;\n\n"
            + "    protected BaseService(IBaseRepository<TEntity> repository, ILogger logger)\n"
            + "    {\n"
            + "        _repository = repository;\n"
            + "        _logger = logger;\n"
            + "    }\n\n"
            + "    protected abstract TResponseModel MapToResponse(TEntity entity);\n\n"
            + "    public virtual async Task<MResult<TResponseModel>> GetByIdAsync(int id)\n"
            + "    {\n"
            + "        try\n"
            + "        {\n"
            + "            var entity = await _repository.GetByIdAsync(id);\n\n"
            + "            if (entity == null)\n"
            + "            {\n"
            + "                return MResult<TResponseModel>.Fail(\"Registro no encontrado.\");\n"
            + "            }\n\n"
            + "            return MResult<TResponseModel>.Success(MapToResponse(entity));\n"
            + "        }\n"
            + "        catch (Exception ex)\n"
            + "        {\n"
            + "            _logger.LogError(ex, \"Error en {Service}.GetByIdAsync({Id})\", GetType().Name, id);\n"
            + "            return MResult<TResponseModel>.Fail(ex.Message);\n"
            + "        }\n"
            + "    }\n\n"
            + "    public virtual async Task<MResult<List<TResponseModel>>> GetAllAsync()\n"
            + "    {\n"
            + "        try\n"
            + "        {\n"
            + "            var entities = await _repository.GetAllAsync();\n"
            + "            return MResult<List<TResponseModel>>.Success(entities.Select(MapToResponse).ToList());\n"
            + "        }\n"
            + "        catch (Exception ex)\n"
            + "        {\n"
            + "            _logger.LogError(ex, \"Error en {Service}.GetAllAsync()\", GetType().Name);\n"
            + "            return MResult<List<TResponseModel>>.Fail(ex.Message);\n"
            + "        }\n"
            + "    }\n\n"
            + "    public virtual async Task<MResult<TResponseModel>> DeleteAsync(int id)\n"
            + "    {\n"
            + "        try\n"
            + "        {\n"
            + "            var entity = await _repository.GetByIdAsync(id);\n\n"
            + "            if (entity == null)\n"
            + "            {\n"
            + "                return MResult<TResponseModel>.Fail(\"Registro no encontrado.\");\n"
            + "            }\n\n"
            + "            await _repository.DeleteAsync(entity);\n"
            + "            return MResult<TResponseModel>.Success();\n"
            + "        }\n"
            + "        catch (Exception ex)\n"
            + "        {\n"
            + "            _logger.LogError(ex, \"Error en {Service}.DeleteAsync({Id})\", GetType().Name, id);\n"
            + "            return MResult<TResponseModel>.Fail(ex.Message);\n"
            + "        }\n"
            + "    }\n"
            + "}\n";
    }

    private static void WriteIfMissing(StringBuilder log, string filePath, string content)
    {
        if (File.Exists(filePath))
        {
            log.AppendLine("SKIP (already exists): " + filePath);
            return;
        }

        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(filePath, content);
        log.AppendLine("Created: " + filePath);
    }

    private static string FindOrCreateSolutionFile(string solutionRootPath, string namespaceRoot, StringBuilder log)
    {
        string? existing = Directory.GetFiles(solutionRootPath, "*.slnx")
            .Concat(Directory.GetFiles(solutionRootPath, "*.sln"))
            .FirstOrDefault();

        if (existing != null)
        {
            log.AppendLine("Using existing solution file: " + Path.GetFileName(existing));
            return existing;
        }

        RunDotnet(log, solutionRootPath, "new sln -n " + namespaceRoot + " -o \"" + solutionRootPath + "\"");

        string created = Directory.GetFiles(solutionRootPath, "*.slnx")
            .Concat(Directory.GetFiles(solutionRootPath, "*.sln"))
            .First();

        return created;
    }

    private static void RunDotnet(StringBuilder log, string workingDirectory, string arguments)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using Process process = new Process { StartInfo = startInfo };
        process.Start();
        string stdOut = process.StandardOutput.ReadToEnd();
        string stdErr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        log.AppendLine("$ dotnet " + arguments);

        if (process.ExitCode != 0)
        {
            log.AppendLine("  EXIT CODE " + process.ExitCode);
            if (!string.IsNullOrWhiteSpace(stdOut))
            {
                log.AppendLine("  stdout: " + stdOut.Trim());
            }
            if (!string.IsNullOrWhiteSpace(stdErr))
            {
                log.AppendLine("  stderr: " + stdErr.Trim());
            }
        }
        else
        {
            log.AppendLine("  OK");
        }
    }
}
