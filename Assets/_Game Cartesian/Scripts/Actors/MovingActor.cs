using UnityEngine;

namespace Navigation
{
    public class MovingActor<T> : Actor where T : MovingActorPropertiesScriptableObject
    {
        [SerializeField] protected T _actorProperties;

        public virtual Vector2 Direction => Vector2.up;

        public T Properties => _actorProperties;

        public override void SimulateTick(float deltaTime, RouteScriptableObject activeRoute)
        {
            base.SimulateTick(deltaTime, activeRoute);

            NMPosition += Direction * (Properties.NMSpeed * deltaTime);
        }
    }
}