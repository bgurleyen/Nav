using System.Collections.Generic;
using System.Text.RegularExpressions;

public class DataHandler
{
    public static void BuildSetDetails(RouteScriptableObject route)
    {
        BuildAltitudes(route);
        BuildSpeeds(route);
    }

    static void BuildSpeeds(RouteScriptableObject set, int startFrom = 270)
    {
        set.Points[0].RawSpeed = startFrom;
        var _lastRegulation = startFrom;

        for (var i = 0; i < set.Points.Length; i++)
        {
            var _point = set.Points[i];

            if(_point.IsSpeedRegulated)
            {
                _lastRegulation = _point.RawSpeed;
            }
            else
            {
                _point.SetSpeedComputed(_lastRegulation);
            }
        }
    }

    static void BuildAltitudes(RouteScriptableObject set, int startFrom = 40000)
    {
        if (set.Points[0].AltitudeRegulation == RoutePoint.AltitudeFlags.NotSet)
        {
            set.Points[0].RawAltitude = startFrom.ToString();
        }

        List<AltitudeRegulationNode> _regulations = null;
        var _distanceToPrevious = 0f;

        // the list is of the nodes of a linked tree
        for (var i = 0; i < set.Points.Length - 1; i++)
        {
            var _point = set.Points[i];

            // if it is not a current regulations interval started
            if (_regulations == null)
            {
                if (set.Points[i + 1].AltitudeRegulation == RoutePoint.AltitudeFlags.Exact)
                {
                    continue;
                }

                // start new regulations interval
                _regulations = new List<AltitudeRegulationNode>
                {
                    new AltitudeRegulationNode
                    {
                        IndexInList = i,
                        LinkedPoint = set.Points[i],
                        Next = null,
                        Prev = null
                    }
                };
                _distanceToPrevious = _point.Distance;
                continue;
            }

            _distanceToPrevious += _point.Distance;

            if (_point.AltitudeRegulation != RoutePoint.AltitudeFlags.NotSet)
            {
                var _lastRegulation = _regulations[_regulations.Count - 1];
                var _newRegulation = new AltitudeRegulationNode
                {
                    IndexInList = _regulations.Count,
                    PointIndexInSet = i,
                    LinkedPoint = _point,
                    Next = null,
                    Prev = _lastRegulation,
                    DistanceToPrevious = _distanceToPrevious
                };

                _lastRegulation.Next = _newRegulation;
                _regulations.Add(_newRegulation);
                _distanceToPrevious = 0;

                // if other exact node reached, close and compute current regulations interval
                if (set.Points[i].AltitudeRegulation == RoutePoint.AltitudeFlags.Exact)
                {
                    ComputeRegulationsInterval(_regulations, set);
                }
            }
        }
    }

