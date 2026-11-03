using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ArjuyScaffold.Models;

namespace ArjuyScaffold.Support;

/// <summary>Provides the embedded scripts/ templates every generated solution ships with.</summary>
public static class ConventionsScript
{
    public const string RelativePath = "scripts/check-conventions.py";
    public const string EnglishRelativePath = "scripts/check-english-identifiers.py";
    public const string VerifyRelativePath = "scripts/verify-conventions.py";
    public const string AllowlistRelativePath = "scripts/english-allowlist.txt";

    public static string Read()
    {
        return ReadResource("check-conventions.py");
    }

    public static string ReadResource(string resourceName)
    {
        Assembly assembly = typeof(ConventionsScript).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException("Embedded resource not found: " + resourceName);
        using StreamReader reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>All files under scripts/ (the allowlist is only seeded when missing, never overwritten).</summary>
    public static List<GeneratedFile> AllFiles()
    {
        return new List<GeneratedFile>
        {
            new GeneratedFile(RelativePath, Read()),
            new GeneratedFile(EnglishRelativePath, ReadResource("check-english-identifiers.py")),
            new GeneratedFile(VerifyRelativePath, ReadResource("verify-conventions.py")),
            new GeneratedFile(AllowlistRelativePath, ReadResource("english-allowlist.txt")),
        };
    }
}
