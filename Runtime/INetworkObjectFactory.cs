using UnityEngine;

namespace Xoderony.Networking {
    /// <summary>网络对象的创建与销毁，由外部实现（Instantiate、对象池等）。</summary>
    public interface INetworkObjectFactory {
        NetworkObject Create(NetworkObject prefab);

        void Destroy(NetworkObject instance);
    }
}
