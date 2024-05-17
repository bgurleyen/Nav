using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Navigation;

public class DataHandler
{
    public static void BuildSetDetails(RouteScriptableObject route)
    {
        route.ComputeCartesianPositions();
        
        BuildAltitudes(route);

        
        BuildSpeeds(route);
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
            else
            {
                point.SetSpeedComputed(lastRegulation);
            }
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

    private static void BuildAltitudes(RouteScriptableObject set, int startFrom = 22000)
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
                        validationAnchoredFrom = validationAnchoredFrom.AnchoredPrev == validationCursor
                            ? validationAnchoredFrom.AnchoredPrev.AnchoredPrev
                            : validationAnchoredFrom.AnchoredPrev;

                        validationAnchoredTo = validationAnchoredTo.AnchoredPrev;


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
            while (node != to)
            {
                node = node.Next;
                totalDistance += node.DistanceToPrevious;
            }

            var totalDiff = to.LinkedPoint.GetAcceptedAltitude - from.LinkedPoint.GetAcceptedAltitude;
            var lossForMile = totalDiff / totalDistance;

            for (var i = from.IndexInList + 1; i <= to.IndexInList; i++)
            {
                var regulation = regulations[i];
                var dif = regulation.DistanceToPrevious * lossForMile;
                regulation.LinkedPoint.Altitude.SetComputedValue(set.Points[i - 1].GetAcceptedAltitude + dif, false);
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
