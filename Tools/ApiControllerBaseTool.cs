using System;
using System.Collections.Generic;
using System.ComponentModel;
using ArjuyScaffold.Models;
using ArjuyScaffold.Support;
using ModelContextProtocol.Server;

namespace ArjuyScaffold.Tools;

/// <summary>Emits the ApiControllerBase that maps MResult ErrorType to HTTP status codes.</summary>
[McpServerToolType]
public static class ApiControllerBaseTool
{
    [McpServerTool(Name = "generate_api_controller_base")]
    [Description(
        "Generates {namespaceRoot}.Api/Controllers/ApiControllerBase.cs: an [ApiController] base class with " +
        "ToActionResult<T>(MResult<T>) that maps MResult.ErrorType to HTTP codes (Validation 400, " +
        "Unauthorized 401, Forbidden 403, NotFound 404, Conflict 409, Locked 423, anything else 500) and " +
        "always returns the MResult as the body. Controllers inherit it and call ToActionResult(result) " +
        "instead of 'return Ok(result)'. Requires the MResult/ErrorType seeded by generate_solution_layout. " +
        "Returns a list with ONE {path, content} file; writes only when 'outputDirectoryPath' is provided.")]
    public static List<GeneratedFile> GenerateApiControllerBase(
        [Description("Root namespace/project prefix, e.g. 'MyApp' (namespace MyApp.Api.Controllers).")] string namespaceRoot,
        [Description("Optional absolute path to the SOLUTION root. When provided, the file is also written under it.")] string? outputDirectoryPath = null)
    {
        if (string.IsNullOrWhiteSpace(namespaceRoot))
        {
            throw new ArgumentException("namespaceRoot is required.", nameof(namespaceRoot));
        }

        List<GeneratedFile> files = new List<GeneratedFile>
        {
            new GeneratedFile(ApiControllerBaseTemplate.RelativePath(namespaceRoot), ApiControllerBaseTemplate.Build(namespaceRoot)),
        };

        GeneratedFileWriter.WriteAll(outputDirectoryPath, files);
        return files;
    }
}
