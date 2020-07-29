using System;
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

    public bool IsCurrent { get; internal set; }
    public bool IsSelected { get; internal set; }
    public bool IsModified { get; internal set; }

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

    public bool GetAltitudeIsRestricted(out string displayValue)
    {
        displayValue = Altitude.GetDisplayValue(RawAltitude, out var _isRestricted);
        return _isRestricted;
    }

    public bool GetSpeedIsRestricted(out string displayValue)
    {
        displayValue = Speed.GetDisplayValue(RawSpeed, out var _isRestricted).ToString();
        return _isRestricted;
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
            IsCurrent = IsCurrent,
            IsSelected = IsSelected,
            IsModified = IsModified,
            Altitude = Altitude ?? Altitude.Clone(),
            Speed = Speed ?? Speed.Clone(),
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
            var _displayed = GetDisplayValue(rawSpeed, out var _);
            if (acceptedAltitude > 10000)
            {
                return _displayed;
            }

            return Mathf.Min(_displayed, 240);
        }
    }

    public class AltitudeData
    {
        public int RestrictionAbove = -1;
        public int RestrictionBelow = -1;
        public int RestrictionExact = -1;

        public float ComputedValue = -1;
        public bool IsAnchored;

        string parsedAltitude = null;

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

        void ParseRawAltitude(string rawAltitude)
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
            var _flag = GetFlag(parsedAltitude);
            switch (_flag)
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