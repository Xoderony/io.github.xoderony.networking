using System;

namespace Xoderony.Networking {
    /// <summary>网络对象完成入网或即将离网时发布生命周期事件。</summary>
    public interface INetworkObjectEvents {
        event Action<NetworkObject> Spawned;

        event Action<NetworkObject> Despawning;
    }
}
