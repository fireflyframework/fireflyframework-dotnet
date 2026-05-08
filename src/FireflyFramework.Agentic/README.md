# FireflyFramework.Agentic

In-process LLM agent loop — .NET counterpart of the Python
`fireflyframework-agentic` library. Exposes provider-agnostic ports for
chat completion, embeddings, tools, and memory; ships an `Agent` class
that drives multi-turn tool dispatch.

## What ships in this NuGet package

| Concept | Type |
|---|---|
| Chat completion | `IChatModel.CompleteAsync` / `StreamAsync` |
| Embeddings | `IEmbeddingModel.EmbedAsync` |
| Tools | `IAgentTool` / `AgentTool<TArgs, TResult>` |
| Memory | `IAgentMemory` (built-in `WindowedMemory`) |
| Agent loop | `Agent.AskAsync(userInput, ct)` (handles tool dispatch + memory) |
| Messages | `ChatMessage`, `ChatResponse`, `ToolCall`, `ToolResult`, `MessageRole` |

## What this package does *not* ship

The core deliberately ships **no** SDK reference for any LLM provider.
Adapters that wrap OpenAI, Anthropic, Azure OpenAI, Bedrock, Ollama,
etc. are the consumer's responsibility — the contract is small enough
(`IChatModel.CompleteAsync` returning `ChatResponse`) that wrapping any
SDK is a few dozen lines.

This keeps applications that only need the bridge to a Python-hosted
agent (`FireflyFramework.AgenticBridge`) from pulling in a model SDK
they will never use.

## Quick start

```csharp
// 1. Implement IChatModel for your provider of choice.
public sealed class OpenAiChatModel(OpenAIClient client) : IChatModel
{
    public string ModelId => "gpt-4.1";

    public async Task<ChatResponse> CompleteAsync(IReadOnlyList<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
    {
        var resp = await client.CompleteChatAsync(/* map ... */, ct);
        return new ChatResponse(resp.Content, ToToolCalls(resp), resp.FinishReason);
    }

    public IAsyncEnumerable<string> StreamAsync(IReadOnlyList<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default) { /* ... */ }
}

// 2. Register tools and the model with DI.
services.AddFireflyAgentic()
        .AddSingleton<IChatModel, OpenAiChatModel>()
        .AddAgentTool<GetOrderTool>()
        .AddAgentTool<RefundOrderTool>();

// 3. Drive an Agent.
var agent = new Agent(model, memory, tools, "You are an orders assistant.", maxTurns: 6);
var answer = await agent.AskAsync("Refund order 0x42, the customer is unhappy.", ct);
```

## When to use this vs `FireflyFramework.AgenticBridge`

| Need | Module |
|---|---|
| Single-model tool use, short conversations, in-process tools | **Agentic** |
| Multi-agent topologies, RAG pipelines, vector DB hops, fine-tuned runtimes | **AgenticBridge** (delegate to Python) |
