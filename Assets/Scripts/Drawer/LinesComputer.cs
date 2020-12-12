    using System;
    using UnityEngine;

    public static class LinesComputer
    {
        public static bool GetNextLine(MarkLine lastLine, int fromDataPointIndex, RoutePoint[] points,
            out MarkLine line,
            out int toDataPointIndex)
        {
            // the line may be already begun if previous was a curve 
            line = null;
            if (fromDataPointIndex == points.Length - 1)
            {
                toDataPointIndex = fromDataPointIndex;
                return false;
            }

            toDataPointIndex = fromDataPointIndex + 1;
            var _nextPoint = points[toDataPointIndex];

            var _forceEndStraight = true;

            if (toDataPointIndex + 1 < points.Length)
            {
                _forceEndStraight = points[toDataPointIndex + 1].IsAfterDiscontinuity;
            }

            RoutePoint _notToCloseSecondPoint = null;

            // there are no more points to create a curve to ( in which case continue with straight line on current segment )
            if (toDataPointIndex != points.Length - 1)
            {
                var _secondPoint = points[toDataPointIndex + 1];
                if (_secondPoint.Distance > 0.5f)
                {
                    _notToCloseSecondPoint = _secondPoint;
                }
            }

            return ComputeLine(lastLine, out line, _nextPoint, _notToCloseSecondPoint, _forceEndStraight);
        }

        public static bool ComputeLine(MarkLine lastLine, out MarkLine line, RoutePoint nextPoint,
            RoutePoint notTooCloseSecondPoint = null, bool forceEndStraight = false)
        {
            float _angleBetween = 180;

            // there are no more points to create a curve to ( in which case continue with straight line on current segment )
            if (notTooCloseSecondPoint != null)
            {
                _angleBetween = Geometry.AngleBetweenNodes(nextPoint.Degrees, notTooCloseSecondPoint.Degrees);
            }

            var _startsStraight = lastLine.LinkedPoint != null &&
                                  (lastLine.LinkedPoint.IsAfterDiscontinuity || lastLine.LinkedPoint.IsHiddenLine);

            var _endsStraight = forceEndStraight;

            var _lastEndOffset = lastLine.LinkedPoint != null && _startsStraight
                ? lastLine.EndPosition
                : lastLine.EndOffsetPosition;

            if (Math.Abs(_angleBetween - 180) < 0.2f || _endsStraight)
            {
                // straight line  
                GenerateLine(nextPoint, lastLine.EndPosition, _lastEndOffset, out line);
            }
            else
            {
                // try relaxed turn. Update: don't use relaxed as the radius can become very big, and there is no advantage to it. Just go with regular curve
                if (true || !GenerateCurve(Drawer.RelaxedRadius, nextPoint, notTooCloseSecondPoint, _angleBetween,
                    lastLine.EndPosition,
                    _lastEndOffset,
                    out line))
                {
                    if (!GenerateCurve(Drawer.GetMinRadius, nextPoint, notTooCloseSecondPoint, _angleBetween,
                        lastLine.EndPosition,
                        _lastEndOffset,
                        out line))
                    {
                        GenerateDoubleCurve(Drawer.GetMinRadius, Drawer.RelaxedRadius, nextPoint,
                            notTooCloseSecondPoint,
                            _angleBetween,
                            lastLine.EndPosition,
                            _lastEndOffset, out line);
                    }
                }
            }

            return true;
        }

        static void GenerateLine(RoutePoint nextPoint, Vector3 lastEndPosition, Vector3 lastEndOffset,
            out MarkLine line)
        {
            var _l = new MarkLine(nextPoint);
            _l.Init(lastEndPosition, lastEndOffset, nextPoint);
            line = _l;
        }

        static bool GenerateCurve(float chosenRadius, RoutePoint nextPoint, RoutePoint secondPoint, float angleBetween,
            Vector3 lastEndPosition, Vector3 lastEndOffset, out MarkLine line)
        {
            var _tangentToMiddle = Line.GetTangentToMiddle(chosenRadius, angleBetween);

            if (_tangentToMiddle <= nextPoint.Distance && _tangentToMiddle <= secondPoint.Distance &&
                _tangentToMiddle <= chosenRadius)
            {
                var l = new Curve(nextPoint);
                l.Init(lastEndPosition, lastEndOffset, nextPoint, secondPoint, _tangentToMiddle, chosenRadius);
                line = l;
                return true;
            }

            line = null;
            return false;
        }

        static void GenerateDoubleCurve(float smallRadius, float bigRadius, RoutePoint nextPoint,
            RoutePoint secondPoint,
            float angleBetween, Vector3 lastEndPosition, Vector3 lastEndOffset, out MarkLine line)
        {
            var l = new DoubleCurve(nextPoint);
            l.Init(lastEndPosition, lastEndOffset, nextPoint, secondPoint, angleBetween, smallRadius, bigRadius);
            line = l;
        }
    }