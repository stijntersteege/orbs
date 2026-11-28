using System.Diagnostics;
using Orbs.Application.Abstractions;
using Orbs.Application.Accounts;
using Orbs.Domain.Conversations;
using Orbs.Domain.Orbs;

namespace Orbs.Application.Tools;

/// <summary>The tools an orb may use in one reply, ready to hand to a model.</summary>
public sealed class ToolSet(IReadOnlyList<ITool> tools)
{
    public static ToolSet Empty { get; } = new([]);

    public IReadOnlyList<ITool> Tools { get; } = tools;

    public IReadOnlyList<ToolDefinition> Definitions { get; } = [.. tools.Select(ToolCatalog.Define)];

    public ITool? Find(string name) => Tools.FirstOrDefault(t => t.Kind == name);
}

/// <summary>Runs the tools a model asks for and records every call for the audit trail.</summary>
public sealed class ToolRunner(ToolCatalog catalog, CurrentUser currentUser)
{
    /// <summary>A reply may go back and forth with tools this many times before Orbs stops it.</summary>
    public const int MaxRounds = 6;

    /// <summary>The tools the model may see: everything the orb has not blocked.</summary>
    public async Task<ToolSet> ForOrbAsync(Orb orb, bool modelSupportsTools, CancellationToken cancellationToken = default) =>
        modelSupportsTools
            ? new ToolSet([.. (await catalog.ForOrbAsync(orb, cancellationToken)).Where(t => orb.PermissionFor(t.Kind) != ToolAccess.Block)])
            : ToolSet.Empty;

    /// <summary>Runs one call. Failures become an error result the model can read, never an exception.</summary>
    public async Task<ToolCallRecord> RunAsync(ToolSet tools, Orb orb, ToolCall call, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(orb);
        ArgumentNullException.ThrowIfNull(call);
        var timer = Stopwatch.StartNew();
        var tool = tools.Find(call.Name);
        if (tool is null)
        {
            return new ToolCallRecord(call.Name, call.Arguments, $"There is no tool called '{call.Name}'.", isError: true, 0);
        }

        try
        {
            var result = await tool.InvokeAsync(new ToolInvocation(orb.Id, currentUser.UserId, call.Arguments), cancellationToken);
            return new ToolCallRecord(call.Name, call.Arguments, result, isError: false, timer.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return new ToolCallRecord(call.Name, call.Arguments, ex.Message, isError: true, timer.ElapsedMilliseconds);
        }
    }
}
