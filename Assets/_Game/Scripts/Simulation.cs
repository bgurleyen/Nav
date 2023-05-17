using System;
using System.Collections.Generic;
using UnityEngine;
using Unyawn.Utils;

namespace Navigation
{
    public class Simulation : MonoBehaviour
    {
        [SerializeField] private Aircraft _playerAircraft;
        [SerializeField] private Actor[] _otherActors;

        [SerializeField] private Drawer _drawer;

        public event Action OnOperationMade;

        private bool queueEraseMode;
        private List<ICommand> cachedCommands;


        // if we detect that during MOD the current node has passed we reExecute all the commands until that point
        private int modReExecutedForIndex = -1;

        private void Awake()
        {
            UYServiceLocator.Register(this);
        }

        public void Init()
        {
            DataHandler.BuildSetDetails(Session.ActiveRoute);

            Session.PlayerAircraft = _playerAircraft;
            Session.Routes.FixedPoints = FixedPointsScriptableObject.CreateDemo();

            Session.ActiveRoute.ComputeSet(false);

            ComputeMod();

 //           _playerAircraft.Init(170, 21600);

            _drawer.Display();
            _drawer.ResetMode();
        }

        public void EraseMod()
        {
            Session.ModRoute = null;
            Session.IsMod = false;
            MainScreen.Instance.DisplayOperation("0k");
        }

        public void Tick(float deltaTime)
        {
            if (Session.IsMod && queueEraseMode)
            {
                EraseMod();
            }

            queueEraseMode = false;

            Session.ActiveRoute.ComputeSet(false);
            ComputeMod();

            //Session.PlayerAircraft.Advance();
            
            _playerAircraft.SimulateTick(deltaTime);

            foreach (var actor in _otherActors)
            {
                actor.SimulateTick(deltaTime);
            }
            
            foreach (var actor in _otherActors)
            {
                actor.Place();
            }

            if (Session.Mode != DrawerMode.Suspeded)
            {
                _drawer.Clear();
                _drawer.Display();
            }
        }

        public void QueueEraseMode()
        {
            queueEraseMode = true;
        }


        private void ComputeMod()
        {
            if (Session.ModRoute == null)
            {
                modReExecutedForIndex = -1;
                return;
            }

            if (Session.PlayerAircraft.IsOnRoute)
            {
                // mod always has to include the last passed active node ( all the passed nodes ) 
                // otherwise it is invalid - will reapply all the commands
                var passedNodeIndex = PositionVirtualNode.PassedNodeIndex;

                if (passedNodeIndex != modReExecutedForIndex)
                {
                    if (Session.ActiveRoute.Points[passedNodeIndex].ID != Session.ModRoute.Points[passedNodeIndex].ID)
                    {
                        ReExecuteCachedCommands();
                        Debug.Log("Reapplied MOD");
                    }

                    modReExecutedForIndex = passedNodeIndex;
                }
            }

            Session.ModeSetWithPosition = Session.ModRoute.CloneAndInit(); // refactor

            Session.ModeSetWithPosition.AddDisplayPositionNode();

            Session.ModeSetWithPosition.ComputeSet(true);
        }

        public void ReExecuteCachedCommands()
        {
            EraseMod();

            for (var i = 0; i < cachedCommands.Count; i++)
            {
                var command = cachedCommands[i];
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
                }
            }
        }

        public void ExecuteDeleteRestrictions(DeleteRestrictionsCommand command)
        {
            CheckModForOperation();
            cachedCommands.Add(command);

            MainScreen.Instance.DisplayOperation("ERASE");

            Session.ModRoute.GetPoint(command.NodeId, out var node);
            node.RawAltitude = "";
            node.RawSpeed = 0;

            DataHandler.BuildSetDetails(Session.ModRoute);
        }

        public void ExecuteAddSpeedRegulation(AddSpeedRegulationCommand command)
        {
            CheckModForOperation();
            cachedCommands.Add(command);

            MainScreen.Instance.DisplayOperation("ERASE");
            Session.ModRoute.GetPoint(command.NodeId, out var node);
            node.RawSpeed = command.Regulation;
            node.IsSpeedModified = true;
            DataHandler.BuildSetDetails(Session.ModRoute);

            OnOperationMade?.Invoke();
        }

        public void ExecuteAddAltitudeRegulation(AddAltitudeRegulationCommand command)
        {
            CheckModForOperation();
            cachedCommands.Add(command);

            MainScreen.Instance.DisplayOperation("ERASE");
            Session.ModRoute.GetPoint(command.NodeId, out var node);
            //maybe move this to route class to also do some tests?
            node.RawAltitude = command.Regulation;
            node.IsAltitudeModified = true;
            DataHandler.BuildSetDetails(Session.ModRoute);

            OnOperationMade?.Invoke();
        }

        public void ExecuteShortcutOnMod(ExecuteShortcutOnModeCommand command)
        {
            CheckModForOperation();
            cachedCommands.Add(command);

            Session.ModRoute.GetPoint(command.FromNodeId, out var node);
            MainScreen.Instance.DisplayOperation("ERASE", ToDegreesDisplay(node.RawDegrees));
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
            CheckModForOperation();
            cachedCommands.Add(command);

            Session.ModRoute.AddRelativeNodeOnDirection(command.FromNodeId, command.Distance, command.RelativeNodeId,
                out var _, out var _);
            DataHandler.BuildSetDetails(Session.ModRoute);
            MainScreen.Instance.DisplayOperation("ERASE");

            OnOperationMade?.Invoke();
        }


        public void ExecuteInsertRelativeOnMod(InsertRelativeCommand command)
        {
            CheckModForOperation();
            cachedCommands.Add(command);

            Session.ModRoute.AddRelativeNodeBefore(command.BeforeNodeId, command.RawDegrees, command.Distance,
                command.RelativeNodeId, out _, true);
            DataHandler.BuildSetDetails(Session.ModRoute);
            MainScreen.Instance.DisplayOperation(MainScreen.Keywords.ERASE);

            OnOperationMade?.Invoke();
        }

        public void ExecuteLinearApproachOnMod(ExecuteAddLinearApproachCommand command)
        {
            CheckModForOperation();
            cachedCommands.Add(command);

            Session.ModRoute.CreateLinearApproach(command.ToNodeId, command.Angle);
            DataHandler.BuildSetDetails(Session.ModRoute);
            MainScreen.Instance.DisplayOperation(MainScreen.Keywords.ERASE, ToDegreesDisplay(command.Angle), true);

            OnOperationMade?.Invoke();
        }



        private void CheckModForOperation()
        {
            if (Session.IsMod) return;

            // if this is the first modification generate a new mod from current active
            Session.ModRoute = Session.ActiveRoute.CloneAndInit();

            cachedCommands = new List<ICommand>();
            Session.IsMod = true;

            // in case the aircraft was in free flight with intersection valid shortcut mod until the node after intersection
            if (!Session.PlayerAircraft.IsOnRoute)
            {
                ExecuteShortcutOnMod(new ExecuteShortcutOnModeCommand
                {
                    FromNodeId = Session.ModRoute.Points[1].ID,
                    ToNodeId = Session.ModRoute.Points[Session.PlayerAircraft.RoutePathLocalization.CurrentNodeIndex].ID
                });
            }
        }
    }
}