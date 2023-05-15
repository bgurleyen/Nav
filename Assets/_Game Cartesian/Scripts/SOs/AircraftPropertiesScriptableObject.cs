using UnityEngine;

namespace Navigation
{
    [CreateAssetMenu]
    public class AircraftPropertiesScriptableObject : MovingActorPropertiesScriptableObject
    {
        public float MaxRejoinCloseNMDistance = 10;
    }
}