using UnityEngine;
using Xoderony.Networking.Serialization;

namespace Xoderony.Networking.Samples
{
    using NetworkObject = Xoderony.Networking.NetworkObject;

    /// <summary>派生对象快照示例；LoopbackTransport 当前仍是空壳。</summary>
    public sealed class DemoCube : NetworkObject
    {
        private Renderer _renderer;
        private Color _color = Color.white;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            ApplyColor(_color);
        }

        public void SetColor(Color color)
        {
            _color = color;
            ApplyColor(color);
        }

        protected override void OnSerializeSnapshot(ref BufferWriter writer)
        {
            writer.WriteFloat(_color.r);
            writer.WriteFloat(_color.g);
            writer.WriteFloat(_color.b);
            writer.WriteFloat(_color.a);
        }

        protected override void OnDeserializeSnapshot(ref BufferReader reader)
        {
            _color = new Color(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
            ApplyColor(_color);
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
