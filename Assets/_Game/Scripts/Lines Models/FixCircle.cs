using Gamelogic.Extensions;
using Navigation;
using UnityEngine;

public class FixCircle : Line
{
    public FixedPointInfo LinkedInfo { get; private set; }

    public FixCircle(RoutePoint linkedPoint, FixedPointInfo linkedInfo) : base(linkedPoint, 1)
    {
        LinkedInfo = linkedInfo;
    }

    public void Init(Vector3 endPosition)
    {
        EndPosition = endPosition;

        var arcPoints = ComputeArcPoints(0, 360, UnitLength, LinkedInfo.NM ?? 0, EndPosition, Vector3.zero);

        Vertexes = new Vector3[arcPoints.Length];

        for (var i = 0; i < arcPoints.Length; i++)
        {
            Vertexes[i] = EndPosition + arcPoints[i].To3DXY();
        }

    }

    public void Init(Vector3 endPosition, bool isFirst)
    {
        EndPosition = endPosition;

        var arcPoints = ComputeArcPoints(0, 360, 0.25f, 0.5f, EndPosition, Vector3.zero);

        Vertexes = new Vector3[arcPoints.Length];

        for (var i = 0; i < arcPoints.Length; i++)
        {
            Vertexes[i] = EndPosition + arcPoints[i].To3DXY();
        }
    }
}
