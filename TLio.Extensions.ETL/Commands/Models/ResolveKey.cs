namespace TLio.Extensions.ETL.Commands.Models;

/// <summary>
/// A key pair used to match a target token against a reference collection.
/// KeyPath is evaluated on the target token; ReferenceKeyPath on each reference token.
/// </summary>
public class ResolveKey
{
    public string KeyPath { get; set; } = string.Empty;
    public string ReferenceKeyPath { get; set; } = string.Empty;
}
