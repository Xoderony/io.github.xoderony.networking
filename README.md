# io.github.xoderony.networking

Lightweight Unity networking core for peer-to-peer Distributed Authority:

- Transport-neutral peer sessions and byte message routing
- Network object identity, Spawn/Despawn, prefab lookup and late-join snapshots
- Explicit lifecycle and resolver contracts for project-owned extensions
- Buffer and unmanaged serialization primitives
- No built-in RPC, NetworkVariable, gameplay state policy, NGO or Steamworks dependency

## Install

Add as a local UPM package or git dependency:

```json
"io.github.xoderony.networking": "file:../io.github.xoderony.networking"
```

Unity 6 (`6000.0`+) recommended.

## Core composition

```csharp
using Xoderony.Networking;
using Xoderony.Networking.Serialization;
using Xoderony.Networking.Transport;

INetworkTransport transport = CreateTransport();
var networkManager = new NetworkManager(transport);
var objectFactory = new InstantiateNetworkObjectFactory();
var objectManager = new NetworkObjectManager(networkManager, objectFactory);

objectManager.RegisterPrefab(prefab);
networkManager.Start();
```

The caller owns the update and shutdown lifecycle:

```csharp
networkManager.Poll();
networkManager.Stop();
```

## Object extensions

Derive from `NetworkObject` to define project-specific Spawn snapshot data:

```csharp
public sealed class ProjectNetworkObject : NetworkObject
{
    protected override void OnSerializeSnapshot(ref BufferWriter writer)
    {
        // Project-owned layout.
    }

    protected override void OnDeserializeSnapshot(ref BufferReader reader)
    {
        // Must consume the same layout.
    }
}
```

`NetworkObjectManager` also implements:

- `INetworkObjectEvents`: `Spawned` and `Despawning`
- `INetworkObjectResolver`: resolves an object by `NetworkObjectId`

Project modules register their own byte messages through `INetworkManager`. Application message types start at `NetworkMessageType.User`. RPC, variable replication, batching, update cadence and ownership policy belong to the project or an optional package built on this core.

## Serialization

All payload APIs use `ReadOnlySpan<byte>`. Build payloads with `BufferWriter` and read them with `BufferReader`.

`Serializer<T>` and `Deserializer<T>` provide default raw-memory encoding for unmanaged values. That default requires matching builds, layouts and endianness; override both delegates when a value needs a stable field protocol.

## Layout

| Namespace | Types |
| --- | --- |
| `Xoderony.Networking` | Session and network-object lifecycle contracts and implementations |
| `Xoderony.Networking.Serialization` | `BufferWriter`, `BufferReader`, `Serializer<T>`, `Deserializer<T>` |
| `Xoderony.Networking.Transport` | `INetworkTransport`, placeholder `LoopbackTransport`, `NetworkDelivery` |
| `Xoderony.Networking.Messaging` | General message delegates, type boundary and payload limit |

## Samples

`Samples~/LoopbackDemo` only demonstrates a derived object snapshot. `LoopbackTransport` is intentionally an unimplemented placeholder.

## Steam

This package ships no Steam transport. The game project provides `JoG.Networking.SteamNetworkTransport` as a consumer-side implementation.

## Status

Foundation (v0.1). Protocol and APIs may change before 1.0.
