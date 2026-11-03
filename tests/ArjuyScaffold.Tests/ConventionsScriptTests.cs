using System.Diagnostics;
using System.IO;
using ArjuyScaffold.Support;
using Xunit;

namespace ArjuyScaffold.Tests;

public class ConventionsScriptTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "conv-" + Guid.NewGuid().ToString("N"));

    public ConventionsScriptTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "scripts"));
        File.WriteAllText(Path.Combine(_root, "scripts", "check-conventions.py"), ConventionsScript.Read());
        Directory.CreateDirectory(Path.Combine(_root, "App.Domain", "Entities"));
        File.WriteAllText(Path.Combine(_root, "App.Domain", "App.Domain.csproj"), "<Project />");
    }

    public void Dispose()
    {
        Directory.Delete(_root, true);
    }

    private (int Code, string Output) Run()
    {
        ProcessStartInfo info = new("python3") { RedirectStandardOutput = true, WorkingDirectory = _root };
        info.ArgumentList.Add("-I");
        info.ArgumentList.Add("scripts/check-conventions.py");
        info.ArgumentList.Add(".");
        using Process process = Process.Start(info)!;
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output);
    }

    private void Write(string name, string content)
    {
        File.WriteAllText(Path.Combine(_root, "App.Domain", "Entities", name), content);
    }

    [Fact]
    public void ValidFile_Passes()
    {
        Write("Edition.cs", "namespace App.Domain.Entities;\npublic class Edition { public string Name { get; set; } = \"Edición\"; }\n");
        Assert.Equal(0, Run().Code);
    }

    [Fact]
    public void TwoTypesInOneFile_Fails()
    {
        Write("Edition.cs", "namespace App.Domain.Entities;\npublic class Edition { }\npublic enum Kind { A }\n");
        (int code, string output) = Run();
        Assert.Equal(1, code);
        Assert.Contains("R1", output);
    }

    [Fact]
    public void FileNameMismatch_Fails()
    {
        Write("Other.cs", "namespace App.Domain.Entities;\npublic class Edition { }\n");
        Assert.Contains("R2", Run().Output);
    }

    [Fact]
    public void NamespaceFolderMismatch_Fails()
    {
        Write("Edition.cs", "namespace App.Domain.Models;\npublic class Edition { }\n");
        Assert.Contains("R3", Run().Output);
    }

    [Fact]
    public void SpanishIdentifier_Fails()
    {
        Write("Edition.cs", "namespace App.Domain.Entities;\npublic class Edition { public int Año { get; set; } }\n");
        Assert.Contains("R4", Run().Output);
    }

    [Fact]
    public void NestedAndGenericConstraint_AreNotTopLevelTypes()
    {
        Write("Box.cs", "namespace App.Domain.Entities;\npublic class Box<T> where T : class { public enum Kind { A } public class Inner { } }\n");
        Assert.Equal(0, Run().Code);
    }
}
