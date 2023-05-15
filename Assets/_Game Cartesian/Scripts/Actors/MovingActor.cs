using UnityEngine;

namespace Navigation
{
    public class MovingActor : Actor
    {
        [SerializeField] protected ActorPropertiesScriptableObject _actorProperties;
        
        public virtual Vector2 Direction => Vector2.up;
        
        public MovingActorPropertiesScriptableObject Properties { get; private set; }
        
        protected override void Awake()
        {
            base.Awake();
            Properties = (MovingActorPropertiesScriptableObject)_actorProperties;
        }

        public override void SimulateTick(float deltaTime)
        {
            base.SimulateTick(deltaTime);

            NMPosition += Direction * (Properties.NMSpeed * deltaTime);
        }
    }
}