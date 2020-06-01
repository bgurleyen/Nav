using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

public class DataHandler
{
    internal static void BuildDataSet(RouteScriptableObject route, out RouteScriptableObject routeBuilt)
    {
        var newSet = route.Clone();

        BuildSetDetails(newSet);

        routeBuilt = newSet;
    }

    public static void BuildSetDetails(RouteScriptableObject route)
    {
        BuildAltitudes(route);
        BuildSpeeds(route);
    }

    static RouteScriptableObject BuildSpeeds(RouteScriptableObject set, int startFrom = 270)
    {
        set.Points[0].RawSpeed = startFrom;
        var lastRegulation = startFrom;

        for (int i = 0; i < set.Points.Length; i++)
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
        return set;
    }

    static RouteScriptableObject BuildAltitudes(RouteScriptableObject set, int startFrom = 40000)
    {
        if (set.Points[0].AltitudeRegulation == RoutePoint.AltitudeFlags.NotSet)
        {
            set.Points[0].RawAltitude = startFrom.ToString();
        }

        List<RegulationNode> regulations = null;
        var distanceToPrevious = 0f;

        // the list is of the nodes of a linked tree
        for (int i = 0; i < set.Points.Length - 1; i++)
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
                regulations = new List<RegulationNode>
                {
                    new RegulationNode
                    {
                        IndexInList = i,
                        linkedPoint = set.Points[i],
                        next = null,
                        prev = null
                    }
                };
                distanceToPrevious = point.Distance;
                continue;
            }
            else
            {
                distanceToPrevious += point.Distance;
            }

