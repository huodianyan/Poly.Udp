# Poly.Udp
a lightweight Udp Server/Client for Unity and any C# (or .Net) project.

## Features
- Zero dependencies
- Minimal core (< 1000 lines)
- Lightweight and fast
- Support Reliable/Ordered transport
- Simple fast byte array pool
- Adapted to all C# game engine

## Installation

## Overview

```csharp

server = new PolyUdpServer();
server.Start(address, port);
server.OnConnectEvent += (connId) => Console.WriteLine($"OnServerConnect: {connId}");
server.OnDisconnectEvent += (connId) => Console.WriteLine($"OnServerDisconnect: {connId}");
server.OnReceiveEvent += (connId, segment, method) =>
{
    var text = Encoding.ASCII.GetString(segment.Array, segment.Offset, segment.Count);
    server.Send(connId, $"Resp: {text}");
};

client = new PolyUdpClient();
client.Connect(address, port);
client.OnConnectEvent += (connId) => { Console.WriteLine($"OnClientConnect: {connId}"); };
client.OnDisconnectEvent += (connId) => Console.WriteLine($"OnClientDisconnect: {connId}");
client.OnReceiveEvent += (connId, segment, method) =>
{
    var text = Encoding.ASCII.GetString(segment.Array, segment.Offset, segment.Count);
    Console.WriteLine($"OnClientRecieve: {connId}, {text}");
};
client.Disconnect();
server.Stop();

```

## License
The software is released under the terms of the [MIT license](./LICENSE.md).

## FAQ

## References

### Documents
- [Understand the Connection State Machine](https://docs-multiplayer.unity3d.com/transport/current/connection-state)

### Projects
- [vis2k/kcp2k](https://github.com/vis2k/kcp2k)
- [RevenantX/LiteNetLib](https://github.com/RevenantX/LiteNetLib)

### Benchmarks
