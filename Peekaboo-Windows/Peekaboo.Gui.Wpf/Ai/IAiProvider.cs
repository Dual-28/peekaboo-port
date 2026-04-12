namespace Peekaboo.Gui.Wpf.Ai;

/// <summary>Role of a chat message.</summary>
public enum ChatRole { System, User, Assistant }

/// <summary>A single chat message, optionally with image data.</summary>
public record ChatMessage(ChatRole Role, string Content, byte[]? ImageData = null);

/// <summary>Complete response from an AI provider.</summary>
public record ChatResponse(string Content, int InputTokens, int OutputTokens);

/// <summary>Streaming chunk from an AI provider.</summary>
public record ChatResponseChunk(string Delta, bool IsFinished);

/// <summary>Represents a tool call requested by the AI.</summary>
public record ToolCallRequest(string Name, string Arguments);

/// <summary>Result of executing a tool.</summary>
public record ToolCallResult(string ToolName, string Result, TimeSpan Duration, bool Success);

/// <summary>Events emitted during agent execution.</summary>
public enum AgentEventKind
{
    Thinking,
    ToolCallStarted,
    ToolCallCompleted,
    AssistantMessage,
    Error,
    Completed,
    Cancelled
}

/// <summary>Event emitted by the agent during task execution.</summary>
public record AgentEvent(
    AgentEventKind Kind,
    string? Content = null,
    string? ToolName = null,
    string? ToolArgs = null,
    string? ToolResult = null,
    TimeSpan? Duration = null,
    int? InputTokens = null,
    int? OutputTokens = null
);

/// <summary>Contract for AI providers (OpenAI, Anthropic, Ollama).</summary>
public interface IAiProvider
{
    Task<ChatResponse> ChatAsync(ChatMessage[] messages, CancellationToken ct = default);
    IAsyncEnumerable<ChatResponseChunk> StreamChatAsync(ChatMessage[] messages, CancellationToken ct = default);
    string ModelName { get; }
    bool SupportsVision { get; }
}
