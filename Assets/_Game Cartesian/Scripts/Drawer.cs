using UnityEngine;
using Unyawn.Utils;

namespace Navigation
{
    public class Drawer : MonoBehaviour
    {
        [SerializeField] private CompassPropertiesScriptableObject _compassProperties;
        [SerializeField] private PlayerAircraft _centerAroundAircraft;
        [SerializeField] private Actor[] _otherActors;

        private Vector3 _compassOrigin;
        
        private MovingActorPropertiesScriptableObject _aircraftProperties => _centerAroundAircraft.Properties;
        
        private void Awake()
        {
            _compassOrigin = transform.position;
            UYServiceLocator.Register(this);
        }

        public void SimulateTick(float deltaTime)
        {
            _centerAroundAircraft.SimulateTick(deltaTime);
            
            foreach (var actor in _otherActors)
            {
                actor.SimulateTick(deltaTime);
            }
            
            // Display
            
            foreach (var actor in _otherActors)
            {
                Place(actor);
            }
            
        }

        private void Place(Actor actor)
        {
            var relativeNmPosition = actor.NMPosition - _centerAroundAircraft.NMPosition;

            var worldCompassPosition = _compassOrigin + relativeNmPosition.ToDisplay(_compassProperties);
            
            actor.Place(worldCompassPosition);
        }
    }
}