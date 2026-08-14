using UnityEngine;

namespace Xoderony.Networking
{
    /// <summary>会话内网络对象复制：登记/注销 prefab、入网/离网、按 id 查找、按帧刷新脏状态。</summary>
    public interface INetworkObjectManager
    {
        void RegisterPrefab(NetworkObject prefab);

        void UnregisterPrefab(NetworkObject prefab);

        bool TryGetPrefab(int prefabId, out NetworkObject prefab);

        bool TryGetSpawned(in NetworkObjectId id, out NetworkObject spawned);

        NetworkObject Spawn(NetworkObject instance);

        void Despawn(NetworkObject networkObject);

        void Flush();
    }
}
