using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Navigation;

public class DataHandler
{
    public static void BuildSetDetails(RouteScriptableObject route)
    {
        route.ComputeCartesianPositions();

        // Altitude solving can throw (a backward-anchor walk with no previous anchor).
        // Speeds must still be filled; otherwise every LEGS speed stays at the -1 default.
        try
        {
            BuildAltitudes(route);
        }
        finally
        {
            BuildSpeeds(route);
        }
    }

    private static void BuildSpeeds(RouteScriptableObject set, int startFrom = 270)
    {
        set.Points[0].RawSpeed = startFrom;
        var lastRegulation = startFrom;

        for (var i = 0; i < set.Points.Length; i++)
        {
            var point = set.Points[i];

            if(point.IsSpeedRegulated)
            {
                lastRegulation = point.RawSpeed;
            }

            // Restricted points used to leave ComputedValue at -1. FMC predictions read that field.
            point.SetSpeedComputed(lastRegulation);
        }
        
        set.FirstSpeedRegulationNodeId = -1;
        for (var i = 1; i < set.Points.Length; i++)
        {
            var point = set.Points[i];
            if (point.IsSpeedRegulated )
            {
                set.FirstSpeedRegulationNodeId = point.ID;
                return;
            }
        }
    }

    
    private static void BuildAltitudes(RouteScriptableObject set, int? startFrom = null)
    {


           if (set.Points[0].AltitudeRegulation == RoutePoint.AltitudeFlags.NotSet)
           {
               set.Points[0].RawAltitude = startFrom.ToString();
           }

      

        List<AltitudeRegulationNode> regulations = null;
        var distanceToPrevious = 0f;

        // the list is of the nodes of a linked tree
        for (var i = 0; i < set.Points.Length - 1; i++)
        {
            var point = set.Points[i];

            // if it is not a current regulations interval started
            if (regulations == null)
            {
                if (set.Points[i + 1].AltitudeRegulation == RoutePoint.AltitudeFlags.Exact)
                {
                    continue;
                }

                // start new regulations interval
                regulations = new List<AltitudeRegulationNode>
                {
                    new AltitudeRegulationNode
                    {
                        IndexInList = i,
                        LinkedPoint = set.Points[i],
                        Next = null,
                        Prev = null
                    }
                };
                distanceToPrevious = point.Distance;
                continue;
            }

            distanceToPrevious += point.Distance;

            if (point.AltitudeRegulation != RoutePoint.AltitudeFlags.NotSet)
            {
                var lastRegulation = regulations[regulations.Count - 1];
                var newRegulation = new AltitudeRegulationNode
                {
                    IndexInList = regulations.Count,
                    PointIndexInSet = i,
                    LinkedPoint = point,
                    Next = null,
                    Prev = lastRegulation,
                    DistanceToPrevious = distanceToPrevious
                };

                lastRegulation.Next = newRegulation;
                regulations.Add(newRegulation);
                distanceToPrevious = 0;

                // if other exact node reached, close and compute current regulations interval
                if (set.Points[i].AltitudeRegulation == RoutePoint.AltitudeFlags.Exact)
                {
                    ComputeRegulationsInterval(regulations, set);
                }
            }
        }

        FillMissingAltitudes(set);

        set.FirstAltRegulationNodeId = -1;
        for (var i = 1; i < set.Points.Length; i++)
        {
            var point = set.Points[i];

            if (point.AltitudeRegulation != RoutePoint.AltitudeFlags.NotSet)
            {
                set.FirstAltRegulationNodeId = point.ID;
                return;
            }
        }
    }

