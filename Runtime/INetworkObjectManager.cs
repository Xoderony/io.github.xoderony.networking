using System;

namespace Xoderony.Networking {
    /// <summary>会话内网络对象管理：Prefab 登记、对象生命周期、事件发布与 id 解析。</summary>
    public interface INetworkObjectManager {
        /// <summary>对象已绑定、完成初始化且可通过 id 解析时发布。</summary>
        event Action<NetworkObject, uint> Spawned;

        /// <summary>对象已移除并解绑、即将交给工厂销毁时发布；id 为解绑前身份。</summary>
        event Action<NetworkObject, uint> Despawned;

        void RegisterPrefab(NetworkObject prefab);

        void UnregisterPrefab(NetworkObject prefab);

        bool TryGetPrefab(int prefabId, out NetworkObject prefab);

        bool TryGetSpawned(uint id, out NetworkObject spawned);

        T Spawn<T>(T prefab, Action<T> initialize = null) where T : NetworkObject;

        void Despawn(NetworkObject networkObject);
    }
}
