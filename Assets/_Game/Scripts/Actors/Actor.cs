using Navigation;
using UnityEngine;

namespace Navigation
{
    public class Actor : MonoBehaviour
    {
        public virtual Vector2 NMPosition => Vector2.zero;

        private Transform _tr;

        protected virtual void Awake()
        {
            _tr = transform;
        }

        public virtual void SimulateTick()
        {
            
        }

    }
}