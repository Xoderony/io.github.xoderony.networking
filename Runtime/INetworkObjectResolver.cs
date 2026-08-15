namespace Xoderony.Networking {
    /// <summary>按网络 id 解析当前已生成对象。</summary>
    public interface INetworkObjectResolver {
        bool TryGetSpawned(in NetworkObjectId id, out NetworkObject networkObject);
    }
}
