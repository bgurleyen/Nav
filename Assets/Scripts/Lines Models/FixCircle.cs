using Gamelogic.Extensions;
using UnityEngine;

public class FixCircle : Line
{
    public FixInfo LinkedInfo { get; private set; }

    public FixCircle(DataPoint linkedPoint, FixInfo linkedInfo) : base(linkedPoint)
    {
        LinkedInfo = linkedInfo;
    }

    public void Init(Vector3 endPosition)
    {
        EndPosition = endPosition;

        var _arcPoints = ComputeArcPoints(0, 360, 100, LinkedInfo.NM ?? 0, EndPosition, Vector3.zero);

        Vertexes = new Vector3[_arcPoints.Length];

        for (var i = 0; i < _arcPoints.Length; i++)
        {
            Vertexes[i] = EndPosition + _arcPoints[i].To3DXY();
        }

    }
}
