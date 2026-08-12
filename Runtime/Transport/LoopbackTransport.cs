using System;
using System.Collections.Generic;

namespace Xoderony.Networking.Transport
{
    /// <summary>
    /// In-process transport for two (or more) <see cref="T:Xoderony.Networking.NetworkManager"/> instances sharing a room name.
    /// Encode room name with <see cref="RoomAddress"/> for <see cref="NetworkTransport.StartClient"/>.
    /// </summary>
    public sealed class LoopbackTransport : NetworkTransport
    {
        private static readonly Dictionary<string, Room> Rooms = new Dictionary<string, Room>();
        private static ulong s_nextTransportId = 1;

        private readonly string _roomName;
        private Room _room;
        private bool _running;
        private bool _isHost;
        private ulong _localTransportId;

        public LoopbackTransport(string roomName = "default")
        {
            _roomName = string.IsNullOrEmpty(roomName) ? "default" : roomName;
        }

        public override bool IsRunning => _running;
        public override bool IsHost => _isHost;
        public override ulong LocalTransportId => _localTransportId;

        public override event Action<ulong> PeerConnected;
        public override event Action<ulong> PeerDisconnected;
        public override event Action<ulong, ArraySegment<byte>, NetworkDelivery> DataReceived;

        public static ulong RoomAddress(string roomName)
        {
            roomName = string.IsNullOrEmpty(roomName) ? "default" : roomName;
            return (ulong)(uint)roomName.GetHashCode();
        }

        public override void StartHost()
        {
            if (_running)
            {
                throw new InvalidOperationException("Transport already running.");
            }

            lock (Rooms)
            {
                if (Rooms.ContainsKey(_roomName))
                {
                    throw new InvalidOperationException($"Loopback room '{_roomName}' already has a host.");
                }

                _localTransportId = s_nextTransportId++;
                _isHost = true;
                _room = new Room(_roomName, this);
                Rooms[_roomName] = _room;
            }

            _running = true;
        }

        public override void StartClient(ulong remoteAddress)
        {
            if (_running)
            {
                throw new InvalidOperationException("Transport already running.");
            }

            lock (Rooms)
            {
                if (!Rooms.TryGetValue(_roomName, out _room))
                {
                    throw new InvalidOperationException(
                        $"Loopback room '{_roomName}' has no host. Call StartHost on another transport first.");
                }

                _localTransportId = s_nextTransportId++;
                _isHost = false;
                _room.AddClient(this);
            }

            _running = true;
            var hostId = _room.Host.LocalTransportId;
            PeerConnected?.Invoke(hostId);
            _room.Host.PeerConnected?.Invoke(_localTransportId);
        }

        public override void Disconnect()
        {
            if (!_running)
            {
                return;
            }

            _running = false;
            Room room;
            lock (Rooms)
            {
                room = _room;
                _room = null;
                if (room == null)
                {
                    return;
                }

                if (_isHost)
                {
                    Rooms.Remove(_roomName);
                }
                else
                {
                    room.RemoveClient(this);
                }
            }

            if (_isHost)
            {
                foreach (var client in room.ClientsSnapshot())
                {
                    client._running = false;
                    client.PeerDisconnected?.Invoke(_localTransportId);
                }
            }
            else
            {
                room.Host.PeerDisconnected?.Invoke(_localTransportId);
            }

            _isHost = false;
        }

        public override void Send(ulong transportPeerId, ArraySegment<byte> data, NetworkDelivery delivery)
        {
            if (!_running || _room == null)
            {
                throw new InvalidOperationException("Transport is not running.");
            }

            var copy = new byte[data.Count];
            Buffer.BlockCopy(data.Array!, data.Offset, copy, 0, data.Count);
            var segment = new ArraySegment<byte>(copy);

            var target = _room.Find(transportPeerId);
            if (target == null || !target._running)
            {
                return;
            }

            target.DataReceived?.Invoke(_localTransportId, segment, delivery);
        }

        public override void Poll()
        {
            // Loopback delivers synchronously in Send.
        }

        private sealed class Room
        {
            private readonly List<LoopbackTransport> _clients = new List<LoopbackTransport>();

            public Room(string name, LoopbackTransport host)
            {
                Name = name;
                Host = host;
            }

            public string Name { get; }
            public LoopbackTransport Host { get; }

            public void AddClient(LoopbackTransport client) => _clients.Add(client);

            public void RemoveClient(LoopbackTransport client) => _clients.Remove(client);

            public List<LoopbackTransport> ClientsSnapshot() => new List<LoopbackTransport>(_clients);

            public LoopbackTransport Find(ulong transportId)
            {
                if (Host.LocalTransportId == transportId)
                {
                    return Host;
                }

                for (var i = 0; i < _clients.Count; i++)
                {
                    if (_clients[i].LocalTransportId == transportId)
                    {
                        return _clients[i];
                    }
                }

                return null;
            }
        }
    }
}
