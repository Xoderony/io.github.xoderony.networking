namespace Xoderony.Networking {
    /// <summary>负责网络对象实例的创建与释放。</summary>
    public interface INetworkObjectFactory {
        /// <summary>根据指定 Prefab 获取一个网络对象实例。</summary>
        NetworkObject Instantiate(NetworkObject prefab);

        /// <summary>释放不再使用的网络对象实例。</summary>
        void Release(NetworkObject instance);
    }
}
