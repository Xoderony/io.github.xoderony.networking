# io.github.xoderony.networking

Lightweight Unity net sync oriented around **Steam + Distributed Authority**:

- Explicit typed messages (no RPC / NetworkVariable)
- Host as relay + ClientId assignment; owners push entity state
- `INetTransport` with in-process `LoopbackNetTransport` and a Steam stub

## Install

Add as a local UPM package (manifest `file:` path) or git URL once published:

```json
"io.github.xoderony.networking": "file:../io.github.xoderony.networking"
```

Unity 6 (`6000.0`+) recommended. No NGO dependency.

## Quick start (loopback)

```csharp
var host = hostGo.AddComponent<NetSession>();
host.BindTransport(new LoopbackNetTransport("room"));
host.StartHost();
host.Spawn.RegisterPrefab(1, cubePrefab);

var client = clientGo.AddComponent<NetSession>();
client.BindTransport(new LoopbackNetTransport("room"));
client.Spawn.RegisterPrefab(1, cubePrefab);
client.Connected += () => { /* LocalClientId ready */ };
client.StartClient(LoopbackNetTransport.RoomAddress("room"));

// Host allocates NetworkId; ownerClientId may be a remote client.
host.Spawn.Spawn(1, client.LocalClientId, Vector3.zero, Quaternion.identity);
```

Owner sync:

```csharp
public sealed class MyEntity : NetworkEntity
{
    public void Push()
    {
        var buffer = new NetBuffer(8);
        buffer.WriteFloat(transform.position.x);
        SendState(buffer);
    }

    protected override void OnNetworkState(ArraySegment<byte> payload) { /* apply */ }
}
```

Application messages: register handlers on `session.Bus` with types `>= NetMessageType.User`, then `SendToOthers`.

## Layout

| Area | Types |
|------|--------|
| Session | `NetSession` |
| Transport | `INetTransport`, `LoopbackNetTransport`, `SteamNetTransport` (stub) |
| Messaging | `NetMessageBus`, `NetBuffer`, `NetMessageType` |
| Spawn | `NetSpawn`, `NetworkEntity` |

## Samples

**Loopback Demo** (`Samples~/LoopbackDemo`): host + client in one process, spawn a cube owned by the client, owner paints color over `EntityState`.

## Steam

`SteamNetTransport` is intentionally unwired so this package stays free of a Steamworks dependency. Implement `INetTransport` against SteamNetworkingSockets (or extend the stub) in a game/Steam-specific assembly.

## Status

Foundation (v0.1). Protocol and APIs may change before 1.0.
