using UnityEngine;

namespace Xoderony.Networking.Samples.LoopbackDemo
{
    /// <summary>
    /// Spins up Host + Client in one process via <see cref="LoopbackNetTransport"/>.
    /// Attach to an empty scene object; assign a <see cref="DemoCube"/> prefab with a Renderer.
    /// </summary>
    public sealed class LoopbackDemoBootstrap : MonoBehaviour
    {
        private const string RoomName = "loopback-demo";
        private const ushort CubePrefabId = 1;

        [SerializeField]
        private DemoCube _cubePrefab;

        private NetSession _host;
        private NetSession _client;
        private DemoCube _hostCube;
        private float _nextPaintTime;

        private void Start()
        {
            if (_cubePrefab == null)
            {
                Debug.LogError("LoopbackDemoBootstrap: assign a DemoCube prefab.");
                enabled = false;
                return;
            }

            _host = CreateSession("Host");
            _host.BindTransport(new LoopbackNetTransport(RoomName));
            _host.StartHost();
            _host.Spawn.RegisterPrefab(CubePrefabId, _cubePrefab);

            _client = CreateSession("Client");
            _client.BindTransport(new LoopbackNetTransport(RoomName));
            _client.Connected += OnClientConnected;
            _client.Spawn.RegisterPrefab(CubePrefabId, _cubePrefab);
            _client.StartClient(LoopbackNetTransport.RoomAddress(RoomName));
        }

        private void OnClientConnected()
        {
            _hostCube = (DemoCube)_host.Spawn.Spawn(
                CubePrefabId,
                _client.LocalClientId,
                Vector3.zero,
                Quaternion.identity);
            _hostCube.SetColorAndSync(Color.cyan);

            // Client is owner: paint from client session's entity after it appears.
            _nextPaintTime = Time.time + 1f;
        }

        private void Update()
        {
            if (_hostCube == null || Time.time < _nextPaintTime)
            {
                return;
            }

            _nextPaintTime = Time.time + 1.5f;
            if (!_client.Spawn.Entities.TryGetValue(_hostCube.NetworkId, out var remote))
            {
                return;
            }

            var cube = (DemoCube)remote;
            if (!cube.IsOwner)
            {
                return;
            }

            var color = Color.HSVToRGB(Random.value, 0.7f, 1f);
            cube.SetColorAndSync(color);
        }

        private void OnDestroy()
        {
            if (_client != null)
            {
                _client.Connected -= OnClientConnected;
                _client.Shutdown();
            }

            if (_host != null)
            {
                _host.Shutdown();
            }
        }

        private static NetSession CreateSession(string name)
        {
            var go = new GameObject($"NetSession-{name}");
            return go.AddComponent<NetSession>();
        }
    }
}
