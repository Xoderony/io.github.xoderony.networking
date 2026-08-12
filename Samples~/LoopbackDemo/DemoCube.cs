using System;
using UnityEngine;

namespace Xoderony.Networking.Samples
{
    using BufferReader = Xoderony.Networking.BufferReader;
    using BufferWriter = Xoderony.Networking.BufferWriter;
    using NetworkObject = Xoderony.Networking.NetworkObject;

    /// <summary>
    /// Owner paints a color and pushes it through <see cref="NetworkObject.SendState"/>.
    /// </summary>
    public sealed class DemoCube : NetworkObject
    {
        private Renderer _renderer;
        private readonly BufferWriter _colorPayload = new BufferWriter(16);
        private readonly BufferReader _colorReader = new BufferReader(16);

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
        }

        public void SetColorAndSync(Color color)
        {
            ApplyColor(color);
            if (!IsOwner)
            {
                return;
            }

            _colorPayload.Clear();
            _colorPayload.WriteFloat(color.r);
            _colorPayload.WriteFloat(color.g);
            _colorPayload.WriteFloat(color.b);
            _colorPayload.WriteFloat(color.a);
            SendState(_colorPayload);
        }

        protected override void OnNetworkState(ArraySegment<byte> payload)
        {
            _colorReader.Load(payload);
            var color = new Color(
                _colorReader.ReadFloat(),
                _colorReader.ReadFloat(),
                _colorReader.ReadFloat(),
                _colorReader.ReadFloat());
            ApplyColor(color);
        }

        private void ApplyColor(Color color)
        {
            if (_renderer != null)
            {
                _renderer.material.color = color;
            }
        }
    }
}
