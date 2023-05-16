using System;
using Legacy;
using Navigation;
using UnityEngine;

[Serializable]
public class RoutePoint
{
    public int ID;
    public string Name = "";
    public float RawDegrees;
    public float Distance;
    public int RawSpeed = 0;
    public string RawAltitude = "";
    public string Details = "";

    public Vector2 CartesianPosition { get; set; }
    public int LinearApproachAngle { get; set; }
    public AltitudeData Altitude { get; private set; } = new AltitudeData();
    public SpeedData Speed { get; private set; } = new SpeedData();

    

    public bool IsSelected { get; internal set; }
    public bool IsModified { get; internal set; }
    public bool IsSpeedModified { get; internal set; }
    public bool IsAltitudeModified { get; internal set; }

    public float Degrees => 360 - RawDegrees;
    public bool IsAfterDiscontinuity => Details.Contains("D");
    public bool IsCenter => Details.Contains("C");
    public bool IsLinearApproach => Details.Contains("L");
    public bool IsHiddenLine => Details.Contains("H");
    public bool IsPositionNode => Details.Contains("P");

    public AltitudeFlags AltitudeRegulation => Altitude.GetFlag(RawAltitude);
    public string DisplayAltitude => Altitude.GetDisplayValue(RawAltitude, out var _);
    public int DisplayDegrees => (int) Degrees;
    public float GetAcceptedAltitude => Altitude.GetAcceptedValue();

    public bool IsSpeedRegulated => Speed.IsSpeedRegulated(RawSpeed);

    public void SetSpeedComputed(int lastRegulation) =>
        Speed.SetComputedValue(lastRegulation, (int) GetAcceptedAltitude);

    /// <summary>
    /// Can be less precise than compute node with degrees and distance directly
    /// </summary>
    /// <param name="position"></param>
    /// <param name="previousPoint"></param>
    /// <param name="newId"></param>
    /// <param name="details"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    public static RoutePoint ConstructFromPosition(Vector2 position, RoutePoint previousPoint, int newId = -1, string details = "", string name = "")
    {
        var rp = new RoutePoint{ CartesianPosition = position, ID = 0};

        if (previousPoint == null)
        {
            rp.Distance = 0;
            return rp;
        }
        
        rp.ID = newId < 0 ? previousPoint.ID + 1 : newId;
        rp.Distance = Vector2.Distance(position, previousPoint.CartesianPosition);
        rp.RawDegrees = Geometry.AngleOfPosition(position, previousPoint.CartesianPosition);
        if (details != "")
        {
            rp.Details = details;
        }

        if (name != "")
        {
            rp.Name = name;
        }


        return rp;
    }

    public bool GetIsDisplayCurrent
    {
        get
        {
            var nodeCursor = PositionVirtualNode.GetNodeTo;
            while (nodeCursor.IsPositionNode || nodeCursor.ID == ID)
            {
                if (nodeCursor.ID == ID)
                {
                    return true;
                }

                nodeCursor = Session.ActiveRoute.Points[Session.ActiveRoute.Points.GetNodeIndex(nodeCursor.ID) + 1];
            }

            return false;

        }
    }

    public bool GetAltitudeIsRestricted(out string displayValue)
    {
        displayValue = Altitude.GetDisplayValue(RawAltitude, out var isRestricted);
        return isRestricted;
    }

    public bool GetSpeedIsRestricted(out string displayValue)
    {
        displayValue = Speed.GetDisplayValue(RawSpeed, out var isRestricted).ToString();
        return isRestricted;
    }

    internal RoutePoint Clone()
    {
        return new RoutePoint
        {
            ID = ID,
            Name = Name,
            Details = Details,
            RawDegrees = RawDegrees,
            Distance = Distance,
            RawSpeed = RawSpeed,
            RawAltitude = RawAltitude,
            // IsCurrent = IsCurrent,
            IsSelected = IsSelected,
            IsModified = IsModified,
            Altitude = Altitude?.Clone(),
            Speed = Speed?.Clone(),
            CartesianPosition = CartesianPosition
        };
    }

