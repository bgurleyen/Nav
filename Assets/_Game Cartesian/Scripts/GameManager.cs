using Gamelogic.Extensions;
using UnityEngine;
using Unyawn.Utils;

namespace Navigation
{
    public class GameManager : MonoBehaviour
    {
        [SerializeField] private GamePropertiesScriptableObject _gameProperties;

        [SerializeField] private RouteScriptableObject _initialRoute;

        [Header("Computed"), ReadOnly] public RouteScriptableObject _activeRoute;
        private Simulation _simulation;

        private void Start()
        {
            _simulation = UYServiceLocator.Get<Simulation>();
            LinesComputer.Init(2f, _gameProperties.DrawerUnitLength);

            _initialRoute.Init(_gameProperties.DrawerUnitLength);
            _activeRoute = _initialRoute.CloneAndInit();

            DataHandler.BuildSetDetails(_activeRoute);
            _activeRoute.ComputeSet();

            _simulation.Init(_activeRoute);
            Session.Zoom = _gameProperties.MinZoom;
            
        }

        private void FixedUpdate()
        {
            _simulation.Tick(Time.fixedDeltaTime);
        }


       
    }
}