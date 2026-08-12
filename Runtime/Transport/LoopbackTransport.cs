using System;
using System.Collections.Generic;

namespace Xoderony.Networking.Transport
{
    /// <summary>
    /// 进程内传输，供共享同一房间名的两个（或更多）对等端使用。
    /// 投递为同步零拷贝：Send 直接在调用栈内触发目标 DataReceived。
    /// </summary>
    public sealed class LoopbackTransport : NetworkTransport
    {
        private static readonly Dictionary<string, Room> Rooms = new Dictionary<string, Room>();
        private static ulong s_nextTransportId = 1;

        private readonly string _roomName;
        private Room _room;
        private ulong _transportId;
        private bool _running;
        private bool _isHost;

        public LoopbackTransport(string roomName = "default")
        {
            _roomName = string.IsNullOrEmpty(roomName) ? "default" : roomName;
        }

        public override ulong ServerClientId => 0;

        public override event Action<ulong> PeerConnected;
        public override event Action<ulong> PeerDisconnected;
        public override event NetworkDataReceivedHandler DataReceived;

        public override bool StartServer()
        {
            if (_running)
            {
                return false;
            }

            lock (Rooms)
            {
                if (Rooms.ContainsKey(_roomName))
                {
                    return false;
                }

                _transportId = s_nextTransportId++;
                _isHost = true;
                _room = new Room(_roomName, this);
                Rooms[_roomName] = _room;
            }

            _running = true;
            return true;
        }

        public override bool StartClient()
        {
            if (_running)
            {
                return false;
            }

            lock (Rooms)
            {
                if (!Rooms.TryGetValue(_roomName, out _room))
                {
                    return false;
                }

                _transportId = s_nextTransportId++;
                _isHost = false;
                _room.AddClient(this);
            }

            _running = true;
            PeerConnected?.Invoke(ServerClientId);
            _room.Host.PeerConnected?.Invoke(_transportId);
            return true;
        }

        public override void Send(ulong clientId, ReadOnlySpan<byte> payload, NetworkDelivery networkDelivery)
        {
            var target = Find(clientId);
            if (target == null || !target._running)
            {
                return;
            }

            // 同步调用期间发送方缓冲区必然存活，直接零拷贝转发。
            target.DataReceived?.Invoke(_transportId, payload, networkDelivery);
        }

        public override void DisconnectRemoteClient(ulong clientId)
        {
            var target = Find(clientId);
            if (target == null)
            {
                return;
            }

            target._running = false;
            _room?.RemoveClient(target);
            target.PeerDisconnected?.Invoke(ServerClientId);
        }

        public override void DisconnectLocalClient()
        {
            if (!_running)
            {
                return;
            }

            _running = false;
            _room?.RemoveClient(this);
            PeerDisconnected?.Invoke(ServerClientId);
            _room?.Host.PeerDisconnected?.Invoke(_transportId);
        }

        public override ulong GetCurrentRtt(ulong clientId) => 0;

        public override void Shutdown()
        {
            if (!_running && _room == null)
            {
                return;
            }

            Room room;
            lock (Rooms)
            {
                room = _room;
                _room = null;
                if (room == null)
                {
                    _running = false;
                    return;
                }

                if (_isHost)
                {
                    Rooms.Remove(_roomName);
                    foreach (var client in room.ClientsSnapshot())
                    {
                        if (!client._running)
                        {
                            continue;
                        }

                        client._running = false;
                        client.PeerDisconnected?.Invoke(ServerClientId);
                    }
                }
                else
                {
                    room.RemoveClient(this);
                    if (_running)
                    {
                        room.Host.PeerDisconnected?.Invoke(_transportId);
                    }
                }
            }

            _running = false;
            _isHost = false;
        }

        private LoopbackTransport Find(ulong transportId)
        {
            if (_room == null)
            {
                return null;
            }

            if (!_isHost)
            {
                return transportId == ServerClientId ? _room.Host : null;
            }

            foreach (var client in _room.ClientsSnapshot())
            {
                if (client._transportId == transportId)
                {
                    return client;
                }
            }

            return null;
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
        }
    }
}
