using System;
using System.Collections.Generic;
using UnityEngine;

namespace Xoderony.Networking
{
    /// <summary>
    /// Session entry: Host/Client lifecycle, ClientId assignment, owns bus and spawn.
    /// </summary>
    public class NetSession : MonoBehaviour
    {
        public const ulong HostClientId = 0;

        private INetTransport _transport;
        private readonly Dictionary<ulong, ulong> _transportToClient = new Dictionary<ulong, ulong>();
        private readonly Dictionary<ulong, ulong> _clientToTransport = new Dictionary<ulong, ulong>();
        private ulong _nextClientId = 1;
        private bool _connected;

        public INetTransport Transport => _transport;
        public NetMessageBus Bus { get; private set; }
        public NetSpawn Spawn { get; private set; }

        public bool IsHost { get; private set; }
        public bool IsConnected => _connected;
        public ulong LocalClientId { get; private set; }

        public event Action Connected;
        public event Action Disconnected;
        public event Action<ulong> ClientConnected;
        public event Action<ulong> ClientDisconnected;

        public void BindTransport(INetTransport transport)
        {
            if (_connected)
            {
                throw new InvalidOperationException("Cannot bind transport while connected.");
            }

            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            EnsureBus();
        }

        public void StartHost()
        {
            if (_transport == null)
            {
                throw new InvalidOperationException("Call BindTransport before StartHost.");
            }

            _transport.PeerConnected += OnTransportPeerConnected;
            _transport.PeerDisconnected += OnTransportPeerDisconnected;
            _transport.DataReceived += OnTransportDataReceived;

            _transport.StartHost();
            IsHost = true;
            LocalClientId = HostClientId;
            _transportToClient[_transport.LocalTransportId] = HostClientId;
            _clientToTransport[HostClientId] = _transport.LocalTransportId;
            _connected = true;
            Connected?.Invoke();
        }

        public void StartClient(ulong remoteAddress)
        {
            if (_transport == null)
            {
                throw new InvalidOperationException("Call BindTransport before StartClient.");
            }

            _transport.PeerConnected += OnTransportPeerConnected;
            _transport.PeerDisconnected += OnTransportPeerDisconnected;
            _transport.DataReceived += OnTransportDataReceived;

            IsHost = false;
            // Loopback may deliver Welcome synchronously inside StartClient.
            _connected = true;
            _transport.StartClient(remoteAddress);
            // LocalClientId assigned on Welcome.
        }

        public void Shutdown()
        {
            if (_transport != null)
            {
                _transport.PeerConnected -= OnTransportPeerConnected;
                _transport.PeerDisconnected -= OnTransportPeerDisconnected;
                _transport.DataReceived -= OnTransportDataReceived;
                _transport.Disconnect();
            }

            Spawn?.ClearLocal();
            _transportToClient.Clear();
            _clientToTransport.Clear();
            _connected = false;
            IsHost = false;
            LocalClientId = 0;
            Disconnected?.Invoke();
        }

        private void Update()
        {
            _transport?.Poll();
        }

        private void OnDestroy()
        {
            Shutdown();
            _transport?.Dispose();
            _transport = null;
        }

        internal void SendRaw(ulong transportPeerId, ArraySegment<byte> data, NetDelivery delivery)
        {
            _transport.Send(transportPeerId, data, delivery);
        }

        internal void SendRawToHost(ArraySegment<byte> data, NetDelivery delivery)
        {
            if (!_clientToTransport.TryGetValue(HostClientId, out var hostTransportId))
            {
                throw new InvalidOperationException("Host transport peer is not mapped.");
            }

            _transport.Send(hostTransportId, data, delivery);
        }

        internal void BroadcastRaw(ArraySegment<byte> data, NetDelivery delivery, ulong excludeTransportId)
        {
            foreach (var pair in _transportToClient)
            {
                if (pair.Key == _transport.LocalTransportId || pair.Key == excludeTransportId)
                {
                    continue;
                }

                _transport.Send(pair.Key, data, delivery);
            }
        }

        internal bool TryGetTransportId(ulong clientId, out ulong transportId) =>
            _clientToTransport.TryGetValue(clientId, out transportId);

        private void EnsureBus()
        {
            if (Bus != null)
            {
                return;
            }

            Bus = new NetMessageBus(this);
            Spawn = new NetSpawn(this);
            Bus.Register(NetMessageType.Welcome, OnWelcome);
            Bus.Register(NetMessageType.Spawn, Spawn.OnSpawnMessage);
            Bus.Register(NetMessageType.Despawn, Spawn.OnDespawnMessage);
            Bus.Register(NetMessageType.EntityState, Spawn.OnEntityStateMessage);
        }

        private void OnTransportPeerConnected(ulong transportPeerId)
        {
            if (!IsHost)
            {
                _transportToClient[transportPeerId] = HostClientId;
                _clientToTransport[HostClientId] = transportPeerId;
                return;
            }

            var clientId = _nextClientId++;
            _transportToClient[transportPeerId] = clientId;
            _clientToTransport[clientId] = transportPeerId;

            var payload = new NetBuffer(16);
            payload.WriteULong(clientId);
            Bus.SendRawToTransportPeer(transportPeerId, NetMessageType.Welcome, HostClientId, payload.AsSegment(), NetDelivery.Reliable);

            ClientConnected?.Invoke(clientId);
            Spawn.SendSnapshotTo(transportPeerId);
        }

        private void OnTransportPeerDisconnected(ulong transportPeerId)
        {
            if (!_transportToClient.TryGetValue(transportPeerId, out var clientId))
            {
                return;
            }

            _transportToClient.Remove(transportPeerId);
            _clientToTransport.Remove(clientId);

            if (IsHost)
            {
                Spawn.DespawnOwnedBy(clientId);
                ClientDisconnected?.Invoke(clientId);
            }
            else
            {
                Shutdown();
            }
        }

        private void OnTransportDataReceived(ulong transportPeerId, ArraySegment<byte> data, NetDelivery delivery)
        {
            Bus.HandleIncoming(transportPeerId, data, delivery);
        }

        private void OnWelcome(ulong senderClientId, NetBuffer reader)
        {
            LocalClientId = reader.ReadULong();
            Connected?.Invoke();
        }
    }
}
