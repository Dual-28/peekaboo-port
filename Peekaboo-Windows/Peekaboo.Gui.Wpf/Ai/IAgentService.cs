using System.Text.Json;
using Peekaboo.Core;
using Peekaboo.Gui.Wpf.Sessions;

namespace Peekaboo.Gui.Wpf.Ai;

/// <summary>Interface for the agent service that orchestrates AI-driven automation.</summary>
public interface IAgentService
{
    /// <summary>Execute a natural language task. Returns the final assistant response.</summary>
    Task<string> ExecuteTaskAsync(string task, Action<AgentEvent> onEvent, CancellationToken ct = default);

    /// <summary>Execute a task with conversation history for session resume.</summary>
    Task<string> ExecuteTaskAsync(
        string task,
        IReadOnlyList<ConversationMessage> history,
        Action<AgentEvent> onEvent,
        CancellationToken ct = default);

    /// <summary>Cancel the current task.</summary>
    void Cancel();

    /// <summary>Whether the agent is currently processing.</summary>
    bool IsProcessing { get; }
}
