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


    public Vector2 CartesianPosition;
    public int LinearApproachAngle { get; set; }
    public AltitudeData Altitude { get; private set; } = new AltitudeData();
    public SpeedData Speed { get; private set; } = new SpeedData();

    public bool IsCurrent { get; internal set; }
    public bool IsSelected { get; internal set; }
    public bool IsModified { get; internal set; }
    public bool IsFirstAfterFreeFlight { get; set; }

    public float Degrees => 360 - RawDegrees;
    public bool IsAfterDiscontinuity => Details.Contains("D");
    public bool IsCenter => Details.Contains("C");
    public bool IsLinearApproach => Details.Contains("L");
    public bool IsHiddenLine => Details.Contains("H");

    public AltitudeFlags AltitudeRegulation => Altitude.GetFlag(RawAltitude);
    public string DisplayAltitude => Altitude.GetDisplayValue(RawAltitude);
    public int DisplayDegrees => (int)Degrees;
    public float GetAcceptedAltitude => Altitude.GetAcceptedValue();

    public bool IsSpeedRegulated => Speed.IsSpeedRegulated(RawSpeed);
    public int DisplaySpeed => Speed.GetDisplayValue(RawSpeed);
    public void SetSpeedComputed(int lastRegulation) => Speed.SetComputedValue(lastRegulation, (int)GetAcceptedAltitude);

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
            CartesianPosition =  CartesianPosition
        };
    }

    // todo remove specific flags
    public void ClearDetails()
    {
        Details = "";
    }

    public void IndicateDiscontinuityBefore()
    {
        Details = "D";
    }

    public void IndicateLinearApproach(int angle)
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

        internal int GetDisplayValue(int rawSpeed)
        {
            return IsSpeedRegulated(rawSpeed) ? rawSpeed : ComputedValue;
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

        public int GetLiniarValue(int rawSpeed, int acceptedAltitude)
        {
            var displayed = GetDisplayValue(rawSpeed);
            if(acceptedAltitude>10000)
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
            DataHandler.ParseAltRegulation(rawAltitude, out RestrictionAbove, out RestrictionBelow, out RestrictionExact);
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

        public string GetDisplayValue(string rawAltitude)  => GetFlag(rawAltitude) == AltitudeFlags.NotSet 
            ? ((int) ComputedValue).ToString() 
            : parsedAltitude;

        public void SetComputedValue(float altitude, bool anchored)
        {
            ComputedValue = altitude;
            IsAnchored = anchored;
        }

        internal float GetAcceptedValue()
        {
            var flag = GetFlag(parsedAltitude);
            switch(flag)
            {
                case AltitudeFlags.Exact:
                    return RestrictionExact;
                default:
                    return ComputedValue;
            }
        }
    }

    public enum AltitudeFlags { Above, Below, AboveBelow, Exact, NotSet };
}
