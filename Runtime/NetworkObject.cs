using UnityEngine;
using Xoderony.Networking.Serialization;

namespace Xoderony.Networking {
    /// <summary>
    /// GameObject 上的网络身份。对等模型：生成者即拥有者（DA），id 见 <see cref="NetworkObjectId"/>。
    /// 契约见 <see cref="INetworkObject"/>。
    /// 入网快照扩展覆写 <see cref="OnSerializeSnapshot"/> / <see cref="OnDeserializeSnapshot"/>，只走 Spawn 与晚加入。
    /// </summary>
    [DisallowMultipleComponent]
    public class NetworkObject : MonoBehaviour, INetworkObject {
        private NetworkObjectManager _objectManager;
        [SerializeField] private int _prefabId;

        public NetworkObjectId Id { get; internal set; }

        public bool IsSpawned => _objectManager != null;

        public bool IsOwner => IsSpawned && Id.PeerId == _objectManager.LocalPeerId;

        public int PrefabId {
            get => _prefabId;
            internal set => _prefabId = value;
        }

        /// <summary>
        /// 入网快照附加数据。仅 Spawn 与晚加入调用；派生类型须成对读写相同字节数。
        /// </summary>
        protected virtual void OnSerializeSnapshot(ref BufferWriter writer) {
        }

        /// <summary>对应 <see cref="OnSerializeSnapshot"/>。</summary>
        protected virtual void OnDeserializeSnapshot(ref BufferReader reader) {
        }

        internal void Bind(NetworkObjectManager objectManager, in NetworkObjectId id) {
            _objectManager = objectManager;
            Id = id;
        }

        internal void Unbind() {
            Debug.Assert(IsSpawned, "Instance is not spawned.");

            _objectManager = null;
            Id = default;
        }

        internal void SerializeSnapshot(ref BufferWriter writer) {
            OnSerializeSnapshot(ref writer);
        }

        internal void DeserializeSnapshot(ref BufferReader reader) {
            OnDeserializeSnapshot(ref reader);
        }
    }
}
