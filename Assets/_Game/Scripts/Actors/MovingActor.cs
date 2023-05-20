using Navigation;
using UnityEngine;

namespace Navigation
{
    public class MovingActor : Actor
    {
        public virtual Vector2 Direction => Vector2.up;
    }
}