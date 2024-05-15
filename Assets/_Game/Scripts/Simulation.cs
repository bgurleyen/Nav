using System;
using System.Collections.Generic;
using UnityEngine;
using Unyawn.Utils;

namespace Navigation
{
    public class Simulation : MonoBehaviour
    {
        [SerializeField] private Aircraft _playerAircraft;

        [SerializeField] private Drawer _drawer;

        public event Action OnOperationMade;
        public Drawer Drawer => _drawer;

        private bool _queueEraseMode;
        private List<ICommand> _cachedCommands;

        private LegsScreen _legsScreen;


        // if we detect that during MOD the current node has passed we reExecute all the commands until that point
        private int _modReExecutedForIndex = -1;

        private void Awake()
        {
            UYServiceLocator.Register(this);
        }

        private void Start()
        {
            _legsScreen = UYServiceLocator.Get<LegsScreen>();
        }

        public void Init()
        {
            DataHandler.BuildSetDetails(Session.ActiveRoute);

            Session.PlayerAircraft = _playerAircraft;
            Session.Routes.FixedPoints = FixedPointsScriptableObject.CreateDemo();

            Session.ActiveRoute.ComputeTrace();

            _playerAircraft.Init(Session.Settings.AirplaneDesignSpeed, 21600);

            _drawer.Display();
            _drawer.RefreshDisplayDistances();
            _drawer.ResetMode();

            Session.IsRunning = true;

        }

        public void EraseMod()
        {
            Session.ModRoute = null;
            Session.IsMod = false;
            _legsScreen.DisplayOperation("0k");
        }

        public void Tick()
        {
            if (Session.IsMod && _queueEraseMode)
            {
                EraseMod();
            }

            _queueEraseMode = false;

            if (Session.IsRunning)
            {
                Session.ActiveRoute.ComputeTrace();
                ComputeMod();

                _playerAircraft.SimulateTick();


                Move.Instance.Tick();
                
                _drawer.Clear();
                _drawer.Display();
            }
        }

        //[ESA] I splitted the Tick() functionality with prefix "Splitted" to accurately
        //simulate the steps of the aircraft at 10x speedup enabled while minimizing performance impact.
        //look at the GameManager.Update() function to see its use.
        public void SplittedTickModHandling()
        {
            if (Session.IsMod && _queueEraseMode)
            {
                EraseMod();
            }
            _queueEraseMode = false;
        }

        public void SplittedTickComputeTrace()
        {
            if (Session.IsRunning)
            {
                Session.ActiveRoute.ComputeTrace();
                ComputeMod();
            }
        }

        public void SplittedTickSimulation()
        {
            if (Session.IsRunning)
            {
                _playerAircraft.SimulateTick();
                Move.Instance.Tick();
            }
        }

        public void SplittedTickDraw()
        {
            if (Session.IsRunning)
            {
                _drawer.Clear();
                _drawer.Display();
            }
        }

        public void QueueEraseMode()
        {
            _queueEraseMode = true;
        }


        private void ComputeMod()
        {
            if (Session.ModRoute == null)
            {
                _modReExecutedForIndex = -1;
                return;
            }

            if (Session.State.LNAV)
            {
                // mod always has to include the last passed active node ( all the passed nodes ) 
                // otherwise it is invalid - will reapply all the commands
                var passedNodeIndex = PositionVirtualNode.PassedNodeIndex;

                if (passedNodeIndex != _modReExecutedForIndex)
                {
                    if (Session.ActiveRoute.Points[passedNodeIndex].ID != Session.ModRoute.Points[passedNodeIndex].ID)
                    {
                        ReExecuteCachedCommands();
                        Debug.Log("Reapplied MOD");
                    }
                    _modReExecutedForIndex = passedNodeIndex;
                }
            }

            if (Session.ModeSetWithPosition != null)
            {
                Destroy(Session.ModeSetWithPosition);
            }
            Debug.Log("ComputeMod 4444444444");
            if (Session.ModeSetWithPosition == null) {
                Debug.Log("IF ComputeMod 4444444444");
                Session.ModeSetWithPosition = Session.ModRoute.CloneAndInit();
            }
            //Session.ModeSetWithPosition = Session.ModRoute.CloneAndInit(); // refactor use the existing modwithposition to avoid reinstantiating
            //Debug.Log("Session.ModeSetWithPosition: "+ Session.ModeSetWithPosition);

            Session.ModeSetWithPosition.AddModPositionNodes();

            Session.ModeSetWithPosition.ComputeTrace();
        }

        public void ReExecuteCachedCommands()
        {
            EraseMod();
            
            for (var i = 0; i < _cachedCommands.Count; i++)
            {
                var command = _cachedCommands[i];
                switch (command)
                {
                    case InsertRelativeCommand relativeCommand:
                        ExecuteInsertRelativeOnMod(relativeCommand);
                        break;
                    case ExecuteShortcutOnModeCommand modeCommand:
                        ExecuteShortcutOnMod(modeCommand);
                        break;
                    case ExecuteAddLinearApproachCommand approachCommand:
                        ExecuteLinearApproachOnMod(approachCommand);
                        break;
                    case ExecuteOriginalInsertOnModCommand approachCommand:
                        ExecuteInsertOriginalOnMod(approachCommand);
                        break;
                }
            }
        }

