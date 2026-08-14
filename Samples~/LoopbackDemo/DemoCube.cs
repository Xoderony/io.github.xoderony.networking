using UnityEngine;

namespace Xoderony.Networking.Samples
{
    using BufferReader = Xoderony.Networking.BufferReader;
    using BufferWriter = Xoderony.Networking.BufferWriter;
    using NetworkObject = Xoderony.Networking.NetworkObject;

    /// <summary>拥有者改颜色后经状态变量推送；变量进入快照。</summary>
    public sealed class DemoCube : NetworkObject
    {
        private Renderer _renderer;
        private Color _color = Color.white;
        private ColorVariable _colorVariable;

        protected override void Awake()
        {
            base.Awake();
            _renderer = GetComponent<Renderer>();
            _colorVariable = new ColorVariable(this);
            Register(_colorVariable);
        }

        private void OnDestroy()
        {
            Unregister(_colorVariable);
        }

        public void SetColorAndSync(Color color)
        {
            ApplyColor(color);
            if (IsOwner)
            {
                _colorVariable.IsDirty = true;
            }
        }

        private void ApplyColor(Color color)
        {
            _color = color;
            if (_renderer != null)
            {
                _renderer.material.color = color;
            }
        }

        private sealed class ColorVariable : NetworkVariableBase
        {
            private readonly DemoCube _owner;

            public ColorVariable(DemoCube owner)
            {
                _owner = owner;
            }

            public override void Write(ref BufferWriter writer)
            {
                var color = _owner._color;
                writer.WriteFloat(color.r);
                writer.WriteFloat(color.g);
                writer.WriteFloat(color.b);
                writer.WriteFloat(color.a);
            }

            public override void Read(BufferReader reader)
            {
                _owner.ApplyColor(new Color(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat()));
            }
        }
    }
}
