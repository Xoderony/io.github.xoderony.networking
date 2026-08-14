# io.github.xoderony.networking

Lightweight Unity net sync oriented around **Steam + Distributed Authority**:

- Explicit typed messages (no RPC / NetworkVariable / NetworkBehaviour)
- Host as relay + ClientId assignment; owners push entity state
- Industry-standard type names (`NetworkManager`, `NetworkObject`, …) in `Xoderony.Networking`
- `INetworkTransport` with in-process `LoopbackTransport` (the Steam transport lives in the game project)

## Install

Add as a local UPM package (manifest `file:` path) or git URL once published:

```json
"io.github.xoderony.networking": "file:../io.github.xoderony.networking"
```

Unity 6 (`6000.0`+) recommended. No NGO dependency.

## Quick start (loopback)

```csharp
using Xoderony.Networking;
using Xoderony.Networking.Transport;

var host = hostGo.AddComponent<NetworkManager>();
host.BindTransport(new LoopbackTransport("room"));
host.StartHost();
host.SpawnManager.RegisterPrefab(1, cubePrefab);

var client = clientGo.AddComponent<NetworkManager>();
client.BindTransport(new LoopbackTransport("room"));
client.SpawnManager.RegisterPrefab(1, cubePrefab);
client.Connected += () => { /* LocalClientId ready */ };
client.StartClient(LoopbackTransport.RoomAddress("room"));

host.SpawnManager.Spawn(1, client.LocalClientId, Vector3.zero, Quaternion.identity);
```

Owner sync:

```csharp
public sealed class MyObject : NetworkObject
{
    public void Push()
    {
        var writer = new BufferWriter(8);
        writer.WriteFloat(transform.position.x);
        SendState(writer);
    }

    protected override void OnNetworkState(ArraySegment<byte> payload) { /* apply */ }
}
```

Application messages: register on `networkManager.CustomMessaging` with types `>= NetworkMessageType.User`, then `SendToOthers`.

## Layout

| Namespace | Types |
|-----------|--------|
| `Xoderony.Networking` | `NetworkManager`, `INetworkManager`, `NetworkObject`, `NetworkObjectManager`, `INetworkObjectManager`, `INetworkObjectFactory`, `BufferWriter`, `BufferReader` |
| `Xoderony.Networking.Transport` | `INetworkTransport`, `LoopbackTransport`, `NetworkDelivery` |
| `Xoderony.Networking.Messaging` | `NetworkMessageType` |

## Samples

**Loopback Demo** (`Samples~/LoopbackDemo`): host + client in one process, spawn a cube owned by the client, owner paints color over `EntityState`.

## Steam

This package ships no Steam transport so it stays free of a Steamworks dependency. The game project
implements `JoG.Networking.SteamNetworkTransport` on top of Facepunch.Steamworks (SteamNetworkingSockets)
as a consumer-side reference.

## Status

Foundation (v0.1). Protocol and APIs may change before 1.0.
