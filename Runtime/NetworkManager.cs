using System;
using System.Collections.Generic;
using UnityEngine;
using Xoderony.Networking.Messaging;
using Xoderony.Networking.Transport;

namespace Xoderony.Networking
{
    /// <summary>
    /// Session entry: Host/Client lifecycle, ClientId assignment, owns messaging and spawn.
    /// </summary>
    public class NetworkManager : MonoBehaviour
    {
        public const ulong ServerClientId = 0;

        private NetworkTransport _transport;
        private readonly Dictionary<ulong, ulong> _transportToClient = new Dictionary<ulong, ulong>();
        private readonly Dictionary<ulong, ulong> _clientToTransport = new Dictionary<ulong, ulong>();
        private readonly BufferWriter _welcomePayload = new BufferWriter(16);
        private ulong _nextClientId = 1;
        private bool _connected;

        public NetworkTransport NetworkTransport => _transport;
        public CustomMessagingManager CustomMessaging { get; private set; }
        public NetworkSpawnManager SpawnManager { get; private set; }

        public bool IsHost { get; private set; }
        public bool IsConnected => _connected;
        public ulong LocalClientId { get; private set; }

        public event Action Connected;
        public event Action Disconnected;
        public event Action<ulong> ClientConnected;
        public event Action<ulong> ClientDisconnected;

        public void BindTransport(NetworkTransport transport)
        {
            if (_connected)
            {
                throw new InvalidOperationException("Cannot bind transport while connected.");
            }

            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            EnsureManagers();
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
            LocalClientId = ServerClientId;
            _transportToClient[_transport.LocalTransportId] = ServerClientId;
            _clientToTransport[ServerClientId] = _transport.LocalTransportId;
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
            // Connected becomes true on Welcome (may arrive synchronously inside StartClient for loopback).
            _transport.StartClient(remoteAddress);
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

            SpawnManager?.ClearLocal();
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

        internal void SendRaw(ulong transportPeerId, ArraySegment<byte> data, NetworkDelivery delivery)
        {
            _transport.Send(transportPeerId, data, delivery);
        }

        internal void SendRawToServer(ArraySegment<byte> data, NetworkDelivery delivery)
        {
            if (!_clientToTransport.TryGetValue(ServerClientId, out var serverTransportId))
            {
                throw new InvalidOperationException("Server transport peer is not mapped.");
            }

            _transport.Send(serverTransportId, data, delivery);
        }

        internal void BroadcastRaw(ArraySegment<byte> data, NetworkDelivery delivery, ulong excludeTransportId)
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

        private void EnsureManagers()
        {
            if (CustomMessaging != null)
            {
                return;
            }

            CustomMessaging = new CustomMessagingManager(this);
            SpawnManager = new NetworkSpawnManager(this);
            CustomMessaging.Register(NetworkMessageType.Welcome, OnWelcome);
            CustomMessaging.Register(NetworkMessageType.Spawn, SpawnManager.OnSpawnMessage);
            CustomMessaging.Register(NetworkMessageType.Despawn, SpawnManager.OnDespawnMessage);
            CustomMessaging.Register(NetworkMessageType.EntityState, SpawnManager.OnEntityStateMessage);
        }

        private void OnTransportPeerConnected(ulong transportPeerId)
        {
            if (!IsHost)
            {
                _transportToClient[transportPeerId] = ServerClientId;
                _clientToTransport[ServerClientId] = transportPeerId;
                return;
            }

            var clientId = _nextClientId++;
            _transportToClient[transportPeerId] = clientId;
            _clientToTransport[clientId] = transportPeerId;

            _welcomePayload.Clear();
            _welcomePayload.WriteULong(clientId);
            CustomMessaging.SendRawToTransportPeer(
                transportPeerId,
                NetworkMessageType.Welcome,
                ServerClientId,
                _welcomePayload.AsSegment(),
                NetworkDelivery.Reliable);

            ClientConnected?.Invoke(clientId);
            SpawnManager.SendSnapshotTo(transportPeerId);
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
                SpawnManager.DespawnOwnedBy(clientId);
                ClientDisconnected?.Invoke(clientId);
            }
            else
            {
                Shutdown();
            }
        }

        private void OnTransportDataReceived(ulong transportPeerId, ArraySegment<byte> data, NetworkDelivery delivery)
        {
            CustomMessaging.HandleIncoming(transportPeerId, data, delivery);
        }

        private void OnWelcome(ulong senderClientId, BufferReader reader)
        {
            LocalClientId = reader.ReadULong();
            _connected = true;
            Connected?.Invoke();
        }
    }
}
