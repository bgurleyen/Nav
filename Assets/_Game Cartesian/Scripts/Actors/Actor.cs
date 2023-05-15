using UnityEngine;

namespace Navigation
{
    public class Actor : MonoBehaviour
    {
        public Vector2 NMPosition;
        public float Height;

        private Transform _tr;

        protected virtual void Awake()
        {
            _tr = transform;
        }

        public virtual void SimulateTick(float deltaTime, RouteScriptableObject activeRoute)
        {
            
        }

        public void Place()
        {
            _tr.localPosition = NMPosition.ToDisplay();
        }
    }
}