        public void ExecuteDeleteRestrictions(DeleteRestrictionsCommand command)
        {
            CheckModForOperation();
            _cachedCommands.Add(command);

            _legsScreen.DisplayOperation("ERASE");

            Session.ModRoute.GetPoint(command.NodeId, out var node, out _);
            node.RawAltitude = "";
            node.RawSpeed = 0;

            DataHandler.BuildSetDetails(Session.ModRoute);
        }

        public void ExecuteAddSpeedRegulation(AddSpeedRegulationCommand command)
        {
            CheckModForOperation();
            _cachedCommands.Add(command);

            _legsScreen.DisplayOperation("ERASE");
            Session.ModRoute.GetPoint(command.NodeId, out var node, out _);
            node.RawSpeed = command.Regulation;
            node.IsSpeedModified = true;
            DataHandler.BuildSetDetails(Session.ModRoute);

            OnOperationMade?.Invoke();
        }

        public void ExecuteAddAltitudeRegulation(AddAltitudeRegulationCommand command)
        {
            CheckModForOperation();
            _cachedCommands.Add(command);

            _legsScreen.DisplayOperation("ERASE");
            Session.ModRoute.GetPoint(command.NodeId, out var node, out _);
            //maybe move this to route class to also do some tests?
            node.RawAltitude = command.Regulation;
            node.IsAltitudeModified = true;
            DataHandler.BuildSetDetails(Session.ModRoute);

            OnOperationMade?.Invoke();
        }

        public void ExecuteShortcutOnMod(ExecuteShortcutOnModeCommand command)
        {
            CheckModForOperation();
            _cachedCommands.Add(command);

            Session.ModRoute.GetPoint(command.FromNodeId, out var node, out _);
            _legsScreen.DisplayOperation("ERASE", ToDegreesDisplay(node.RawDegrees));
            Session.ModRoute.ShortcutNodes(command.FromNodeId, command.ToNodeId, out var _);
            DataHandler.BuildSetDetails(Session.ModRoute);

            OnOperationMade?.Invoke();
        }

        private static string ToDegreesDisplay(float value)
        {
            return $"{value:000}°";
        }

        public void ExecuteInsertRelativeOnDirectionOnMod(ExecuteRelativeOnDirectionOnMod command)
        {
            Debug.Log("=execute relative insert on direction=");
            CheckModForOperation();
            _cachedCommands.Add(command);

            Session.ModRoute.AddRelativeNodeOnDirection(command.FromNodeId, command.Distance, command.RelativeNodeId,
                out _, out _);
            DataHandler.BuildSetDetails(Session.ModRoute);
            _legsScreen.DisplayOperation("ERASE");

            OnOperationMade?.Invoke();
        }

        public void ExecuteInsertOriginalOnMod(ExecuteOriginalInsertOnModCommand command)
        {
            Debug.Log("=execute insert original on MOD=");
            CheckModForOperation();
            _cachedCommands.Add(command);
            
            Session.ModRoute.AddDirectToCartesianNodeBefore(command.OriginalRouteNodeId, command.OnTopNodeId);
            
            DataHandler.BuildSetDetails(Session.ModRoute);
            _legsScreen.DisplayOperation(MainScreen.Keywords.ERASE);
            
            OnOperationMade?.Invoke();
        }

        public void ExecuteInsertRelativeOnMod(InsertRelativeCommand command)
        {
            Debug.Log("=execute relative insert simple=");
            CheckModForOperation();
            _cachedCommands.Add(command);

            Session.ModRoute.AddRelativeNodeBefore(command.BeforeNodeId, command.RawDegrees, command.Distance,
                command.RelativeNodeId, out _, true);
            DataHandler.BuildSetDetails(Session.ModRoute);
            _legsScreen.DisplayOperation(MainScreen.Keywords.ERASE);

            OnOperationMade?.Invoke();
        }

        public void ExecuteLinearApproachOnMod(ExecuteAddLinearApproachCommand command)
        {
            Debug.Log("=linear approach=");
            CheckModForOperation();
            _cachedCommands.Add(command);

            Session.ModRoute.CreateLinearApproach(command.ToNodeId, command.Angle);
            DataHandler.BuildSetDetails(Session.ModRoute);
            _legsScreen.DisplayOperation(MainScreen.Keywords.ERASE, ToDegreesDisplay(command.Angle), true);

            OnOperationMade?.Invoke();
        }

        private void CheckModForOperation()
        {
            if (Session.IsMod) return;

            if (Session.ModRoute != null)
            {
                Destroy(Session.ModRoute);
            }

            Debug.Log("Check Mode For Operaiton 555555");
            // if this is the first modification generate a new mod from current active
            Session.ModRoute = Session.ActiveRoute.CloneAndInit();
            //Debug.Log("Session.modeRoute: "+Session.ModRoute);
            for (int i = 0; i < Session.ModRoute.Points.Length; i++) {
                Debug.Log("Session.ModeRount[" + i + "]: " + Session.ModRoute.Points[i].ID + "||" + Session.ModRoute.Points[i].Name);
            }

            _cachedCommands = new List<ICommand>();
            Session.IsMod = true;

            // in case the aircraft was in free flight with intersection valid, shortcut mod until the node after intersection
            if (Session.PlayerAircraft.IsJoining)
            {
                ExecuteShortcutOnMod(new ExecuteShortcutOnModeCommand
                {
                    FromNodeId = Session.ModRoute.Points[1].ID,
                    ToNodeId = Session.ModRoute.Points[Session.PlayerAircraft.CurrentSegmentIndex].ID
                });
            }
        }
    }
}