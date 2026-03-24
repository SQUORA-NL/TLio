namespace TLio.Core.Models;

public class ValidationResult
{
    public List<string> ValidationMessages { get; } = new();
    public bool IsValid => ValidationMessages.Count == 0;

    public void AddError(string message) => ValidationMessages.Add(message);
}
