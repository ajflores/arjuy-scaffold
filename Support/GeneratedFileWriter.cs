using System;
using System.Collections.Generic;
using System.IO;
using ArjuyScaffold.Models;

namespace ArjuyScaffold.Support;

/// <summary>Writes a list of <see cref="GeneratedFile"/> under a root directory (opt-in side effect).</summary>
public static class GeneratedFileWriter
{
    public static void WriteAll(string? rootDirectory, IEnumerable<GeneratedFile> files)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            return;
        }

        string fullRoot = Path.GetFullPath(rootDirectory);

        foreach (GeneratedFile file in files)
        {
            string fullPath = Path.GetFullPath(Path.Combine(fullRoot, file.Path));

            // Defensa: una ruta relativa con '..' no debe escapar del directorio raíz.
            if (!fullPath.StartsWith(fullRoot, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Generated path escapes the output directory: " + file.Path);
            }

            string? directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(fullPath, file.Content);
        }
    }
}
