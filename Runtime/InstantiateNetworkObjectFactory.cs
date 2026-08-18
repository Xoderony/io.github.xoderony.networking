using UnityEngine;

namespace Xoderony.Networking {
    /// <summary>用 <see cref="Object.Instantiate"/> / <see cref="Object.Destroy"/> 创建和销毁。</summary>
    public sealed class InstantiateNetworkObjectFactory : INetworkObjectFactory {
        public NetworkObject Instantiate(NetworkObject prefab) {
            return Object.Instantiate(prefab);
        }

        public void Release(NetworkObject instance) {
            Object.Destroy(instance.gameObject);
        }
    }
}
