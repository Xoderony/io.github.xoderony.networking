using System;
using UnityEngine;
using Xoderony.Networking.Messaging;
using Xoderony.Networking.Transport;

namespace Xoderony.Networking
{
    /// <summary>
    /// GameObject 上的网络身份。对等模型：生成者即拥有者（DA），id 见 <see cref="NetworkObjectId"/>。
    /// 拥有者可推送状态（<see cref="SendState"/>），远端经 <see cref="OnNetworkState"/> 接收。
    /// </summary>
    [DisallowMultipleComponent]
    public class NetworkObject : MonoBehaviour
    {
        /// <summary>状态信封容量：LocalId + 状态数据（上限见 <see cref="NetworkMessageLimits.StateDataCapacity"/>）。</summary>
        private const int StateEnvelopeCapacity = sizeof(uint) + NetworkMessageLimits.StateDataCapacity;

        private NetworkManager _networkManager;
        private readonly byte[] _stateBuffer = new byte[StateEnvelopeCapacity];

        /// <summary>全局唯一 id，由生成端派生。</summary>
        public NetworkObjectId Id { get; internal set; }

        /// <summary>拥有该对象的对等端 id（DA：恒等于 <see cref="Id"/>.PeerId）。</summary>
        public ulong OwnerPeerId => Id.PeerId;

        /// <summary>是否已生成（Bind 后为 true，Unbind 后为 false）。</summary>
        public bool IsSpawned { get; internal set; }

        /// <summary>生成所用的 prefab id。</summary>
        public ushort PrefabId { get; internal set; }

        /// <summary>绑定会话（生成时由 SpawnManager 设置）。</summary>
        public NetworkManager NetworkManager => _networkManager;

        /// <summary>本机是否拥有该对象。</summary>
        public bool IsOwner => IsSpawned && _networkManager != null && Id.PeerId == _networkManager.LocalPeerId;

        internal void Bind(NetworkManager networkManager, NetworkObjectId id, ushort prefabId)
        {
            _networkManager = networkManager;
            Id = id;
            PrefabId = prefabId;
            IsSpawned = true;
            OnNetworkSpawn();
        }

        internal void Unbind()
        {
            if (!IsSpawned)
            {
                return;
            }

            OnNetworkDespawn();
            IsSpawned = false;
            _networkManager = null;
            Id = default;
        }

        internal void ReceiveState(ReadOnlySpan<byte> payload)
        {
            OnNetworkState(payload);
        }

        /// <summary>
        /// 推送本对象状态给所有对端。仅拥有者可调用；状态数据上限见
        /// <see cref="NetworkMessageLimits.StateDataCapacity"/>，超出时由固定容量缓冲直接暴露。
        /// </summary>
        public void SendState(BufferWriter payload, NetworkDelivery delivery = NetworkDelivery.Reliable)
        {
            Debug.Assert(IsOwner, "Only the owner can send entity state.");

            var writer = new BufferWriter(_stateBuffer);
            writer.WriteUInt(Id.LocalId);
            writer.WriteBytes(payload.Written);
            _networkManager.SendToOthers(NetworkMessageType.EntityState, writer, delivery);
        }

        protected virtual void OnNetworkSpawn()
        {
        }

        protected virtual void OnNetworkDespawn()
        {
        }

        /// <summary>接收拥有者推送的状态；payload 仅在调用期间有效。</summary>
        protected virtual void OnNetworkState(ReadOnlySpan<byte> payload)
        {
        }
    }
}
