using System.ComponentModel;

namespace ArjuyScaffold.Models;

/// <summary>
/// Describes a single parameter of a service method to generate.
/// </summary>
public sealed class ParameterDefinition
{
    [Description("Parameter name in camelCase, e.g. 'ticketId'.")]
    public string Name { get; set; } = string.Empty;

    [Description("C# type name for the parameter, e.g. 'Guid', 'string', 'CreateTicketDto'.")]
    public string Type { get; set; } = string.Empty;
}
