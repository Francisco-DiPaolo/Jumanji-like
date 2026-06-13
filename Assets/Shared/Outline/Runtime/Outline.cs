using UnityEngine;
using UnityEngine.Rendering;

namespace OutlineFx
{
    [ExecuteAlways] [DisallowMultipleComponent]
    public abstract class Outline : MonoBehaviour
    {
        internal Renderer _renderer;

        public abstract Color Color { get; set; }

        private void OnEnable()
        {
            _renderer = GetComponent<Renderer>();
            OutlineFxFeature.Register(this);
        }

        private void OnDisable()
        {
            OutlineFxFeature.Unregister(this);
        }
    }
}