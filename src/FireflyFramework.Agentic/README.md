# FireflyFramework.Agentic

LLM agent framework — .NET counterpart of `fireflyframework-agentic`
(Python). Provider-agnostic ports for chat models, embeddings, tools,
and memory; an `Agent` loop that drives multi-turn tool use.

## What it provides

| Concept | Type |
|---|---|
| Chat completion | `IChatModel.CompleteAsync` / `StreamAsync` |
| Embeddings | `IEmbeddingModel.EmbedAsync` |
| Tools | `IAgentTool` / `AgentTool<TArgs, TResult>` |
| Memory | `IAgentMemory` (built-in `WindowedMemory`) |
| Agent loop | `Agent.AskAsync(userInput, ct)` (handles tool dispatch) |

## Pluggable providers

Adapters live in sibling NuGet packages:

* `FireflyFramework.Agentic.Adapters.OpenAi`
* `FireflyFramework.Agentic.Adapters.Anthropic`
* `FireflyFramework.Agentic.Adapters.AzureOpenAi`
* `FireflyFramework.Agentic.Adapters.Bedrock`

Each registers an `IChatModel` and an `IEmbeddingModel` you can pick by
configuration. The core module deliberately ships **no** SDK reference,
so apps that never call OpenAI directly (e.g. they only use the
`AgenticBridge` to hit a Python-hosted agent) don't pay for it.

## Quick start

```csharp
services.AddFireflyAgentic()
        .AddAgentTool<GetOrderTool>()
        .AddAgentTool<RefundOrderTool>();

public sealed class OrdersAgentFactory(IChatModel model, IAgentMemory mem, IEnumerable<IAgentTool> tools)
{
    public Agent Build() => new(model, mem, tools, "You are an orders assistant. Use the tools to answer.", maxTurns: 6);
}

var answer = await agent.AskAsync("Refund order 0x42, the customer is unhappy.", ct);
```
