using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using UnityEngine;
using Xoderony.Networking.Messaging;
using Xoderony.Networking.Transport;

namespace Xoderony.Networking
{
    /// <summary>
    /// GameObject 上的网络身份。对等模型：生成者即拥有者（DA），id 见 <see cref="NetworkObjectId"/>。
    /// 契约见 <see cref="INetworkObject"/>。位姿在 <see cref="Awake"/> 登记一次，不随 Bind/Unbind 插拔。
    /// </summary>
    [DisallowMultipleComponent]
    public class NetworkObject : MonoBehaviour, INetworkObject
    {
        /// <summary>信封容量：Sequence + 下标/通道 + 数据（上限见 <see cref="NetworkMessageLimits.StateDataCapacity"/>）。</summary>
        private const int StateEnvelopeCapacity = sizeof(uint) + sizeof(byte) + NetworkMessageLimits.StateDataCapacity;

        private const int ChannelCount = 256;

        private INetworkManager _networkManager;
        private readonly byte[] _stateBuffer = new byte[StateEnvelopeCapacity];
        private readonly List<NetworkVariableBase> _variables = new List<NetworkVariableBase>();
        private readonly NetworkMessageHandler[] _handlers = new NetworkMessageHandler[ChannelCount];

        public NetworkObjectId Id { get; internal set; }

        public bool IsSpawned => _networkManager != null;

        [SerializeField] private int _prefabId;

        public int PrefabId
        {
            get => _prefabId;
            internal set => _prefabId = value;
        }

        public bool IsOwner => IsSpawned && Id.PeerId == _networkManager.LocalPeerId;

        protected virtual void Awake()
        {
            _variables.Add(new PoseVariable(this));
        }

        public void Register(NetworkVariableBase variable)
        {
            Debug.Assert(!_variables.Contains(variable), "Variable is already registered.");
            Debug.Assert(_variables.Count < ChannelCount, "Too many network variables.");
            _variables.Add(variable);
        }

        public void Register(byte channel, NetworkMessageHandler handler)
        {
            _handlers[channel] += handler;
        }

        public void Unregister(NetworkVariableBase variable)
        {
            var index = _variables.IndexOf(variable);
            Debug.Assert(index >= 0, "Variable is not registered.");
            _variables.RemoveAt(index);
            variable.IsDirty = false;
        }

        public void Unregister(byte channel, NetworkMessageHandler handler)
        {
            _handlers[channel] -= handler;
        }

        public void SendToOthers(byte channel, in BufferWriter payload, NetworkDelivery delivery = NetworkDelivery.Reliable)
        {
            Debug.Assert(IsSpawned, "Instance is not spawned.");
            _networkManager.SendToOthers(NetworkMessageType.Rpc, WriteMessage(channel, payload.Written), delivery);
        }

        public void SendToAll(byte channel, in BufferWriter payload, NetworkDelivery delivery = NetworkDelivery.Reliable)
        {
            SendToOthers(channel, payload, delivery);
            _handlers[channel]?.Invoke(_networkManager.LocalPeerId, new BufferReader(payload.Written));
        }

        public void SendToPeer(ulong peerId, byte channel, in BufferWriter payload, NetworkDelivery delivery = NetworkDelivery.Reliable)
        {
            Debug.Assert(IsSpawned, "Instance is not spawned.");
            _networkManager.SendToPeer(peerId, NetworkMessageType.Rpc, WriteMessage(channel, payload.Written), delivery);
        }

        internal void Bind(INetworkManager networkManager, in NetworkObjectId id)
        {
            _networkManager = networkManager;
            Id = id;
        }

        internal void Unbind()
        {
            Debug.Assert(IsSpawned, "Instance is not spawned.");

            for (var i = 0; i < _variables.Count; i++)
            {
                _variables[i].IsDirty = false;
            }

            _networkManager = null;
            Id = default;
        }

        internal void WriteSnapshot(ref BufferWriter writer)
        {
            for (var i = 0; i < _variables.Count; i++)
            {
                var variable = _variables[i];
                var lengthOffset = writer.DataLength;
                writer.WriteUShort(0);
                var payloadStart = writer.DataLength;
                variable.Write(ref writer);
                BinaryPrimitives.WriteUInt16LittleEndian(writer.Buffer[lengthOffset..], (ushort)(writer.DataLength - payloadStart));
                variable.IsDirty = false;
            }
        }

        internal void FlushDirty()
        {
            for (var i = 0; i < _variables.Count; i++)
            {
                var variable = _variables[i];
                if (!variable.IsDirty)
                {
                    continue;
                }

                var writer = new BufferWriter(_stateBuffer);
                writer.WriteUInt(Id.Sequence);
                writer.WriteByte((byte)i);
                variable.Write(ref writer);
                _networkManager.SendToOthers(NetworkMessageType.State, writer, NetworkDelivery.Reliable);
                variable.IsDirty = false;
            }
        }

        internal void ApplySnapshot(BufferReader reader)
        {
            for (var i = 0; reader.Remaining > 0; i++)
            {
                var length = reader.ReadUShort();
                var slice = reader.Buffer.Slice(reader.Position, length);
                reader.Position += length;
                Debug.Assert(i < _variables.Count, "Snapshot variable index is out of range.");
                _variables[i].Read(new BufferReader(slice));
            }
        }

        internal void ReceiveState(BufferReader reader)
        {
            var index = reader.ReadByte();
            Debug.Assert(index < _variables.Count, "State variable index is out of range.");
            _variables[index].Read(reader);
        }

        internal void ReceiveRpc(ulong senderPeerId, BufferReader reader)
        {
            var channel = reader.ReadByte();
            _handlers[channel]?.Invoke(senderPeerId, reader);
        }

        private BufferWriter WriteMessage(byte channel, ReadOnlySpan<byte> payload)
        {
            var writer = new BufferWriter(_stateBuffer);
            writer.WriteUInt(Id.Sequence);
            writer.WriteByte(channel);
            writer.WriteBytes(payload);
            return writer;
        }

        private sealed class PoseVariable : NetworkVariableBase
        {
            private readonly NetworkObject _owner;

            public PoseVariable(NetworkObject owner)
            {
                _owner = owner;
            }

            public override void Write(ref BufferWriter writer)
            {
                var t = _owner.transform;
                var position = t.position;
                var rotation = t.rotation;
                writer.WriteFloat(position.x);
                writer.WriteFloat(position.y);
                writer.WriteFloat(position.z);
                writer.WriteFloat(rotation.x);
                writer.WriteFloat(rotation.y);
                writer.WriteFloat(rotation.z);
                writer.WriteFloat(rotation.w);
            }

            public override void Read(BufferReader reader)
            {
                var position = new Vector3(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
                var rotation = new Quaternion(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
                _owner.transform.SetPositionAndRotation(position, rotation);
            }
        }
    }
}
