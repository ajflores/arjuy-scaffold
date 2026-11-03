namespace ArjuyScaffold.Models;

/// <summary>Result of verify_conventions: 'Output' holds one finding per line plus a summary line.</summary>
public sealed record VerifyConventionsResult(bool Passed, int ExitCode, string Output);
