// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Net.WebSockets;
using System.Text;

namespace FireflyFramework.Client.WebSockets;

/// <summary>
/// Convenience wrapper around <see cref="ClientWebSocket"/>: connect, send text/binary,
/// receive an async stream of frames. Mirrors Java <c>WebSocketClientHelper</c>.
/// </summary>
public sealed class WebSocketClientHelper : IAsyncDisposable
{
    private readonly ClientWebSocket _socket = new();

    public Task ConnectAsync(Uri url, CancellationToken ct = default) => _socket.ConnectAsync(url, ct);

    public WebSocketState State => _socket.State;

    public Task SendTextAsync(string message, CancellationToken ct = default) =>
        _socket.SendAsync(Encoding.UTF8.GetBytes(message), WebSocketMessageType.Text, endOfMessage: true, ct);

    public Task SendBinaryAsync(byte[] payload, CancellationToken ct = default) =>
        _socket.SendAsync(payload, WebSocketMessageType.Binary, endOfMessage: true, ct);

    public async IAsyncEnumerable<WebSocketFrame> ReceiveAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var buffer = new byte[8192];
        while (_socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            var result = await _socket.ReceiveAsync(buffer, ct).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                yield break;
            }

            var copy = new byte[result.Count];
            Array.Copy(buffer, copy, result.Count);
            yield return new WebSocketFrame(result.MessageType, copy, result.EndOfMessage);
        }
    }

    public Task CloseAsync(CancellationToken ct = default) =>
        _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "client closing", ct);

    public ValueTask DisposeAsync()
    {
        _socket.Dispose();
        return ValueTask.CompletedTask;
    }
}

public sealed record WebSocketFrame(WebSocketMessageType Type, byte[] Payload, bool EndOfMessage)
{
    public string AsText() => Encoding.UTF8.GetString(Payload);
}