    /// <summary>
    /// Fill INIT holes after the last exact, A/B skips, and the last waypoint.
    /// Empty RW uses field elevation + 50 ft (TCH). Unknown elevation → 50.
    /// </summary>
    private static void FillMissingAltitudes(RouteScriptableObject set)
    {
        var pts = set.Points;
        if (pts == null || pts.Length == 0)
            return;

        for (var i = 0; i < pts.Length; i++)
            pts[i].Altitude.GetFlag(pts[i].RawAltitude);

        var last = pts[pts.Length - 1];
        var rw = RunwayTargetAltitude(last);
        if (last.Altitude.ComputedValue <= 0)
            last.Altitude.SetComputedValue(rw, true);

        ClampAbRestrictions(pts);

        var anchors = new List<int>();
        for (var i = 0; i < pts.Length; i++)
        {
            if (pts[i].Altitude.ComputedValue > 0)
                anchors.Add(i);
        }

        if (anchors.Count == 0)
            return;

        if (anchors[anchors.Count - 1] != pts.Length - 1)
            anchors.Add(pts.Length - 1);

        for (var a = 0; a < anchors.Count - 1; a++)
        {
            var from = anchors[a];
            var to = anchors[a + 1];
            if (to <= from + 1)
                continue;

            var total = 0f;
            for (var i = from + 1; i <= to; i++)
                total += pts[i].Distance;

            if (total < 1e-4f)
            {
                var hold = pts[from].Altitude.ComputedValue;
                for (var i = from + 1; i < to; i++)
                {
                    if (pts[i].Altitude.ComputedValue <= 0)
                        pts[i].Altitude.SetComputedValue(hold, false);
                }
                continue;
            }

            var alt0 = pts[from].Altitude.ComputedValue;
            var alt1 = pts[to].Altitude.ComputedValue;
            var cum = 0f;
            for (var i = from + 1; i < to; i++)
            {
                cum += pts[i].Distance;
                if (pts[i].Altitude.ComputedValue > 0)
                    continue;
                pts[i].Altitude.SetComputedValue(alt0 + (alt1 - alt0) * (cum / total), false);
            }
        }

        ClampAbRestrictions(pts);
    }

    private static float RunwayTargetAltitude(RoutePoint rw)
    {
        if (rw == null)
            return 50f;

        rw.Altitude.GetFlag(rw.RawAltitude);
        if (rw.Altitude.RestrictionExact > 0)
            return rw.Altitude.RestrictionExact;
        if (rw.Altitude.ComputedValue > 0)
            return rw.Altitude.ComputedValue;
        if (!string.IsNullOrEmpty(rw.RawAltitude)
            && float.TryParse(rw.RawAltitude, out var raw)
            && raw > 0)
            return raw;

        return FieldElevationFeet(rw) + 50f;
    }

    private static float FieldElevationFeet(RoutePoint rw)
    {
        if (rw != null && rw.Altitude.RestrictionExact > 0)
            return rw.Altitude.RestrictionExact;
        return 0f;
    }

    private static void ClampAbRestrictions(RoutePoint[] pts)
    {
        for (var i = 0; i < pts.Length; i++)
        {
            var p = pts[i];
            var fl = p.AltitudeRegulation;
            var v = p.Altitude.ComputedValue;
            if (v <= 0)
                continue;
            if ((fl == RoutePoint.AltitudeFlags.Below || fl == RoutePoint.AltitudeFlags.AboveBelow)
                && p.Altitude.RestrictionBelow > 0 && v > p.Altitude.RestrictionBelow)
                p.Altitude.SetComputedValue(p.Altitude.RestrictionBelow, true);
            if ((fl == RoutePoint.AltitudeFlags.Above || fl == RoutePoint.AltitudeFlags.AboveBelow)
                && p.Altitude.RestrictionAbove > 0 && v < p.Altitude.RestrictionAbove)
                p.Altitude.SetComputedValue(p.Altitude.RestrictionAbove, true);
        }
    }

