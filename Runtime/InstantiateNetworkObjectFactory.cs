using UnityEngine;

namespace Xoderony.Networking
{
    /// <summary>用 <see cref="Object.Instantiate"/> / <see cref="Object.Destroy"/> 创建和销毁。</summary>
    public sealed class InstantiateNetworkObjectFactory : INetworkObjectFactory
    {
        public T Create<T>(T prefab) where T : NetworkObject
        {
            return Object.Instantiate(prefab);
        }

        public void Destroy(NetworkObject instance)
        {
            Object.Destroy(instance.gameObject);
        }
    }
}
