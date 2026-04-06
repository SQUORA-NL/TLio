using TLio.Core;
using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Commands.Advanced;

/// <summary>
/// Compares the nodes at FirstPath and SecondPath and writes a comparison-result
/// node at ResultPath.
///
/// Result value (written as a string):
///   "equal"    — nodes are deeply equal
///   "greater"  — first is numerically greater than second
///   "less"     — first is numerically less than second
///   "different" — nodes are not equal and not numerically comparable
///
/// Ported from JLio's Compare command.
/// </summary>
public class Compare<TNode> : CommandBase<TNode>
{
    public override string CommandName => "compare";

    public string? FirstPath { get; set; }
    public string? SecondPath { get; set; }
    public string? ResultPath { get; set; }

    // JLio-compatible aliases (FR-001/FR-002)
    public string? FromPath { set => FirstPath = value; }
    public string? ToPath   { set => SecondPath = value; }

    public Compare() { }

    public Compare(string firstPath, string secondPath, string resultPath)
    {
        FirstPath = firstPath;
        SecondPath = secondPath;
        ResultPath = resultPath;
    }

    public override TLioExecutionResult<TNode> Execute(TNode dataContext, IExecutionContext<TNode> context)
    {
        ResetSuccess();
        var validation = ValidateCommandInstance();
        if (!validation.IsValid)
        {
            validation.ValidationMessages.ForEach(m => context.LogWarning(CoreConstants.CommandExecution, m));
            return TLioExecutionResult<TNode>.Failed(dataContext);
        }

        var firstNodes = context.ItemsFetcher.SelectNodes(FirstPath!, dataContext);
        var secondNodes = context.ItemsFetcher.SelectNodes(SecondPath!, dataContext);

        if (firstNodes.Count == 0)
        {
            context.LogWarning(CoreConstants.CommandExecution, $"{CommandName}: no node at FirstPath '{FirstPath}'");
            return TLioExecutionResult<TNode>.Successful(dataContext);
        }
        if (secondNodes.Count == 0)
        {
            context.LogWarning(CoreConstants.CommandExecution, $"{CommandName}: no node at SecondPath '{SecondPath}'");
            return TLioExecutionResult<TNode>.Successful(dataContext);
        }

        var first = firstNodes.First!;
        var second = secondNodes.First!;

        var comparisonResult = ComputeResult(first, second, context);
        var resultNode = context.NodeAdapter.CreateString(comparisonResult);

        // Write result to ResultPath
        var (parentPath, leafName) = context.ItemsFetcher.SplitParentAndLeaf(ResultPath!);
        context.ItemsFetcher.EnsurePath(ResultPath!, dataContext, context.NodeAdapter);
        var parents = context.ItemsFetcher.SelectNodes(parentPath, dataContext);

        foreach (var parent in parents)
            context.NodeAdapter.SetProperty(parent, leafName, context.NodeAdapter.DeepClone(resultNode));

        context.LogInfo(CoreConstants.CommandExecution, $"{CommandName}: result = '{comparisonResult}'");
        return TLioExecutionResult<TNode>.Successful(dataContext);
    }

    private string ComputeResult(TNode first, TNode second, IExecutionContext<TNode> context)
    {
        if (context.NodeAdapter.DeepEquals(first, second))
            return "equal";

        var firstNum = context.NodeAdapter.TryGetDouble(first);
        var secondNum = context.NodeAdapter.TryGetDouble(second);

        if (firstNum.HasValue && secondNum.HasValue)
        {
            if (firstNum.Value > secondNum.Value) return "greater";
            if (firstNum.Value < secondNum.Value) return "less";
            return "equal";
        }

        return "different";
    }

    public override ValidationResult ValidateCommandInstance()
    {
        var result = new ValidationResult();
        if (string.IsNullOrWhiteSpace(FirstPath)) result.AddError($"{CommandName}: FirstPath is required.");
        if (string.IsNullOrWhiteSpace(SecondPath)) result.AddError($"{CommandName}: SecondPath is required.");
        if (string.IsNullOrWhiteSpace(ResultPath)) result.AddError($"{CommandName}: ResultPath is required.");
        return result;
    }
}
