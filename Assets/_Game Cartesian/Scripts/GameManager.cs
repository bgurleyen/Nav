using System;
using UnityEngine;
using Unyawn.Utils;

namespace Navigation
{
    public class GameManager : MonoBehaviour
    {
        private Drawer _drawer;
        
        private void Start()
        {
            _drawer = UYServiceLocator.Get<Drawer>();
        }

        private void FixedUpdate()
        {
            _drawer.SimulateTick(Time.fixedDeltaTime);
        }
    }
}