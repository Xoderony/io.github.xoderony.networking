using UnityEngine;
using UnityEngine.Assertions;
using Xoderony.Networking.Serialization;

namespace Xoderony.Networking {
    /// <summary>
    /// GameObject 上的网络身份：Id 在逻辑会话内稳定且唯一，OwnerPeerId 表示当前权威端。
    /// </summary>
    [DisallowMultipleComponent]
    public class NetworkObject : MonoBehaviour {
        private uint _id;
        private ulong _ownerPeerId;

        [SerializeField] private int _prefabId;

        public uint Id => _id;

        public ulong OwnerPeerId => _ownerPeerId;

        public bool IsSpawned => _id != 0;

        public int PrefabId {
            get => _prefabId;
            internal set => _prefabId = value;
        }

        /// <summary>
        /// 新 Owner 被分配给此对象时调用。所有端都会调用。
        /// </summary>
        protected virtual void OnOwnerAssigned(ulong ownerPeerId) {
        }

        /// <summary>
        /// 原 Owner 从此对象解除时调用。所有端都会调用。
        /// </summary>
        protected virtual void OnOwnerUnassigned(ulong ownerPeerId) {
        }

        /// <summary>
        /// Owner 发生变化时调用，包括首次分配和解除 Owner。所有端都会调用。
        /// </summary>
        protected virtual void OnOwnerChanged(ulong previousOwnerPeerId, ulong newOwnerPeerId) {
        }

        /// <summary>
        /// 入网快照附加数据。仅 Spawn 与晚加入调用；派生类型须成对读写相同字节数。
        /// </summary>
        protected virtual void OnSerializeSnapshot(ref BufferWriter writer) {
        }

        /// <summary>对应 <see cref="OnSerializeSnapshot"/>。</summary>
        protected virtual void OnDeserializeSnapshot(ref BufferReader reader) {
        }

        internal void Bind(uint id, ulong ownerPeerId) {
            Assert.AreEqual(0u, _id, "Network object is already bound.");
            Assert.AreNotEqual(0u, id, "Network object id 0 is reserved.");
            Assert.AreNotEqual(0ul, ownerPeerId, "Network object owner PeerId 0 is invalid.");

            _id = id;
            SetOwner(ownerPeerId);
        }

        internal void SetOwner(ulong newOwnerPeerId) {
            Assert.AreNotEqual(0u, _id, "Network object is not bound.");

            if (_ownerPeerId == newOwnerPeerId) {
                return;
            }

            var previousOwnerPeerId = _ownerPeerId;
            _ownerPeerId = newOwnerPeerId;

            if (previousOwnerPeerId != 0) {
                OnOwnerUnassigned(previousOwnerPeerId);
            }

            if (newOwnerPeerId != 0) {
                OnOwnerAssigned(newOwnerPeerId);
            }

            OnOwnerChanged(previousOwnerPeerId, newOwnerPeerId);
        }

        internal void Unbind() {
            Assert.AreNotEqual(0u, _id, "Network object is not bound.");

            SetOwner(0);

            _id = default;
        }

        internal void SerializeSnapshot(ref BufferWriter writer) {
            OnSerializeSnapshot(ref writer);
        }

        internal void DeserializeSnapshot(ref BufferReader reader) {
            OnDeserializeSnapshot(ref reader);
        }
    }
}
