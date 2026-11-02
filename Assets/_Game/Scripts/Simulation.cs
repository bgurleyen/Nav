using System;
using System.Collections.Generic;
using UnityEngine;
using Unyawn.Utils;

namespace Navigation {
    public class Simulation : MonoBehaviour {
        [SerializeField] private Aircraft _playerAircraft;

        [SerializeField] private Drawer _drawer;

        public event Action OnOperationMade;
        public Drawer Drawer => _drawer;

        private List<ICommand> _cachedCommands;

        private LegsScreen _legsScreen;


        // if we detect that during MOD the current node has passed we reExecute all the commands until that point
        private int _modReExecutedForIndex = -1;
        // Intercept replaces point 0. Replaying that id mismatch calls EraseMod,
        // which clears the passed-index guard and can re-enter this replay.
        private bool _reExecutingCommands;
        private bool _insideCheckMod;

        private void Awake() {
            UYServiceLocator.Register(this);
        }

        private void Start() {
            _legsScreen = UYServiceLocator.Get<LegsScreen>();
            if (_legsScreen == null)
                Debug.LogError("[Simulation] LegsScreen is not registered. LEGS scratchpad updates will be skipped.");
        }

        private void DisplayLegsOperation(string value, string details = "", bool tallDetails = false, int courseNodeId = -1) {
            if (_legsScreen != null)
                _legsScreen.DisplayOperation(value, details, tallDetails, courseNodeId);
        }

        private void DisplayLegsCourseForNode(int nodeId) {
            if (Session.ModRoute == null || nodeId < 0 || !Session.ModRoute.GetPoint(nodeId, out var node, out _) || node == null)
            {
                DisplayLegsOperation(MainScreen.Keywords.ERASE, courseNodeId: nodeId);
                return;
            }

            float course = node.FmcDisplayLegCourse();
            DisplayLegsOperation(MainScreen.Keywords.ERASE, ToDegreesDisplay(course), true, nodeId);
        }

        public void Init() {
            try
            {
                DataHandler.BuildSetDetails(Session.ActiveRoute);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Debug.LogError("[Simulation.Init] BuildSetDetails failed — continuing so the flight can still run.");
            }

            Session.PlayerAircraft = _playerAircraft;

            if (_playerAircraft == null || _drawer == null || Session.Routes == null
                || Session.ActiveRoute == null || Session.Settings == null)
            {
                Debug.LogError("[Simulation.Init] Aircraft, drawer, route, or settings is missing.");
                return;
            }

            //changes by S.A
            Session.Routes.FixedPoints = FixedPointsScriptableObject.CloneAndInit();
            //Debug.Log(Session.Routes.FixedPoints.Entries.Length);

            // Unity Object→bool: original ComputeTrace(transform) meant hasOtherMarkers=true (fix circles/rays).
            Session.ActiveRoute.ComputeTrace(true);
            //Debug.Log(Session.ActiveRoute.TracedRoute.ComputedLines.Length);

            _playerAircraft.Init(Session.Settings.AirplaneDesignSpeed, 21600);

            _drawer.Display();
            _drawer.RefreshDisplayDistances();
            _drawer.ResetMode();

            Session.IsRunning = true;
            // Briefing pauses until REQUEST DESCENT; don't unpause over LevelStartInformation.
            // Score-test / level-select / pause can leave timeScale at 0; only force-run when no briefing UI.
            if (LevelTestMode.IsActive
                || (!PlayerPrefsHolder.ShowLevelSelectOnLoad
                    && FindFirstObjectByType<LevelStartInformation>() == null))
                Time.timeScale = 1f;

        }

        public void EraseMod() {
            Session.ModRoute = null;
            Session.IsMod = false;
            ClearModeSetWithPosition();
            _modReExecutedForIndex = -1;
            DisplayLegsOperation("0k");
        }

        public void Tick() {
            if (!Session.IsRunning)
                return;

            if (_playerAircraft != null)
                _playerAircraft.SimulateTick();
            if (Move.Instance != null)
                Move.Instance.Tick();

            ComputeMod();

            if (_drawer != null)
            {
                _drawer.Clear();
                _drawer.Display();
            }
        }

        //[ESA] I splitted the Tick() functionality with prefix "Splitted" to accurately
        //simulate the steps of the aircraft at 10x speedup enabled while minimizing performance impact.
        //look at the GameManager.Update() function to see its use.
        public void SplittedTickModHandling() {
        }

        public void SplittedTickSimulation() {
            if (!Session.IsRunning)
                return;

            if (_playerAircraft != null)
                _playerAircraft.SimulateTick();
            if (Move.Instance != null)
                Move.Instance.Tick();
        }

        public void SplittedTickComputeMod() {
            if (Session.IsRunning) {
                ComputeMod();
            }
        }

        public void SplittedTickDraw() {
            if (Session.IsRunning && _drawer != null)
            {
                _drawer.Clear();
                _drawer.Display();
            }
        }

