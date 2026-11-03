using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using ArjuyScaffold.Models;
using ArjuyScaffold.Support;
using ModelContextProtocol.Server;

namespace ArjuyScaffold.Tools;

/// <summary>Runs the embedded verify-conventions.py against a solution and returns its findings.</summary>
[McpServerToolType]
public static class VerifyConventionsTool
{
    [McpServerTool(Name = "verify_conventions")]
    [Description(
        "Verifies an existing solution against ALL arjuy* conventions and returns the findings (requires python3 on PATH). " +
        "Checks: one public type per file, file name = type name, namespace = folder, identifiers in English " +
        "(Spanish-word detector with allowlist scripts/english-allowlist.txt), models in a folder per entity " +
        "(Business/Models/<PluralEntity>/), one mapper per entity, services inherit BaseService, repositories " +
        "inherit BaseRepository, and infrastructure types that do not exist in the MCP template (reported as DEVIATIONS; " +
        "they pass only when registered in SLICES.md under 'Desvios de la skill/MCP'). 'passed' is false when there " +
        "is any finding. The same check runs from the command line: python3 scripts/verify-conventions.py <path> " +
        "(seeded by generate_solution_layout / generate_conventions_check). Paste the output in the slice report.")]
    public static VerifyConventionsResult VerifyConventions(
        [Description("Absolute path to the solution root (the folder containing the .slnx/.sln and the layer projects).")] string solutionPath)
    {
        if (string.IsNullOrWhiteSpace(solutionPath) || !Directory.Exists(solutionPath))
        {
            throw new ArgumentException("solutionPath must be an existing directory.", nameof(solutionPath));
        }

        string scriptDirectory = Path.Combine(Path.GetTempPath(), "arjuy-verify-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scriptDirectory);

        try
        {
            GeneratedFileWriter.WriteAll(scriptDirectory, ConventionsScript.AllFiles());

            ProcessStartInfo startInfo = new ProcessStartInfo("python3")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = solutionPath,
            };
            startInfo.ArgumentList.Add("-I");
            startInfo.ArgumentList.Add(Path.Combine(scriptDirectory, "scripts", "verify-conventions.py"));
            startInfo.ArgumentList.Add(solutionPath);

            using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start python3.");
            string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit();

            return new VerifyConventionsResult(process.ExitCode == 0, process.ExitCode, output);
        }
        finally
        {
            Directory.Delete(scriptDirectory, true);
        }
    }
}
