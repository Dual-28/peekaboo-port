using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Peekaboo.Gui.Wpf.Sessions;

/// <summary>A single message in a conversation.</summary>
public record ConversationMessage(
    string Role,
    string Content,
    DateTimeOffset Timestamp,
    List<ToolCallInfo>? ToolCalls = null,
    string? Id = null
)
{
    public string Id { get; init; } = Id ?? Guid.NewGuid().ToString("N");

    public ConversationMessage(string role, string content)
        : this(role, content, DateTimeOffset.Now, null) { }
}

/// <summary>Information about a tool call made during a conversation.</summary>
public record ToolCallInfo(
    string Name,
    string? Arguments,
    string? Result,
    string Status = "completed",
    TimeSpan? Duration = null
);

/// <summary>A conversation session with persistent history.</summary>
public class ConversationSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "New Session";
    public string ModelName { get; set; } = "";
    public List<ConversationMessage> Messages { get; set; } = new();
    public string? Summary { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public ConversationSession() { }
    public ConversationSession(string title, string modelName)
    {
        Title = title;
        ModelName = modelName;
    }
}

/// <summary>Summary view of a session for UI display.</summary>
public record SessionSummary(
    string Id,
    string Title,
    string ModelName,
    DateTimeOffset CreatedAt,
    int MessageCount,
    string? Summary
);

/// <summary>Manages conversation sessions with JSON persistence.</summary>
public class SessionStore
{
    public List<ConversationSession> Sessions { get; private set; } = new();
    public ConversationSession? CurrentSession { get; private set; }

    private static string StoragePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Peekaboo", "sessions.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public SessionStore()
    {
        Load();
    }

    public ConversationSession CreateSession(string title = "New Session", string modelName = "")
    {
        var session = new ConversationSession(title, modelName);
        Sessions.Insert(0, session);
        CurrentSession = session;
        Save();
        return session;
    }

    public void SelectSession(string sessionId)
    {
        CurrentSession = Sessions.FirstOrDefault(s => s.Id == sessionId);
    }

    public void AddMessage(ConversationMessage message)
    {
        if (CurrentSession == null) return;
        CurrentSession.Messages.Add(message);
        Save();
    }

    public void UpdateSummary(string summary)
    {
        if (CurrentSession == null) return;
        CurrentSession.Summary = summary;
        Save();
    }

    public void AddToolCall(string name, string? arguments)
    {
        if (CurrentSession == null) return;
        var msg = new ConversationMessage("assistant", $"[TOOL_CALL:{name}]", DateTimeOffset.Now,
            new List<ToolCallInfo> { new ToolCallInfo(name, arguments, null, "running", null) });
        CurrentSession.Messages.Add(msg);
        Save();
    }

    public void UpdateToolCallResult(string name, string? result, TimeSpan? duration, bool success)
    {
        if (CurrentSession == null) return;
        // Find the last tool call message
        for (int i = CurrentSession.Messages.Count - 1; i >= 0; i--)
        {
            var msg = CurrentSession.Messages[i];
            if (msg.ToolCalls != null && msg.ToolCalls.Count > 0)
            {
                var toolCall = msg.ToolCalls[0];
                msg.ToolCalls[0] = toolCall with { Result = result, Status = success ? "completed" : "failed", Duration = duration };
                Save();
                return;
            }
        }
    }

    public void UpdateTitle(string title)
    {
        if (CurrentSession == null) return;
        CurrentSession.Title = title;
        Save();
    }

    public void DeleteSession(string sessionId)
    {
        var session = Sessions.FirstOrDefault(s => s.Id == sessionId);
        if (session == null) return;
        Sessions.Remove(session);
        if (CurrentSession?.Id == sessionId)
            CurrentSession = Sessions.FirstOrDefault();
        Save();
    }

    public List<SessionSummary> GetSummaries() =>
        Sessions.Select(s => new SessionSummary(
            s.Id, s.Title, s.ModelName, s.CreatedAt, s.Messages.Count, s.Summary)).ToList();

    private void Load()
    {
        try
        {
            if (File.Exists(StoragePath))
            {
                var json = File.ReadAllText(StoragePath);
                Sessions = JsonSerializer.Deserialize<List<ConversationSession>>(json, JsonOpts) ?? new();
                CurrentSession = Sessions.FirstOrDefault();
            }
        }
        catch { /* Use empty state */ }
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(StoragePath)!;
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(Sessions, JsonOpts);
            File.WriteAllText(StoragePath, json);
        }
        catch { /* Silently fail */ }
    }
}