        private void ComputeMod() {
            if (Session.ModRoute == null) {
                ClearModeSetWithPosition();
                _modReExecutedForIndex = -1;
                return;
            }

            if (Session.State != null && Session.State.LNAV) {
                // mod always has to include the last passed active node ( all the passed nodes ) 
                // otherwise it is invalid - will reapply all the commands
                var passedNodeIndex = PositionVirtualNode.PassedNodeIndex;

                if (passedNodeIndex != _modReExecutedForIndex) {
                    var modPoints = Session.ModRoute.Points;
                    var activePoints = Session.ActiveRoute?.Points;
                    var bothInRange = modPoints != null && activePoints != null
                        && passedNodeIndex >= 0
                        && passedNodeIndex < modPoints.Length
                        && passedNodeIndex < activePoints.Length;
                    var activePoint = bothInRange ? activePoints[passedNodeIndex] : null;
                    var modPoint = bothInRange ? modPoints[passedNodeIndex] : null;
                    // A shorter MOD (direct-to removed waypoints) is not a reason to rebuild.
                    // Rebuilding here used to throw or restart every frame and lock the game.
                    // Course intercept always puts a new INTC id at index 0. Replaying that
                    // wipes the mod and runs the command again; a nested call overflows the stack.
                    var diverged = bothInRange
                        && activePoint != null
                        && modPoint != null
                        && !Session.ModRoute.IsCourseInterceptRoute
                        && activePoint.ID != modPoint.ID;
                    // Commit first, and again after replay: EraseMod clears this index,
                    // which otherwise restarts the replay on every following frame.
                    _modReExecutedForIndex = passedNodeIndex;
                    if (diverged)
                        ReExecuteCachedCommands();
                    _modReExecutedForIndex = passedNodeIndex;
                }
            }

            RebuildModeSetWithPosition();
        }

        public void RebuildModeSetWithPosition() {
            if (Session.ModRoute == null) {
                ClearModeSetWithPosition();
                return;
            }

            ClearModeSetWithPosition();
            Session.ModeSetWithPosition = Session.ModRoute.CloneAndInit();
            Session.ModeSetWithPosition.AddModPositionNodes();
            Session.ModeSetWithPosition.ComputeTrace();
        }

        public void ClearModPreview() {
            ClearModeSetWithPosition();
        }

        private void ClearModeSetWithPosition() {
            if (Session.ModeSetWithPosition == null)
                return;
            Destroy(Session.ModeSetWithPosition);
            Session.ModeSetWithPosition = null;
        }

