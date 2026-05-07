# FireflyFramework.AgenticBridge

.NET client for **Python-hosted** Firefly agents. Mirrors the
fireflyframework-agentic-bridge Java starter: a thin transport layer
(`IAgenticClient`) that calls into a Python service running the heavier
agent loop (LangChain, LlamaIndex, custom multi-agent orchestration).

## Why a bridge instead of running agents in .NET?

The .NET `Agentic` module covers the common case (single-model tool use,
short conversations, in-process tools). For richer agent topologies —
multi-agent debates, RAG pipelines, vector DB queries, fine-tuned
runtimes — most ecosystems standardize on Python. The bridge lets your
.NET microservices invoke those flows over a stable contract instead of
re-implementing every agent framework primitive in C#.

## Transports

| Transport | Use |
|---|---|
| `Rest` (default) | request/response, JSON over HTTPS |
| `Sse` | server-sent streaming events for long-running agents |
| `WebSocket` | bidirectional (future use) |
| `Queue` | enqueue + poll for offline agents (future use) |

## Quick start

```csharp
services.AddFireflyAgenticBridge(Configuration);

public sealed class TriageController(IAgenticClient client)
{
    public async Task<IActionResult> Diagnose(IncidentReport r, CancellationToken ct)
    {
        var result = await client.InvokeAsync(
            new AgentInvocation("incident-triage", r.Description),
            ct);
        return Ok(result);
    }
}
```

```yaml
Firefly:
  Agentic:
    Bridge:
      Transport: Rest
      BaseUrl: https://agents.internal/
      ApiKey: ${AGENTIC_BRIDGE_KEY}
      RequestTimeout: 00:01:00
      MaxAttempts: 3
```
