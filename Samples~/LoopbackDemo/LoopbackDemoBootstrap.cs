using UnityEngine;

namespace Xoderony.Networking.Samples
{
    /// <summary>LoopbackTransport 尚未实现；当前样例仅保留对象快照结构。</summary>
    public sealed class LoopbackDemoBootstrap : MonoBehaviour
    {
        private void Start()
        {
            Debug.LogWarning("LoopbackDemoBootstrap is disabled because LoopbackTransport is a placeholder.");
            enabled = false;
        }
    }
}
