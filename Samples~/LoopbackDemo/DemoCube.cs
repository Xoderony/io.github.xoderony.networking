using System;
using UnityEngine;

namespace Xoderony.Networking.Samples.LoopbackDemo
{
    /// <summary>
    /// Owner paints a color and pushes it through <see cref="NetworkEntity.SendState"/>.
    /// </summary>
    public sealed class DemoCube : NetworkEntity
    {
        private Renderer _renderer;
        private Color _color = Color.white;

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

            var buffer = new NetBuffer(16);
            buffer.WriteFloat(color.r);
            buffer.WriteFloat(color.g);
            buffer.WriteFloat(color.b);
            buffer.WriteFloat(color.a);
            SendState(buffer);
        }

        protected override void OnNetworkState(ArraySegment<byte> payload)
        {
            var reader = new NetBuffer();
            reader.Load(payload);
            var color = new Color(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
            ApplyColor(color);
        }

        private void ApplyColor(Color color)
        {
            _color = color;
            if (_renderer != null)
            {
                _renderer.material.color = color;
            }
        }
    }
}