    private static void ComputeRegulationsInterval(List<AltitudeRegulationNode> regulations, RouteScriptableObject set)
    {

        var anchoredFrom = regulations[0];
        var anchoredTo = regulations[regulations.Count - 1];
        anchoredFrom.AnchoredNext = anchoredTo;

        // initial compute straight line
        ComputeStraightInterval(anchoredFrom, anchoredTo);

        // check each regulation
        var cursor = anchoredFrom; // from the first after start
        while (cursor.Next != null) // until the last
        {
            cursor = cursor.Next;
            var point = cursor.LinkedPoint;
            var flag = point.AltitudeRegulation;

            var anchored = false;

            switch (flag)
            {
                case RoutePoint.AltitudeFlags.Below:
                    if (AnchorForBelow(point))
                    {
                        anchored = true;
                    }
                    break;

                case RoutePoint.AltitudeFlags.Above:
                    if (AnchorForAbove(point))
                    {
                        anchored = true;
                    }
                    break;
                case RoutePoint.AltitudeFlags.AboveBelow:
                    if (AnchorForBelow(point)
                    || AnchorForAbove(point))
                    {
                        anchored = true;
                    }

                    break;
            }

            if (anchored)
            {
                // cursor will became a new anchor
                anchoredFrom.AnchoredNext = cursor;
                cursor.AnchoredPrev = anchoredFrom;
                cursor.AnchoredNext = anchoredTo;
                anchoredTo.AnchoredPrev = cursor;

                ComputeStraightInterval(anchoredFrom, cursor);
                ComputeStraightInterval(cursor, anchoredTo); // may not be needed - it's direct

                anchoredFrom = cursor;

                // ==  validate backwards ==
                var validationCursor = cursor.Prev;

                if (validationCursor != null &&
                    validationCursor.LinkedPoint.AltitudeRegulation != RoutePoint.AltitudeFlags.Exact)
                {
                    // previos anchor before the previos node
                    var validationAnchoredFrom = anchoredFrom;
                    var validationAnchoredTo = anchoredTo;

                    bool validationUpdated;
                    do
                    {
                        // shift validation interval to previous
                        var shiftedFrom = validationAnchoredFrom.AnchoredPrev == validationCursor
                            ? validationAnchoredFrom.AnchoredPrev.AnchoredPrev
                            : validationAnchoredFrom.AnchoredPrev;
                        var shiftedTo = validationAnchoredTo.AnchoredPrev;

                        // Level 19: the walk reaches the cruise anchor, whose previous anchor is null.
                        if (shiftedFrom == null || shiftedTo == null)
                            break;

                        validationAnchoredFrom = shiftedFrom;
                        validationAnchoredTo = shiftedTo;

                        validationUpdated = false;

                        var computedToCheck = validationCursor.LinkedPoint.GetAcceptedAltitude;
                        var computedWasAnchored = validationCursor.LinkedPoint.Altitude.IsAnchored;

                        ComputeStraightInterval(validationAnchoredFrom, validationAnchoredTo);

                        var validationAnchored = AnchorForBelow(validationCursor.LinkedPoint) ||
                                                 AnchorForAbove(validationCursor.LinkedPoint);

                        var validationValueModified = computedToCheck != validationCursor.LinkedPoint.GetAcceptedAltitude;
                        var validationAnchorModified = computedWasAnchored != validationAnchored;

                        validationUpdated = validationValueModified || validationAnchorModified;

                        // if validation cursor was not already an anchor insert it as an anchor
                        if (validationAnchored && !computedWasAnchored)
                        {
                            validationAnchoredFrom.AnchoredNext = validationCursor;
                            validationCursor.AnchoredPrev = validationAnchoredFrom;
                            validationCursor.AnchoredNext = validationAnchoredTo;
                            validationAnchoredTo.AnchoredPrev = validationCursor;
                        }

                        // if validation is still an anchor, but other kind or was not an anchor 
                        if (validationAnchored && validationUpdated)
                        {
                            ComputeStraightInterval(validationAnchoredFrom, validationCursor);
                            ComputeStraightInterval(validationCursor, validationAnchoredTo); // may not be needed - it's direct
                        }

                        // if validation cursor stopped being an achor remove the anchor node
                        if (!validationAnchored && computedWasAnchored)
                        {
                            anchoredFrom.AnchoredNext = anchoredTo;
                            anchoredTo.AnchoredPrev = anchoredFrom;
                        }

                        // go to the prev node ( anchored or not )
                        validationCursor = validationCursor.Prev;


                    }
                    while (
                        validationCursor != null &&
                        validationCursor.Prev != null &&
                        validationUpdated &&
                        validationCursor.LinkedPoint.AltitudeRegulation != RoutePoint.AltitudeFlags.Exact);
                }
            }
        }


        bool AnchorForBelow(RoutePoint point)
        {
            if (point.GetAcceptedAltitude > point.Altitude.RestrictionBelow)
            {
                // adjust
                point.Altitude.SetComputedValue(point.Altitude.RestrictionBelow, true);
                return true;
            }
            return false;
        }

        bool AnchorForAbove(RoutePoint point)
        {
            if (point.GetAcceptedAltitude < point.Altitude.RestrictionAbove)
            {
                //adjust
                point.Altitude.SetComputedValue(point.Altitude.RestrictionAbove, true);
                return true;
            }
            return false;
        }

        void ComputeStraightInterval(AltitudeRegulationNode from, AltitudeRegulationNode to)
        {
            var totalDistance = 0f;
            var node = from;
            var guard = 0;
            while (node != null && node != to)
            {
                node = node.Next;
                if (node == null || ++guard > regulations.Count + 1)
                    break;
                totalDistance += node.DistanceToPrevious;
            }

            var totalDiff = to.LinkedPoint.GetAcceptedAltitude - from.LinkedPoint.GetAcceptedAltitude;
            var lossForMile = totalDiff / totalDistance;
            var altitudeFrom = from.LinkedPoint.GetAcceptedAltitude;
            var cumulativeDistance = 0f;

            for (var i = from.IndexInList + 1; i <= to.IndexInList; i++)
            {
                var regulation = regulations[i];
                cumulativeDistance += regulation.DistanceToPrevious;
                var computedAltitude = altitudeFrom + cumulativeDistance * lossForMile;
                regulation.LinkedPoint.Altitude.SetComputedValue(computedAltitude, false);
            }
        }
        // fil in altitudes for points between regulations
        anchoredFrom = regulations[0];
        while (anchoredFrom.Next != null)
        {
            var totalDistance = anchoredFrom.Next.DistanceToPrevious;
            var totalDiff = anchoredFrom.Next.LinkedPoint.GetAcceptedAltitude - anchoredFrom.LinkedPoint.GetAcceptedAltitude;
            var lossForMile = totalDiff / totalDistance;

            for (int i = anchoredFrom.PointIndexInSet + 1; i <= anchoredFrom.Next.PointIndexInSet; i++)
            {

                var dif = set.Points[i].Distance * lossForMile;
                set.Points[i].Altitude.SetComputedValue(set.Points[i - 1].GetAcceptedAltitude + dif, false);
            }
            anchoredFrom = anchoredFrom.Next;
        }

    }

    // ex: 180A3444B
    public static bool ParseAltRegulation(string input, out int above, out int below, out int exact)
    {
        above = -1;
        below = -1;
        exact = -1;
        var re = new Regex(@"(\d+)([a-zA-Z]{0,1})");
        var result = re.Matches(input);

        if (result.Count == 0)
        {
            // $^% also exclude leftovers 234AA234B : second A needs to flag error
            return false;
        }

        for (var i = 0; i < result.Count; i++)
        {
            var set = result[i];
            var number = int.Parse(set.Groups[1].Value);
            var indicator = set.Groups[2].Value;

            switch (indicator.ToUpper())
            {
                case "B":
                    below = number;
                    break;
                case "A":
                    above = number;
                    break;
                default:
                    exact = number;
                    break;
            }
        }

        return true;
    }


    private class AltitudeRegulationNode
    {
        public AltitudeRegulationNode Prev;
        public AltitudeRegulationNode Next;

        public AltitudeRegulationNode AnchoredPrev;
        public AltitudeRegulationNode AnchoredNext;

        public RoutePoint LinkedPoint;
        public int IndexInList;
        public float DistanceToPrevious;

        public int PointIndexInSet;
    }

}
