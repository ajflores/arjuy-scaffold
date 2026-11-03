namespace ArjuyScaffold.Models;

/// <summary>
/// One generated source file: a path RELATIVE to the solution root (e.g.
/// 'MyApp.Repository/Interfaces/ICategoryRepository.cs') plus its full content. Every generator
/// returns a list of these so that each public type always lands in its own file, named after
/// the type.
/// </summary>
/// <param name="Path">Relative path using forward slashes, rooted at the solution directory.</param>
/// <param name="Content">Full file content.</param>
public sealed record GeneratedFile(string Path, string Content);
