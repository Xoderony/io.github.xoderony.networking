using System;

namespace Xoderony.Networking {
    /// <summary>管理会话内网络 Prefab 和已生成网络对象的生命周期与查询。</summary>
    public interface INetworkObjectManager {
        /// <summary>网络对象完成生成并可通过 id 查询时触发。</summary>
        event Action<NetworkObject> Spawned;

        /// <summary>网络对象已从表移除、尚未解除网络身份时触发。</summary>
        event Action<NetworkObject> Despawned;

        /// <summary>网络对象权威已变更时触发；此时 <see cref="NetworkObject.OwnerPeerId"/> 已是新权威。</summary>
        event Action<NetworkObject> OwnerChanged;

        /// <summary>注册可用于网络生成的 Prefab。</summary>
        void RegisterPrefab(NetworkObject prefab);

        /// <summary>注销已注册的 Prefab。</summary>
        void UnregisterPrefab(NetworkObject prefab);

        /// <summary>尝试获取指定 id 对应的已注册 Prefab。</summary>
        bool TryGetPrefab(int prefabId, out NetworkObject prefab);

        /// <summary>尝试获取指定 id 对应的已生成网络对象。</summary>
        bool TryGetSpawned(uint id, out NetworkObject spawned);

        /// <summary>生成一个由本端拥有的网络对象；initialize 在对象初始网络状态序列化前调用。</summary>
        NetworkObject Spawn(NetworkObject prefab, Action<NetworkObject> initialize = null);

        /// <summary>移除指定的本端拥有网络对象。</summary>
        void Despawn(NetworkObject instance);
    }
}
