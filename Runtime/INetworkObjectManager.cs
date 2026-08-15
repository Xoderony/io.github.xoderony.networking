namespace Xoderony.Networking
{
    /// <summary>会话内网络对象生命周期：登记/注销 prefab，并执行入网与离网。</summary>
    public interface INetworkObjectManager
    {
        void RegisterPrefab(NetworkObject prefab);

        void UnregisterPrefab(NetworkObject prefab);

        bool TryGetPrefab(int prefabId, out NetworkObject prefab);

        NetworkObject Spawn(NetworkObject instance);

        void Despawn(NetworkObject networkObject);
    }
}
