using Gamelogic.Extensions;
using UnityEngine;

public class FixCircle : Line
{
    public FixedPointInfo LinkedInfo { get; private set; }

    public FixCircle(RoutePoint linkedPoint, FixedPointInfo linkedInfo) : base(linkedPoint)
    {
        LinkedInfo = linkedInfo;
    }

    public void Init(Vector3 endPosition)
    {
        EndPosition = endPosition;

        var arcPoints = ComputeArcPoints(0, 360, 100, LinkedInfo.NM ?? 0, EndPosition, Vector3.zero);

        Vertexes = new Vector3[arcPoints.Length];

        for (var i = 0; i < arcPoints.Length; i++)
        {
            Vertexes[i] = EndPosition + arcPoints[i].To3DXY();
        }

    }
}
