using System;
using System.Collections.Generic;
using Gamelogic.Extensions;
using Lean.Pool;
using Legacy;
using UnityEngine;
using UnityEngine.Serialization;
using Unyawn.Utils;

namespace Navigation
{
    public class Drawer : MonoBehaviour
    {
        [SerializeField] private PlayerAircraft _centerAroundAircraft;
        [SerializeField] private Actor[] _otherActors;
        
        [SerializeField] private Transform _compasPivot;
        [Space] [SerializeField] private Transform _dynamicHolder;
        [Space] [SerializeField] private LeanGameObjectPool _linesPool;
        

        private void Awake()
        {
            UYServiceLocator.Register(this);
        }

        public void SimulateTick(float deltaTime, RouteScriptableObject routeData)
        {
            _centerAroundAircraft.SimulateTick(deltaTime);

            Session.PlayerNMPosition = _centerAroundAircraft.NMPosition;
            Session.PlayerHeadingDegrees = _centerAroundAircraft.HeadingDegrees;

            foreach (var actor in _otherActors)
            {
                actor.SimulateTick(deltaTime);
            }

            // Display
            DisplayRotations();

            foreach (var actor in _otherActors)
            {
                actor.Place();
            }
            

            ClearPooledVisuals();
            DisplaySet(routeData.PathLines.ComputedLines);
        }

        private void ClearPooledVisuals()
        {
            Extension.DespawnChildren<LineDrawer>(_dynamicHolder, _linesPool);
        }

        private void DisplayRotations()
        {
            switch (Session.Mode)
            {
                case DrawerMode.Center:
                case DrawerMode.Map:
                    //rotate compass
                    _compasPivot.SetLocalRotationZ(Session.PlayerHeadingDegrees);
                    //if (Aircraft.IsFreeFlight)
                    {
                       // freeFlightPivot.SetLocalRotationZ(Session.PlayerHeadingDegrees - Calculator.RHeading);
                    }

                    break;
                case DrawerMode.Plan:
                    // //rotate compass
                    // _compasPivot.SetLocalRotationZ(0);
                    // mobilePlaneIndicatorPivot.position =
                    //     Aircraft.PositionFreeOrOnCurvedPath.ToDisplay();
                    // mobilePlaneIndicatorPivot.SetLocalRotationZ(-Session.PlayerHeadingDegrees);
                    break;
                case DrawerMode.Suspeded:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void DisplaySet(IReadOnlyList<MarkLine> lines)
        {
            if (lines == null)
            {
                return;
            }

            LeanGameObjectPool pool;
            Transform holder;


            pool = _linesPool;
            holder = _dynamicHolder;


            for (var i = 0; i < lines.Count; i++)
            {
                var hiddenLabel = false;
                var hiddenLine = false;
                var fromPointIndex = 0;
                var line = lines[i];

                if (line == null)
                {
                    continue;
                }

                var point = line.LinkedPoint;

                var drawer = pool.Spawn(Vector3.zero, Quaternion.identity, holder).GetComponent<LineDrawer>();
                drawer.name = $"{line.GetName} {point.Name}";

                drawer.Display(line, point, hiddenLabel, hiddenLine, fromPointIndex);
            }
        }
    }
}