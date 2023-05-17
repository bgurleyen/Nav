using Navigation;
using UnityEngine;

/// <summary>
/// TracedLines will have the first element null and will be related to the end point ( at same index )
/// </summary>
public class TracedRoute 
{
    public TracedLine[] TracedLines { get; private set; }

    private readonly float _drawerUnitLength;

    public TracedRoute(float drawerUnitLength)
    {
        _drawerUnitLength = drawerUnitLength;
    }

    public void ComputeSet(RoutePoint[] pointsArray, bool hasOtherMarkers = false)
    {
        TracedLines = new TracedLine[pointsArray.Length];
        // if (hasOtherMarkers)
        // {
        //     ComputedCircles = new List<FixCircle>();
        //     ComputedRays = new List<FixRay>();
        // }

        var pilot = new Pilot(
            nmPosition: pointsArray[0].CartesianPosition,
            initialOrientationTarget: pointsArray[1].CartesianPosition,
            
            stepDistance: _drawerUnitLength );

        for (int i = 1; i < pointsArray.Length; i++)
        {
            Debug.Log("alta linie");
            var line = new TracedLine(
                0.1f,
                1f,
                pilot,
                pointsArray[i - 1].CartesianPosition,
                pointsArray[i]);
            TracedLines[i] = line;
        }
    }
}