using Legacy;
using UnityEngine;

namespace Navigation
{
    public class MovingActor : Actor
    {
        [SerializeField] private float _nmSpeed = 0.1f;
        
        public virtual Vector2 Direction => Vector2.up;

        public override void SimulateTick(float deltaTime)
        {
            base.SimulateTick(deltaTime);

            NMPosition += Direction * (_nmSpeed * deltaTime);
        }
    }
}