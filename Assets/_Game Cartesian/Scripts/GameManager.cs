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
        private Drawer _drawer;

        private void Start()
        {
            _drawer = UYServiceLocator.Get<Drawer>();
            LinesComputer.Init(2f, _gameProperties.DrawerUnitLength);

            _initialRoute.Init(_gameProperties.DrawerUnitLength);
            _activeRoute = _initialRoute.CloneAndInit();
            
            DataHandler.BuildSetDetails(_activeRoute);
            _activeRoute.ComputeSet();

            Session.Zoom = _gameProperties.MinZoom;
        }

        private void FixedUpdate()
        {
            _drawer.SimulateTick(Time.fixedDeltaTime, _activeRoute);
        }
    }
}