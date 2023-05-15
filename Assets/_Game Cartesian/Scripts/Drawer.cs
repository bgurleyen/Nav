using System.Collections.Generic;
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

            foreach (var actor in _otherActors)
            {
                actor.SimulateTick(deltaTime);
            }

            // Display

            foreach (var actor in _otherActors)
            {
                actor.Place();
            }
            

            ClearPooledVisuals();
            DisplaySet(routeData.PathLines?.ComputedLines);
        }

        private void ClearPooledVisuals()
        {
            Extension.DespawnChildren<LineDrawer>(_dynamicHolder, _linesPool);
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