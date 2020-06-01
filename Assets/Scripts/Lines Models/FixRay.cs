using Gamelogic.Extensions;
using UnityEngine;

public class FixRay : Line
{
    public FixInfo LinkedInfo { get; private set; }

    public FixRay(DataPoint linkedPoint, FixInfo linkedInfo) : base(linkedPoint)
    {
        LinkedInfo = linkedInfo;

    }

    public void Init(Vector3 endPosition)
    {
        EndPosition = endPosition;

        var rayEnd = Geometry.GetNextPosition(EndPosition, 80, LinkedInfo.Degrees.Value);

        Vertexes = new Vector3[2];

        Vertexes[0] = EndPosition;
        Vertexes[1] = rayEnd;
    }
}
