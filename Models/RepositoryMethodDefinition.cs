using System.Collections.Generic;
using System.ComponentModel;

namespace ArjuyScaffold.Models;

/// <summary>
/// Describes a single custom method to generate on a Repository/IRepository pair, in addition
/// to the CRUD already inherited from IBaseRepository&lt;T&gt;/BaseRepository&lt;T&gt;. Unlike
/// Service methods, Repository methods do NOT wrap the return type in MResult&lt;T&gt; and do
/// NOT use try/catch — that error-handling convention belongs to the Business layer, not
/// Repository, per the real arjuy* codebase (ArjuyTurismo.Repository).
/// </summary>
public sealed class RepositoryMethodDefinition
{
    [Description("Method name in PascalCase ending in 'Async', e.g. 'GetAllWithExcursionsAsync'.")]
    public string Name { get; set; } = string.Empty;

    [Description("Type wrapped by Task<T>, e.g. 'List<Category>', 'Category?', 'bool'. Use 'void' (or leave empty) for a method that only returns Task, no value.")]
    public string ReturnType { get; set; } = string.Empty;

    [Description("Method parameters. Empty list for parameterless methods.")]
    public List<ParameterDefinition> Parameters { get; set; } = new List<ParameterDefinition>();
}
