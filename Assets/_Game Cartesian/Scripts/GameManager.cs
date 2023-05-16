using Gamelogic.Extensions;
using Legacy;
using UnityEngine;
using Unyawn.Utils;

namespace Navigation
{
    public class GameManager : MonoBehaviour
    {
        [SerializeField] private GameSettingsScriptableObject _gameProperties;

        [SerializeField] private RouteScriptableObject _initialRoute;

        [Header("Computed")]
        [SerializeField, ReadOnly] private RouteScriptableObject _activeRoute;
        
        private Simulation _simulation;

        private void Start()
        {
            _simulation = UYServiceLocator.Get<Simulation>();
            LinesComputer.Init(2f, _gameProperties.DrawerUnitLength);

            _initialRoute.Init(_gameProperties.DrawerUnitLength, _gameProperties.ForwardThreshold);
            _activeRoute = _initialRoute.CloneAndInit();

            DataHandler.BuildSetDetails(_activeRoute);
            _activeRoute.ComputeSet(Session.IsMod);

            _simulation.Init(_gameProperties);
            Session.Zoom = _gameProperties.MinZoom;
            
        }

        private void FixedUpdate()
        {
            _simulation.Tick(Time.fixedDeltaTime);
        }
    }
}