using System;
using System.Collections.Generic;

namespace Xoderony.Networking
{
    /// <summary>
    /// In-process transport for two (or more) <see cref="NetSession"/> instances sharing a room name.
    /// Encode room name with <see cref="RoomAddress"/> for <see cref="INetTransport.StartClient"/>.
    /// </summary>
    public sealed class LoopbackNetTransport : INetTransport
    {
        private static readonly Dictionary<string, Room> Rooms = new Dictionary<string, Room>();
        private static ulong s_nextTransportId = 1;

        private readonly string _roomName;
        private Room _room;
        private bool _running;

        public LoopbackNetTransport(string roomName = "default")
        {
            _roomName = string.IsNullOrEmpty(roomName) ? "default" : roomName;
        }

        public bool IsRunning => _running;
        public bool IsHost { get; private set; }
        public ulong LocalTransportId { get; private set; }

        public event Action<ulong> PeerConnected;
        public event Action<ulong> PeerDisconnected;
        public event Action<ulong, ArraySegment<byte>, NetDelivery> DataReceived;

        public static ulong RoomAddress(string roomName)
        {
            roomName = string.IsNullOrEmpty(roomName) ? "default" : roomName;
            return (ulong)(uint)roomName.GetHashCode();
        }

        public void StartHost()
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

                LocalTransportId = s_nextTransportId++;
                IsHost = true;
                _room = new Room(_roomName, this);
                Rooms[_roomName] = _room;
            }

            _running = true;
        }

        public void StartClient(ulong remoteAddress)
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

                LocalTransportId = s_nextTransportId++;
                IsHost = false;
                _room.AddClient(this);
            }

            _running = true;
            // Notify both sides after leaving the lock.
            var hostId = _room.Host.LocalTransportId;
            PeerConnected?.Invoke(hostId);
            _room.Host.PeerConnected?.Invoke(LocalTransportId);
        }

        public void Disconnect()
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

                if (IsHost)
                {
                    Rooms.Remove(_roomName);
                }
                else
                {
                    room.RemoveClient(this);
                }
            }

            if (IsHost)
            {
                foreach (var client in room.ClientsSnapshot())
                {
                    client._running = false;
                    client.PeerDisconnected?.Invoke(LocalTransportId);
                }
            }
            else
            {
                room.Host.PeerDisconnected?.Invoke(LocalTransportId);
            }

            IsHost = false;
        }

        public void Send(ulong transportPeerId, ArraySegment<byte> data, NetDelivery delivery)
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

            target.DataReceived?.Invoke(LocalTransportId, segment, delivery);
        }

        public void Poll()
        {
            // Loopback delivers synchronously in Send.
        }

        public void Dispose() => Disconnect();

        private sealed class Room
        {
            private readonly List<LoopbackNetTransport> _clients = new List<LoopbackNetTransport>();

            public Room(string name, LoopbackNetTransport host)
            {
                Name = name;
                Host = host;
            }

            public string Name { get; }
            public LoopbackNetTransport Host { get; }

            public void AddClient(LoopbackNetTransport client)
            {
                _clients.Add(client);
            }

            public void RemoveClient(LoopbackNetTransport client)
            {
                _clients.Remove(client);
            }

            public List<LoopbackNetTransport> ClientsSnapshot()
            {
                return new List<LoopbackNetTransport>(_clients);
            }

            public LoopbackNetTransport Find(ulong transportId)
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