    public bool IsSkippable => IsHiddenLine || IsPositionNode;

    // todo remove specific flags
    public void ClearDetails()
    {
        Details = "";
    }

    public void IndicateDiscontinuityBefore()
    {
        Details = "D";
    }

    public void IndicateDirectApproach(int angle)
    {
        Details = "L";
        LinearApproachAngle = angle;
    }

    public void IndicateHiddenLine()
    {
        Details = "H";
    }

    public void IndicateFakeFar()
    {
        Details = "F";
    }

    public static bool HaveSamePosition(RoutePoint a, RoutePoint b)
    {
        return (a.CartesianPosition - b.CartesianPosition).sqrMagnitude < 0.01f;
    }

    public class SpeedData
    {
        public int ComputedValue = -1;

        public bool IsSpeedRegulated(int rawSpeed) => rawSpeed != 0;

        public SpeedData Clone()
        {
            return new SpeedData
            {
                ComputedValue = ComputedValue
            };
        }

        internal int GetDisplayValue(int rawSpeed, out bool isRestricted)
        {
            isRestricted = IsSpeedRegulated(rawSpeed);

            return isRestricted ? rawSpeed : ComputedValue;
        }

        public void SetComputedValue(int lastRegulation, int acceptedAltitude)
        {
            if (acceptedAltitude > 10000)
            {
                ComputedValue = lastRegulation;
            }
            else
            {
                ComputedValue = Mathf.Min(240, lastRegulation);
            }
        }

        public int GetLinearValue(int rawSpeed, int acceptedAltitude)
        {
            var displayed = GetDisplayValue(rawSpeed, out var _);
            if (acceptedAltitude > 10000)
            {
                return displayed;
            }

            return Mathf.Min(displayed, 240);
        }
    }

    public class AltitudeData
    {
        public int RestrictionAbove = -1;
        public int RestrictionBelow = -1;
        public int RestrictionExact = -1;

        public float ComputedValue = -1;
        public bool IsAnchored;

        private string parsedAltitude = null;

        public AltitudeData Clone()
        {
            return new AltitudeData
            {
                RestrictionAbove = RestrictionAbove,
                RestrictionBelow = RestrictionBelow,
                RestrictionExact = RestrictionExact,
                ComputedValue = ComputedValue,
                IsAnchored = IsAnchored,
                parsedAltitude = parsedAltitude
            };
        }

        private void ParseRawAltitude(string rawAltitude)
        {
            if (parsedAltitude == rawAltitude) return;
            ComputedValue = -1;
            DataHandler.ParseAltRegulation(rawAltitude, out RestrictionAbove, out RestrictionBelow,
                out RestrictionExact);
            parsedAltitude = rawAltitude;
        }

        public AltitudeFlags GetFlag(string rawAltitude)
        {
            ParseRawAltitude(rawAltitude);

            return string.IsNullOrEmpty(rawAltitude)
                ? AltitudeFlags.NotSet
                : RestrictionAbove > 0 && RestrictionBelow > 0
                    ? AltitudeFlags.AboveBelow
                    : RestrictionBelow > 0
                        ? AltitudeFlags.Below
                        : RestrictionAbove > 0
                            ? AltitudeFlags.Above
                            : AltitudeFlags.Exact;
        }

        public string GetDisplayValue(string rawAltitude, out bool isRestricted)
        {
            isRestricted = GetFlag(rawAltitude) != AltitudeFlags.NotSet;
            return !isRestricted
                ? ((int) ComputedValue).ToString()
                : parsedAltitude;
        }

        public void SetComputedValue(float altitude, bool anchored)
        {
            ComputedValue = altitude;
            IsAnchored = anchored;
        }

        internal float GetAcceptedValue()
        {
            var flag = GetFlag(parsedAltitude);
            switch (flag)
            {
                case AltitudeFlags.Exact:
                    return RestrictionExact;
                default:
                    return ComputedValue;
            }
        }
    }

    public enum AltitudeFlags
    {
        Above,
        Below,
        AboveBelow,
        Exact,
        NotSet
    };
}