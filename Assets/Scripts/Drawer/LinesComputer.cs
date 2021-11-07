    using System;
    using UnityEngine;

    public static class LinesComputer
    {
        private static float _unitLength;

        public static void Init(GameSettingsScriptableObject settings)
        {
            _unitLength = settings.UnitLength;
        }
        
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
            var nextPoint = points[toDataPointIndex];

            var forceEndStraight = true;

            if (toDataPointIndex + 1 < points.Length)
            {
                forceEndStraight = points[toDataPointIndex + 1].IsAfterDiscontinuity;
            }

            RoutePoint notToCloseSecondPoint = null;

            // there are no more points to create a curve to ( in which case continue with straight line on current segment )
            if (toDataPointIndex != points.Length - 1)
            {
                var secondPoint = points[toDataPointIndex + 1];
                if (secondPoint.Distance > 0.5f)
                {
                    notToCloseSecondPoint = secondPoint;
                }
            }

            return ComputeLine(lastLine, out line, nextPoint, notToCloseSecondPoint, forceEndStraight);
        }

        public static bool ComputeLine(MarkLine lastLine, out MarkLine line, RoutePoint nextPoint,
            RoutePoint notTooCloseSecondPoint = null, bool forceEndStraight = false)
        {
            float angleBetween = 180;

            // there are no more points to create a curve to ( in which case continue with straight line on current segment )
            if (notTooCloseSecondPoint != null)
            {
                angleBetween = Geometry.AngleBetweenNodes(nextPoint.Degrees, notTooCloseSecondPoint.Degrees);
            }

            var startsStraight = lastLine.LinkedPoint != null &&
                                  (lastLine.LinkedPoint.IsAfterDiscontinuity || lastLine.LinkedPoint.IsHiddenLine);

            var endsStraight = forceEndStraight;

            var lastEndOffset = lastLine.LinkedPoint != null && startsStraight
                ? lastLine.EndPosition
                : lastLine.EndOffsetPosition;

            if (Math.Abs(angleBetween - 180) < 0.2f || endsStraight)
            {
                // straight line  
                if (!GenerateLine(nextPoint, lastLine.EndPosition, lastEndOffset, out line))
                {
                    return false;
                }
            }
            else
            {
                // try relaxed turn. Update: don't use relaxed as the radius can become very big, and there is no advantage to it. Just go with regular curve
                if (true || !GenerateCurve(Drawer.RelaxedRadius, nextPoint, notTooCloseSecondPoint, angleBetween,
                    lastLine.EndPosition,
                    lastEndOffset,
                    out line))
                {
                    if (!GenerateCurve(Drawer.GetMinRadius, nextPoint, notTooCloseSecondPoint, angleBetween,
                        lastLine.EndPosition,
                        lastEndOffset,
                        out line))
                    {
                        GenerateDoubleCurve(Drawer.GetMinRadius, Drawer.RelaxedRadius, nextPoint,
                            notTooCloseSecondPoint,
                            angleBetween,
                            lastLine.EndPosition,
                            lastEndOffset, out line);
                    }
                }
            }

            return true;
        }

        static bool GenerateLine(RoutePoint nextPoint, Vector3 lastEndPosition, Vector3 lastEndOffset,
            out MarkLine line)
        {
            line = new MarkLine(nextPoint, _unitLength);
            return line.Init(lastEndPosition, lastEndOffset, nextPoint);
        }

        static bool GenerateCurve(float chosenRadius, RoutePoint nextPoint, RoutePoint secondPoint, float angleBetween,
            Vector3 lastEndPosition, Vector3 lastEndOffset, out MarkLine line)
        {
            var tangentToMiddle = Line.GetTangentToMiddle(chosenRadius, angleBetween);

            if (tangentToMiddle <= nextPoint.Distance && tangentToMiddle <= secondPoint.Distance &&
                tangentToMiddle <= chosenRadius)
            {
                var l = new Curve(nextPoint, _unitLength);
                l.Init(lastEndPosition, lastEndOffset, nextPoint, secondPoint, tangentToMiddle, chosenRadius);
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
            var l = new DoubleCurve(nextPoint, _unitLength);
            l.Init(lastEndPosition, lastEndOffset, nextPoint, secondPoint, angleBetween, smallRadius, bigRadius);
            line = l;
        }
    }