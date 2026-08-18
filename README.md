# io.github.xoderony.networking

Lightweight Unity networking core for peer-to-peer Distributed Authority:

- Transport-neutral session facts and byte message routing
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
INetworkSession session = CreateSession();
var messageManager = new NetworkMessageManager(transport);
INetworkObjectIdAllocator idAllocator = CreateObjectIdAllocator();
var objectFactory = new InstantiateNetworkObjectFactory();
var objectManager = new NetworkObjectManager(transport, session, messageManager, idAllocator, objectFactory);

objectManager.RegisterPrefab(prefab);
transport.Start();

var instance = objectManager.Spawn(prefab, instance =>
{
    // Initialize project state before the initial snapshot is serialized.
});
```

The caller owns the update and shutdown lifecycle:

```csharp
transport.Poll();
transport.Stop();
objectManager.Dispose();
messageManager.Dispose();
```

`INetworkSession` derives `MemberJoined` from the first transport connection to each peer and `MemberLeft` when that peer disconnects or leaves the logical session (whichever happens first; only once per departure).

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

`INetworkObjectManager` exposes symmetric `Spawned` and `Despawned` events with the object and its session-stable `uint` id, and resolves spawned objects by id. `NetworkObject.OwnerPeerId` identifies the current authority independently from identity. Local ids come from the injected `INetworkObjectIdAllocator`; the project lifecycle guarantees that `Allocate` is called only after local allocation is initialized. `Spawned` runs after the object is bound and initialized; `Despawned` runs after removal and unbinding, immediately before factory destruction. `NetworkObjectManager` uses session `MemberJoined` for snapshot delivery to newly connected peers and session `MemberLeft` for object cleanup.

Local `Spawn` asks `INetworkObjectFactory` to create the registered prefab, invokes the caller's initializer, binds the network identity, sends the initial snapshot, and then publishes `Spawned`.

Project modules register and send their own byte messages through `INetworkMessageManager`. Application message types start at `NetworkMessageType.User`. RPC, variable replication, batching, update cadence and ownership policy belong to the project or an optional package built on this core.

## Serialization

All byte APIs use `ReadOnlySpan<byte>`. Messages sent through `INetworkMessageManager` begin with a one-byte message type followed by the payload. Build messages with `BufferWriter` and read payloads with `BufferReader`.

`Serializer<T>` and `Deserializer<T>` provide default raw-memory encoding for unmanaged values. That default requires matching builds, layouts and endianness; override both delegates when a value needs a stable field protocol.

## Layout

| Namespace | Types |
| --- | --- |
| `Xoderony.Networking` | Session facts, message routing and network-object lifecycle contracts and implementations |
| `Xoderony.Networking.Serialization` | `BufferWriter`, `BufferReader`, `Serializer<T>`, `Deserializer<T>` |
| `Xoderony.Networking.Transport` | `INetworkTransport`, placeholder `LoopbackTransport`, `NetworkDelivery` |
| `Xoderony.Networking.Messaging` | General message delegates, type boundary and payload limit |

## Samples

`Samples~/LoopbackDemo` only demonstrates a derived object snapshot. `LoopbackTransport` is intentionally an unimplemented placeholder.

## Steam

This package ships no Steam transport. The game project provides `JoG.Networking.P2P.SteamNetworkTransport` as a consumer-side implementation.

## Status

Foundation (v0.1). Protocol and APIs may change before 1.0.
