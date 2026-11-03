using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using ArjuyScaffold.Models;
using ArjuyScaffold.Support;
using ModelContextProtocol.Server;

namespace ArjuyScaffold.Tools;

/// <summary>Emits scripts/check-conventions.py, the validator every arjuy* solution must pass before a slice is "done".</summary>
[McpServerToolType]
public static class ConventionsCheckTool
{
    [McpServerTool(Name = "generate_conventions_check")]
    [Description(
        "Generates scripts/check-conventions.py (ONE top-level type per .cs file, file name = type name, " +
        "namespace = folder, ASCII identifiers), scripts/check-english-identifiers.py (fails with " +
        "file:line:identifier when identifiers contain common Spanish words) and " +
        "scripts/english-allowlist.txt (documented allowlist, never overwritten). Python 3, no dependencies; " +
        "both scripts exit with code 1 on any violation. Run BOTH ('python3 scripts/check-conventions.py' and " +
        "'python3 scripts/check-english-identifiers.py') before reporting a slice as finished. generate_solution_layout already seeds it; use this tool to add " +
        "or refresh it in an existing solution. Returns a list with ONE {path, content} file; writes " +
        "to disk only when 'outputDirectoryPath' (solution root) is provided.")]
    public static List<GeneratedFile> GenerateConventionsCheck(
        [Description("Optional absolute path to the SOLUTION root. When provided, the scripts are written (overwritten) there; the allowlist is kept if it already exists.")] string? outputDirectoryPath = null)
    {
        List<GeneratedFile> files = ConventionsScript.AllFiles();

        GeneratedFileWriter.WriteAll(outputDirectoryPath, files.Where(f => f.Path != ConventionsScript.AllowlistRelativePath || !File.Exists(Path.Combine(outputDirectoryPath ?? string.Empty, f.Path))));
        return files;
    }
}