            if (point.AltitudeRegulation != RoutePoint.AltitudeFlags.NotSet)
            {
                var lastRegulation = regulations[regulations.Count - 1];
                var newRegulation = new RegulationNode
                {
                    IndexInList = regulations.Count,
                    PointIndexInSet = i,
                    linkedPoint = point,
                    next = null,
                    prev = lastRegulation,
                    distanceToPrevious = distanceToPrevious
                };

                lastRegulation.next = newRegulation;
                regulations.Add(newRegulation);
                distanceToPrevious = 0;

                // if other exact node reached, close and compute current regulations interval
                if (set.Points[i].AltitudeRegulation == RoutePoint.AltitudeFlags.Exact)
                {
                    ComputeRegulationsInterval(regulations, set);
                }
            }
        }

        return set;
    }

    static void ComputeRegulationsInterval(List<RegulationNode> regulations, RouteScriptableObject set)
    {
        var anchoredFrom = regulations[0];
        var anchoredTo = regulations[regulations.Count - 1];
        anchoredFrom.anchoredNext = anchoredTo;

        // initial compute straight line
        ComputeStraightInterval(anchoredFrom, anchoredTo);

        // check each regulation
        var cursor = anchoredFrom; // from the first after start
        while (cursor.next != null) // until the last
        {
            cursor = cursor.next;
            var point = cursor.linkedPoint;
            var flag = point.AltitudeRegulation;

            bool anchored = false;

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
                anchoredFrom.anchoredNext = cursor;
                cursor.anchoredPrev = anchoredFrom;
                cursor.anchoredNext = anchoredTo;
                anchoredTo.anchoredPrev = cursor;

                ComputeStraightInterval(anchoredFrom, cursor);
                ComputeStraightInterval(cursor, anchoredTo); // may not be needed - it's direct

                anchoredFrom = cursor;

                // ==  validate backwards ==
                var validationCursor = cursor.prev;

                if (validationCursor != null &&
                    validationCursor.linkedPoint.AltitudeRegulation != RoutePoint.AltitudeFlags.Exact)
                {
                    // previos anchor before the previos node
                    var validationAnchoredFrom = anchoredFrom;
                    var validationAnchoredTo = anchoredTo;

                    bool validationUpdated;
                    do
                    {
                        // shift validation interval to previous
                        validationAnchoredFrom = validationAnchoredFrom.anchoredPrev == validationCursor
                            ? validationAnchoredFrom.anchoredPrev.anchoredPrev
                            : validationAnchoredFrom.anchoredPrev;

                        validationAnchoredTo = validationAnchoredTo.anchoredPrev;


                        validationUpdated = false;

                        var computedToCheck = validationCursor.linkedPoint.GetAcceptedAltitude;
                        var computedWasAnchored = validationCursor.linkedPoint.Altitude.IsAnchored;

                        ComputeStraightInterval(validationAnchoredFrom, validationAnchoredTo);

                        var validationAnchored = AnchorForBelow(validationCursor.linkedPoint) ||
                                                 AnchorForAbove(validationCursor.linkedPoint);

                        var validationValueModified = computedToCheck != validationCursor.linkedPoint.GetAcceptedAltitude;
                        var validationAnchorModified = computedWasAnchored != validationAnchored;

                        validationUpdated = validationValueModified || validationAnchorModified;

                        // if validation cursor was not already an anchor insert it as an anchor
                        if (validationAnchored && !computedWasAnchored)
                        {
                            validationAnchoredFrom.anchoredNext = validationCursor;
                            validationCursor.anchoredPrev = validationAnchoredFrom;
                            validationCursor.anchoredNext = validationAnchoredTo;
                            validationAnchoredTo.anchoredPrev = validationCursor;
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
                            anchoredFrom.anchoredNext = anchoredTo;
                            anchoredTo.anchoredPrev = anchoredFrom;
                        }

                        // go to the prev node ( anchored or not )
                        validationCursor = validationCursor.prev;


                    }
                    while (
                        validationCursor != null &&
                        validationCursor.prev != null &&
                        validationUpdated &&
                        validationCursor.linkedPoint.AltitudeRegulation != RoutePoint.AltitudeFlags.Exact);
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

        void ComputeStraightInterval(RegulationNode from, RegulationNode to)
        {
            var totalDistance = 0f;
            var node = from;
            while (node != to)
            {
                node = node.next;
                totalDistance += node.distanceToPrevious;
            }

            var totalDiff = to.linkedPoint.GetAcceptedAltitude - from.linkedPoint.GetAcceptedAltitude;
            var lossForMile = totalDiff / totalDistance;

            for (int i = from.IndexInList + 1; i <= to.IndexInList; i++)
            {
                var regulation = regulations[i];
                var dif = regulation.distanceToPrevious * lossForMile;
                regulation.linkedPoint.Altitude.SetComputedValue(set.Points[i - 1].GetAcceptedAltitude + dif, false);
            }
        }

        // fil in altitudes for points between regulations
        anchoredFrom = regulations[0];
        while (anchoredFrom.next != null)
        {
            var totalDistance = anchoredFrom.next.distanceToPrevious;
            var totalDiff = anchoredFrom.next.linkedPoint.GetAcceptedAltitude - anchoredFrom.linkedPoint.GetAcceptedAltitude;
            var lossForMile = totalDiff / totalDistance;

            for (int i = anchoredFrom.PointIndexInSet + 1; i <= anchoredFrom.next.PointIndexInSet; i++)
            {

                var dif = set.Points[i].Distance * lossForMile;
                set.Points[i].Altitude.SetComputedValue(set.Points[i - 1].GetAcceptedAltitude + dif, false);
            }
            anchoredFrom = anchoredFrom.next;
        }

    }

    // ex: 180A3444B
    public static void ParseAltRegulation(string input, out int above, out int below, out int exact)
    {
        above = -1;
        below = -1;
        exact = -1;
        Regex re = new Regex(@"(\d+)([a-zA-Z]*)");
        var result = re.Matches(input);

        for (int i = 0; i < result.Count; i++)
        {
            var set = result[i];
            var number = int.Parse(set.Groups[1].Value);
            var indicator = set.Groups[2].Value;

            if (indicator.ToUpper() == "B")
            {
                below = number;
            }
            else if (indicator.ToUpper() == "A")
            {
                above = number;
            }
            else
            {
                exact = number;
            }
        }
    }

    // ex: 080/34
    public static bool ParseRelativeNode(string input, out int angle, out int distance)
    {
        angle = 0;
        distance = 0;

        if (input.Length <= 4 || input[3] != '/') return false;

        var rex = new Regex(@"(\-?)(\d+)\/(\d+)");

        var result = rex.Match(input);
        if (!string.IsNullOrEmpty(result.Groups[1].Value))
        {
            return false;
        }

        angle = Mathf.Clamp(int.Parse(result.Groups[2].Value), 0, 360);
        distance = int.Parse(result.Groups[3].Value);

        return true;
    }

    public static bool ParseRelativeNodeOnDirection(string input, out int distance)
    {
        distance = 0;

        if (input.Length <= 1|| input[0] != '/') return false;
        input = input.Remove(0, 1);

        return int.TryParse(input, out distance); 
    }

    public static bool ParseLiniarApproach(string input, out int angle)
    {
        angle = 0;

        if (input.Length != 3) return false;

        return int.TryParse(input, out angle); 
    }

    class RegulationNode
    {
        public RegulationNode prev;
        public RegulationNode next;

        public RegulationNode anchoredPrev;
        public RegulationNode anchoredNext;

        public RoutePoint linkedPoint;
        public int IndexInList;
        public float distanceToPrevious;

        public int PointIndexInSet;
    }

}