    static void ComputeRegulationsInterval(List<AltitudeRegulationNode> regulations, RouteScriptableObject set)
    {
        var _anchoredFrom = regulations[0];
        var _anchoredTo = regulations[regulations.Count - 1];
        _anchoredFrom.AnchoredNext = _anchoredTo;

        // initial compute straight line
        ComputeStraightInterval(_anchoredFrom, _anchoredTo);

        // check each regulation
        var _cursor = _anchoredFrom; // from the first after start
        while (_cursor.Next != null) // until the last
        {
            _cursor = _cursor.Next;
            var _point = _cursor.LinkedPoint;
            var _flag = _point.AltitudeRegulation;

            var _anchored = false;

            switch (_flag)
            {
                case RoutePoint.AltitudeFlags.Below:
                    if (AnchorForBelow(_point))
                    {
                        _anchored = true;
                    }
                    break;

                case RoutePoint.AltitudeFlags.Above:
                    if (AnchorForAbove(_point))
                    {
                        _anchored = true;
                    }
                    break;
                case RoutePoint.AltitudeFlags.AboveBelow:
                    if (AnchorForBelow(_point)
                    || AnchorForAbove(_point))
                    {
                        _anchored = true;
                    }

                    break;
            }

            if (_anchored)
            {
                // cursor will became a new anchor
                _anchoredFrom.AnchoredNext = _cursor;
                _cursor.AnchoredPrev = _anchoredFrom;
                _cursor.AnchoredNext = _anchoredTo;
                _anchoredTo.AnchoredPrev = _cursor;

                ComputeStraightInterval(_anchoredFrom, _cursor);
                ComputeStraightInterval(_cursor, _anchoredTo); // may not be needed - it's direct

                _anchoredFrom = _cursor;

                // ==  validate backwards ==
                var _validationCursor = _cursor.Prev;

                if (_validationCursor != null &&
                    _validationCursor.LinkedPoint.AltitudeRegulation != RoutePoint.AltitudeFlags.Exact)
                {
                    // previos anchor before the previos node
                    var _validationAnchoredFrom = _anchoredFrom;
                    var _validationAnchoredTo = _anchoredTo;

                    bool _validationUpdated;
                    do
                    {
                        // shift validation interval to previous
                        _validationAnchoredFrom = _validationAnchoredFrom.AnchoredPrev == _validationCursor
                            ? _validationAnchoredFrom.AnchoredPrev.AnchoredPrev
                            : _validationAnchoredFrom.AnchoredPrev;

                        _validationAnchoredTo = _validationAnchoredTo.AnchoredPrev;


                        _validationUpdated = false;

                        var _computedToCheck = _validationCursor.LinkedPoint.GetAcceptedAltitude;
                        var _computedWasAnchored = _validationCursor.LinkedPoint.Altitude.IsAnchored;

                        ComputeStraightInterval(_validationAnchoredFrom, _validationAnchoredTo);

                        var _validationAnchored = AnchorForBelow(_validationCursor.LinkedPoint) ||
                                                 AnchorForAbove(_validationCursor.LinkedPoint);

                        var _validationValueModified = _computedToCheck != _validationCursor.LinkedPoint.GetAcceptedAltitude;
                        var _validationAnchorModified = _computedWasAnchored != _validationAnchored;

                        _validationUpdated = _validationValueModified || _validationAnchorModified;

                        // if validation cursor was not already an anchor insert it as an anchor
                        if (_validationAnchored && !_computedWasAnchored)
                        {
                            _validationAnchoredFrom.AnchoredNext = _validationCursor;
                            _validationCursor.AnchoredPrev = _validationAnchoredFrom;
                            _validationCursor.AnchoredNext = _validationAnchoredTo;
                            _validationAnchoredTo.AnchoredPrev = _validationCursor;
                        }

                        // if validation is still an anchor, but other kind or was not an anchor 
                        if (_validationAnchored && _validationUpdated)
                        {
                            ComputeStraightInterval(_validationAnchoredFrom, _validationCursor);
                            ComputeStraightInterval(_validationCursor, _validationAnchoredTo); // may not be needed - it's direct
                        }

                        // if validation cursor stopped being an achor remove the anchor node
                        if (!_validationAnchored && _computedWasAnchored)
                        {
                            _anchoredFrom.AnchoredNext = _anchoredTo;
                            _anchoredTo.AnchoredPrev = _anchoredFrom;
                        }

                        // go to the prev node ( anchored or not )
                        _validationCursor = _validationCursor.Prev;


                    }
                    while (
                        _validationCursor != null &&
                        _validationCursor.Prev != null &&
                        _validationUpdated &&
                        _validationCursor.LinkedPoint.AltitudeRegulation != RoutePoint.AltitudeFlags.Exact);
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
            var _totalDistance = 0f;
            var _node = from;
            while (_node != to)
            {
                _node = _node.Next;
                _totalDistance += _node.DistanceToPrevious;
            }

            var _totalDiff = to.LinkedPoint.GetAcceptedAltitude - from.LinkedPoint.GetAcceptedAltitude;
            var _lossForMile = _totalDiff / _totalDistance;

            for (var i = from.IndexInList + 1; i <= to.IndexInList; i++)
            {
                var _regulation = regulations[i];
                var _dif = _regulation.DistanceToPrevious * _lossForMile;
                _regulation.LinkedPoint.Altitude.SetComputedValue(set.Points[i - 1].GetAcceptedAltitude + _dif, false);
            }
        }

        // fil in altitudes for points between regulations
        _anchoredFrom = regulations[0];
        while (_anchoredFrom.Next != null)
        {
            var _totalDistance = _anchoredFrom.Next.DistanceToPrevious;
            var _totalDiff = _anchoredFrom.Next.LinkedPoint.GetAcceptedAltitude - _anchoredFrom.LinkedPoint.GetAcceptedAltitude;
            var _lossForMile = _totalDiff / _totalDistance;

            for (int i = _anchoredFrom.PointIndexInSet + 1; i <= _anchoredFrom.Next.PointIndexInSet; i++)
            {

                var _dif = set.Points[i].Distance * _lossForMile;
                set.Points[i].Altitude.SetComputedValue(set.Points[i - 1].GetAcceptedAltitude + _dif, false);
            }
            _anchoredFrom = _anchoredFrom.Next;
        }

    }

    // ex: 180A3444B
    public static bool ParseAltRegulation(string input, out int above, out int below, out int exact)
    {
        above = -1;
        below = -1;
        exact = -1;
        var _re = new Regex(@"(\d+)([a-zA-Z]{0,1})");
        var _result = _re.Matches(input);

        if (_result.Count == 0)
        {
            // @#$ also exclude leftovers 234AA234B : second A needs to flag error
            return false;
        }

        for (var i = 0; i < _result.Count; i++)
        {
            var _set = _result[i];
            var _number = int.Parse(_set.Groups[1].Value);
            var _indicator = _set.Groups[2].Value;

            switch (_indicator.ToUpper())
            {
                case "B":
                    below = _number;
                    break;
                case "A":
                    above = _number;
                    break;
                default:
                    exact = _number;
                    break;
            }
        }

        return true;
    }

    

  

   

    class AltitudeRegulationNode
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
