using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Client;

/// <summary>
/// Sentinel command produced when a script references a command name not in the registry.
/// Logs a warning and succeeds — mirrors JLio's NotFoundCommand behaviour (no exception).
/// </summary>
internal class NotFoundCommand<TNode> : ICommand<TNode>
{
    private readonly string _commandName;

    public NotFoundCommand(string commandName) => _commandName = commandName;

    public string CommandName => _commandName;

    public TLioExecutionResult<TNode> Execute(TNode dataContext, IExecutionContext<TNode> context)
    {
        context.LogWarning("ScriptEngine", $"Command '{_commandName}' is not registered and will be skipped.");
        return TLioExecutionResult<TNode>.Successful(dataContext);
    }

    public ValidationResult ValidateCommandInstance() => new ValidationResult();
}
