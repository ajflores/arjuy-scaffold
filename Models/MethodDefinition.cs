using System.Collections.Generic;
using System.ComponentModel;

namespace ArjuyScaffold.Models;

/// <summary>
/// Describes a single method to generate on a Service/IService pair.
/// The method always returns MResult&lt;ReturnType&gt; per the arjuy* convention.
/// </summary>
public sealed class MethodDefinition
{
    [Description("Method name in PascalCase, e.g. 'GetById'.")]
    public string Name { get; set; } = string.Empty;

    [Description("Inner type T wrapped by MResult<T>, e.g. 'TicketDto', 'bool', 'List<TicketDto>'.")]
    public string ReturnType { get; set; } = string.Empty;

    [Description("Method parameters. Empty list for parameterless methods.")]
    public List<ParameterDefinition> Parameters { get; set; } = new List<ParameterDefinition>();
}