        public void ReExecuteCachedCommands() {
            // Nested replay is the intercept stack overflow: EraseMod clears the
            // passed-index guard while this call is still on the stack.
            if (_reExecutingCommands)
                return;

            // CheckMod replaces _cachedCommands. Iterating that list grows forever
            // when a join shortcut is inserted, and the frame never returns.
            var commands = _cachedCommands == null
                ? new List<ICommand>()
                : new List<ICommand>(_cachedCommands);
            _reExecutingCommands = true;
            try {
                EraseMod();

                for (var i = 0; i < commands.Count; i++) {
                    var command = commands[i];
                    switch (command) {
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
            finally {
                _reExecutingCommands = false;
            }
        }

        public void ExecuteDeleteRestrictions(DeleteRestrictionsCommand command) {
            CheckModForOperation();
            _cachedCommands.Add(command);

            DisplayLegsOperation("ERASE");

            if (!Session.ModRoute.GetPoint(command.NodeId, out var node, out _) || node == null)
                return;
            node.RawAltitude = "";
            node.RawSpeed = 0;

            DataHandler.BuildSetDetails(Session.ModRoute);
        }

        public void ExecuteAddSpeedRegulation(AddSpeedRegulationCommand command) {
            CheckModForOperation();
            _cachedCommands.Add(command);

            DisplayLegsOperation("ERASE");
            if (!Session.ModRoute.GetPoint(command.NodeId, out var node, out _) || node == null)
                return;
            node.RawSpeed = command.Regulation;
            node.IsSpeedModified = true;
            DataHandler.BuildSetDetails(Session.ModRoute);

            OnOperationMade?.Invoke();
        }

        public void ExecuteAddAltitudeRegulation(AddAltitudeRegulationCommand command) {
            CheckModForOperation();
            _cachedCommands.Add(command);

            DisplayLegsOperation("ERASE");
            if (!Session.ModRoute.GetPoint(command.NodeId, out var node, out _) || node == null)
                return;
            node.RawAltitude = command.Regulation;
            node.IsAltitudeModified = true;
            DataHandler.BuildSetDetails(Session.ModRoute);

            OnOperationMade?.Invoke();
        }

        public void ExecuteDeleteWayPoint(DeleteWayPointCommand command)
        {
            CheckModForOperation();
            _cachedCommands.Add(command);

            Session.ModRoute.RemoveNode(command.NodeId, out _);
            DataHandler.BuildSetDetails(Session.ModRoute);
            DisplayLegsOperation(MainScreen.Keywords.ERASE);

            OnOperationMade?.Invoke();
        }

        public void ExecuteShortcutOnMod(ExecuteShortcutOnModeCommand command) {
            CheckModForOperation();
            _cachedCommands.Add(command);

            if (!Session.ModRoute.GetPoint(command.FromNodeId, out _, out _)
                || !Session.ModRoute.GetPoint(command.ToNodeId, out _, out _)) {
                DisplayLegsOperation(MainScreen.Keywords.ERASE);
                return;
            }
            if (!Session.ModRoute.Points.GetNodeIndex(command.ToNodeId, out _)) {
                DisplayLegsOperation(MainScreen.Keywords.ERASE);
                return;
            }

            Session.ModRoute.ShortcutNodes(command.FromNodeId, command.ToNodeId, out var reducedPoint);
            if (reducedPoint == null) {
                DisplayLegsOperation(MainScreen.Keywords.ERASE);
                return;
            }

            DataHandler.BuildSetDetails(Session.ModRoute);
            if (Session.ModRoute.GetPoint(reducedPoint.ID, out var onRoute, out _) && onRoute != null)
                DisplayLegsCourseForNode(onRoute.ID);
            else
                DisplayLegsCourseForNode(reducedPoint.ID);

            OnOperationMade?.Invoke();
        }

        private static string ToDegreesDisplay(float value) {
            return $"{value:000}°";
        }

        public void ExecuteInsertRelativeOnDirectionOnMod(ExecuteRelativeOnDirectionOnMod command) {
            Debug.Log("=execute relative insert on direction=");
            CheckModForOperation();
            _cachedCommands.Add(command);

            Session.ModRoute.AddRelativeNodeOnDirection(command.FromNodeId, command.Distance, command.RelativeNodeId,
                out _, out _);
            DataHandler.BuildSetDetails(Session.ModRoute);
            DisplayLegsOperation("ERASE");

            OnOperationMade?.Invoke();
        }

        public void ExecuteInsertOriginalOnMod(ExecuteOriginalInsertOnModCommand command) {
            //Debug.Log("=execute insert original on MOD=");
            CheckModForOperation();
            _cachedCommands.Add(command);

            Session.ModRoute.AddDirectToCartesianNodeBefore(command.OriginalRouteNodeId, command.OnTopNodeId, out var inserted);

            DataHandler.BuildSetDetails(Session.ModRoute);
            if (inserted != null
                && Session.ModRoute.GetPointByName(inserted.Name, out var onRoute)
                && onRoute != null)
                DisplayLegsCourseForNode(onRoute.ID);
            else if (inserted != null)
                DisplayLegsCourseForNode(inserted.ID);
            else
                DisplayLegsOperation(MainScreen.Keywords.ERASE);

            OnOperationMade?.Invoke();
        }

        public void ExecuteInsertRelativeOnMod(InsertRelativeCommand command) {
            //Debug.Log("=execute relative insert simple=");
            CheckModForOperation();
            _cachedCommands.Add(command);

            Session.ModRoute.AddRelativeNodeBefore(command.BeforeNodeId, command.RawDegrees, command.Distance,
                command.RelativeNodeId, out var insertionNode, true);
            DataHandler.BuildSetDetails(Session.ModRoute);
            if (insertionNode != null)
                DisplayLegsCourseForNode(insertionNode.ID);
            else
                DisplayLegsOperation(MainScreen.Keywords.ERASE);

            OnOperationMade?.Invoke();
        }

        public void ExecuteLinearApproachOnMod(ExecuteAddLinearApproachCommand command) {
            //Debug.Log("=linear approach=");
            CheckModForOperation();
            _cachedCommands.Add(command);

            Session.ModRoute.CreateLinearApproach(command.ToNodeId, command.Angle);
            DataHandler.BuildSetDetails(Session.ModRoute);
            DisplayLegsOperation(MainScreen.Keywords.ERASE, ToDegreesDisplay(command.Angle), true, command.ToNodeId);

            OnOperationMade?.Invoke();
        }

        private void CheckModForOperation() {
            // IsMod is set below before the join shortcut. If that shortcut clears it,
            // this flag still stops CheckMod → shortcut → CheckMod.
            if (Session.IsMod || _insideCheckMod) return;
            if (Session.ActiveRoute == null)
                return;

            _insideCheckMod = true;
            try {
                if (Session.ModRoute != null) {
                    Destroy(Session.ModRoute);
                }

                // if this is the first modification generate a new mod from current active
                Session.ModRoute = Session.ActiveRoute.CloneAndInit();

                _cachedCommands = new List<ICommand>();
                Session.IsMod = true;

                // in case the aircraft was in free flight with intersection valid, shortcut mod until the node after intersection
                var segment = Session.PlayerAircraft.CurrentSegmentIndex;
                if (Session.PlayerAircraft.IsJoining &&
                    segment > 0 &&
                    segment < Session.ModRoute.Points.Length) {
                    ExecuteShortcutOnMod(new ExecuteShortcutOnModeCommand {
                        FromNodeId = Session.ModRoute.Points[1].ID,
                        ToNodeId = Session.ModRoute.Points[segment].ID
                    });
                }
            }
            finally {
                _insideCheckMod = false;
            }
        }
    }
}