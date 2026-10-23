
using Gamelogic.Extensions;
using UnityEngine;

namespace Navigation.Data
{

    public class infoFMC : Singleton<infoFMC>
    {
        private int previousPrvIndex = -1;
        private int Level = Calculator.Level;

        public FMC Fmc = new FMC();

        public static FMC Live => Instance != null ? Instance.Fmc : null;

        

        public bool IsPopulated =>
            Fmc != null
            && !string.IsNullOrEmpty(Fmc.Initref.Destination)
            && !string.IsNullOrEmpty(Fmc.Crz.Altitude)
            && !string.IsNullOrEmpty(Fmc.Prog.NxtName);

        public void TryPopulate()
        {
            ComputeFMCFields();
        }

        private void Start()
        {
            ComputeFMCFields();
            InvokeRepeating(nameof(ComputeFMCFields), 1f, 1f);
        }

        private void ComputeFMCFields()
        {

            double PrvAltitude, Altitude;
            int Speed, VS, ff;
            double Distance;

            RouteScriptableObject activePoints = Session.ActiveRoute;
            if (activePoints?.Points == null || activePoints.Points.Length == 0)
            {
                return;
            }

            var levelData = Session.CurrentLevel?.levelInfo;
            if (levelData == null)
            {
                return;
            }

            PrvAltitude = Calculator.CAltitude;

            Calculator.WindElements WE;
            int prvWptIdx = Session.PlayerAircraft != null ? PositionVirtualNode.PassedNodeIndex : -1;
            int WPTCount = activePoints.Points.Length;
            double[] fr_onpoint = new double[WPTCount];
            int[] GS_onpoint = new int[WPTCount];
            double[] totalDistLeft = new double[WPTCount];
            double RW_Alt = ResolveEndOfDescentAltitude(activePoints);
            double fuelBurn, fuelRemaining = Calculator.totalFuel / 100;

            Fmc.Initref.GWT = "" + (long)(levelData.ZFW + Calculator.totalFuel / 100);
            Fmc.Initref.Destination = levelData.Destination;
            Fmc.Initref.RW = levelData.Runway;
            Fmc.Initref.Field = levelData.FieldInfo;
            Fmc.Initref.Freq = levelData.Freq;
            Fmc.Initref.Course = levelData.Course;
            Fmc.Initref.F15 = "" + (levelData.F30Speed - 10);
            Fmc.Initref.F30 = levelData.F30Speed.ToString();
            Fmc.Initref.F40 = "" + (levelData.F30Speed + 10);
            Fmc.Initref.Vref = "" + levelData.F30Speed;
            Fmc.Initref.GlideSlope = levelData.GlideSlope;

            Fmc.Rte.Destination = levelData.Destination;
            Fmc.Rte.RW = levelData.Runway;
            Fmc.Rte.Origin = Session.RteOrigin;
            Fmc.Des.RWAltitude = Calculator.FormatFmcAltitude((long)RW_Alt);
            Fmc.Des.EconSpeed = Calculator.FormatMachIas(levelData.DesEconMach, levelData.DesEconSpeed);

            long cruiseAltitude = Calculator.RouteIdZeroAltitude(activePoints);
            Fmc.Crz.Destination = levelData.Destination;
            Fmc.Crz.Altitude = Calculator.FormatFmcCruiseAltitude(cruiseAltitude);
            Fmc.Crz.Speed = Calculator.FormatFmcSpeedDisplay(
                levelData.CrzSpeed, cruiseAltitude, machDigits: 3);
            Fmc.Crz.ActualWind = "" + Calculator.CWind;

            Fmc.Arr.Destination = levelData.Destination;
            Fmc.Arr.STAR = levelData.Star;
            Fmc.Arr.Transition = levelData.Transition;
            Fmc.Arr.RW = levelData.Runway;

            if (prvWptIdx < 0 || prvWptIdx + 1 >= activePoints.Points.Length || Session.PlayerAircraft == null)
            {
                return;
            }

            var previousPoint = activePoints.Points[prvWptIdx];
            var nextPoint = activePoints.Points[prvWptIdx + 1];
            if (previousPoint == null || nextPoint == null)
                return;

            for (int i = prvWptIdx + 1; i < activePoints.Points.Length; i++)
            {
                var leg = activePoints.Points[i];
                if (leg?.Altitude == null || leg.Speed == null)
                    continue;

                Altitude = (int)leg.Altitude.ComputedValue;
                Speed = leg.Speed.ComputedValue;
                if (Speed < 0) Speed = 230; // Change Computed -1 speed

                Distance = System.Math.Round(leg.Distance, 2);
                WE = Calculator.CalculateWindElements(Altitude, Speed, 0);
                double gs = WE.GS < 1 ? 1 : WE.GS;
                double minutes = Distance / gs * 60.0;
                if (minutes < 0.001)
                    minutes = 0.001;
                VS = (int)((Altitude - PrvAltitude) / minutes);
                ff = (Altitude - RW_Alt) > 3000 ? Calculator.FuelFlowFor((PrvAltitude - Altitude) / 2, VS, Speed) : 270;
                if (ff < 95) ff = 95;
                fuelBurn = System.Math.Round((Distance / gs * ff * 2 / 100), 3);
                fuelRemaining -= fuelBurn;

                totalDistLeft[i] = (i == prvWptIdx + 1)
                    ? Session.PlayerAircraft.ComputedDistanceLeftOnSegment
                    : totalDistLeft[i - 1] + Distance;
                PrvAltitude = Altitude;
                fr_onpoint[i] = fuelRemaining;
                GS_onpoint[i] = (int)gs;

            }

            Fmc.Crz.FuelAtDestination = "" + System.Math.Round(fr_onpoint[WPTCount - 1], 2);

            Fmc.Prog.PrvName = "" + previousPoint.Name; 
       


            if (prvWptIdx != previousPrvIndex) // Catch the actual info while passing the point
            {
                Fmc.Prog.PrvCrossAltitude = Calculator.FormatFmcAltitude((long)Calculator.CAltitude);
                Fmc.Prog.PrvActualTime = "" + GameTime.timerFMC;
                Fmc.Prog.PrvActualFuel = "" + System.Math.Round(Calculator.totalFuel / 100, 1);


            }

            previousPrvIndex = prvWptIdx;

            var secondPoint = prvWptIdx + 2 < activePoints.Points.Length
                ? activePoints.Points[prvWptIdx + 2]
                : null;
            double nextGs = GS_onpoint[prvWptIdx + 1] > 0 ? GS_onpoint[prvWptIdx + 1] : 1;
            double destGs = GS_onpoint[WPTCount - 1] > 0 ? GS_onpoint[WPTCount - 1] : 1;
            double secondGs = secondPoint != null && GS_onpoint[prvWptIdx + 2] > 0 ? GS_onpoint[prvWptIdx + 2] : 1;

            Fmc.Prog.NxtName = "" + nextPoint.Name;
            Fmc.Prog.NxtDTG = "" + (int)totalDistLeft[prvWptIdx + 1];
            Fmc.Prog.NxtETA = "" + GameTime.FormatFMCTime(GameTime.timer +
                                                          (((float)totalDistLeft[prvWptIdx + 1] /
                                                            (float)nextGs) * 3600));
            Fmc.Prog.NxtFUEL = "" + System.Math.Round(fr_onpoint[prvWptIdx + 1], 1);
            Fmc.Prog.SecondName = secondPoint != null ? "" + secondPoint.Name : "";
            Fmc.Prog.SecondDTG = secondPoint != null ? "" + (int)totalDistLeft[prvWptIdx + 2] : "";
            Fmc.Prog.SecondETA = secondPoint != null
                ? "" + GameTime.FormatFMCTime(GameTime.timer +
                                              (((float)totalDistLeft[prvWptIdx + 2] / (float)secondGs) *
                                               3600))
                : "";
            Fmc.Prog.SecondFUEL = secondPoint != null
                ? "" + System.Math.Round(fr_onpoint[prvWptIdx + 2], 1)
                : "";
            Fmc.Prog.Destination = levelData.Destination;
            Fmc.Prog.DestDTG = "" + (int)totalDistLeft[WPTCount - 1];
            Fmc.Prog.DestETA = "" + GameTime.FormatFMCTime(GameTime.timer +
                                                           (((float)totalDistLeft[WPTCount - 1] /
                                                             (float)destGs) * 3600));
            Fmc.Prog.DestFUEL = "" + System.Math.Round(fr_onpoint[WPTCount - 1], 1);
            Fmc.Prog.ActualWind = "" + Calculator.CWind;
            Fmc.Prog.FuelQty = "" + System.Math.Round(Calculator.totalFuel / 100, 1);

            }

        private static double ResolveEndOfDescentAltitude(RouteScriptableObject route)
        {
            var points = route.Points;
            for (var i = points.Length - 1; i >= 0; i--)
            {
                var point = points[i];
                var alt = point?.Altitude;
                if (alt == null)
                    continue;
                if (alt.ComputedValue > 0)
                {
                    return alt.ComputedValue;
                }

                if (alt.RestrictionExact > 0)
                {
                    return alt.RestrictionExact;
                }

                if (alt.RestrictionBelow > 0)
                {
                    return alt.RestrictionBelow;
                }

                if (!string.IsNullOrEmpty(point.RawAltitude)
                    && float.TryParse(point.RawAltitude, out var raw)
                    && raw > 0)
                {
                    return raw;
                }
            }

            if (Move.Instance != null)
            {
                var runwayAlt = Move.Instance.RunwayAltitudeFeet();
                if (runwayAlt > 0)
                {
                    return runwayAlt;
                }
            }

            return -1;
        }

        const float FeetPerNm = 6076.12f;
        const float MinDescentDistanceNm = 0.05f;

        public void UpdateDescentToPointBeforeRunway()
        {
            Fmc.Des.WptAltFix = "";
            Fmc.Des.FPA = FormatActualFpa();
            Fmc.Des.VB = "";
            Fmc.Des.VS = "";

            var route = Session.ActiveRoute;
            if (!TryGetPointBeforeRunway(route, out var point, out var index))
            {
                return;
            }

            var targetAlt = ResolvePointAltitude(point);
            Fmc.Des.WptAltFix = targetAlt > 0
                ? $"{point.Name}/{Calculator.FormatFmcAltitude((long)targetAlt)}"
                : point.Name;
            if (targetAlt <= 0 || Session.PlayerAircraft == null)
            {
                return;
            }

            var distNm = Vector2.Distance(
                route.GetCartesianPosition(index),
                Session.PlayerAircraft.NMPosition);
            if (distNm <= MinDescentDistanceNm)
            {
                Fmc.Des.VB = "0.00";
                Fmc.Des.VS = "0";
                return;
            }

            var deltaFeet = Calculator.CAltitude - targetAlt;
            var bearingDeg = Mathf.Atan((float)(deltaFeet / (distNm * FeetPerNm))) * Mathf.Rad2Deg;
            Fmc.Des.VB = bearingDeg.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

            if (Calculator.GS <= 1f)
            {
                Fmc.Des.VS = "0";
                return;
            }

            var minutes = distNm / Calculator.GS * 60.0;
            var descentFpm = deltaFeet / minutes;
            Fmc.Des.VS = ((int)System.Math.Round(-descentFpm)).ToString();
        }

        static string FormatActualFpa()
        {
            if (Calculator.GS <= 1f)
            {
                return "0.00";
            }

            var horizontalFpm = Calculator.GS * (FeetPerNm / 60f);
            var fpaDeg = Mathf.Atan(Calculator.CVS / horizontalFpm) * Mathf.Rad2Deg;
            return fpaDeg.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static bool TryGetPointBeforeRunway(RouteScriptableObject route, out RoutePoint point, out int index)
        {
            point = null;
            index = -1;
            var points = route?.Points;
            if (points == null || points.Length < 2)
            {
                return false;
            }

            var runway = points.Length - 1;
            for (var i = points.Length - 1; i >= 0; i--)
            {
                var name = points[i]?.Name;
                if (!string.IsNullOrEmpty(name)
                    && name.StartsWith("RW", System.StringComparison.OrdinalIgnoreCase))
                {
                    runway = i;
                    break;
                }
            }

            for (var i = runway - 1; i >= 0; i--)
            {
                var candidate = points[i];
                if (candidate == null || candidate.IsHiddenLine || candidate.IsPositionNode)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(candidate.Name))
                {
                    continue;
                }

                point = candidate;
                index = i;
                return true;
            }

            return false;
        }

        private static double ResolvePointAltitude(RoutePoint point)
        {
            if (point == null)
            {
                return -1;
            }

            var alt = point.Altitude;
            if (alt.ComputedValue > 0)
            {
                return alt.ComputedValue;
            }

            if (alt.RestrictionExact > 0)
            {
                return alt.RestrictionExact;
            }

            if (alt.RestrictionBelow > 0)
            {
                return alt.RestrictionBelow;
            }

            if (alt.RestrictionAbove > 0)
            {
                return alt.RestrictionAbove;
            }

            if (!string.IsNullOrEmpty(point.RawAltitude)
                && float.TryParse(point.RawAltitude, out var raw)
                && raw > 0)
            {
                return raw;
            }

            return -1;
        }

    }

    public class FMC
    {
        public INITREF Initref = new INITREF();
        public RTE Rte = new RTE();
        public DES Des = new DES();
        public CRZ Crz = new CRZ();
        public ARR Arr = new ARR();
        public PROG Prog = new PROG();
    }

    public class INITREF
    {
        public string GWT, Destination, RW, Field, Freq, F15, F30, F40, Vref;
        public int Course;
        public float GlideSlope;
    }

    public class RTE
    {
        public string Destination, RW, Origin;
    }

    public class DES
    {
        public string RWAltitude, Destination, WptAltFix, FPA, VB, VS, EconSpeed;
    }

    public class CRZ
    {
        public string Destination, FuelAtDestination, ActualWind, Altitude, Speed;
    }

    public class ARR
    {
        public string Destination, STAR, Transition, RW;
    }

    public class PROG
    {
        public string PrvName, PrvCrossAltitude, PrvActualTime, PrvActualFuel;
        public string NxtName, NxtDTG, NxtETA, NxtFUEL;
        public string SecondName, SecondDTG, SecondETA, SecondFUEL;
        public string Destination, DestDTG, DestETA, DestFUEL;
        public string ActualWind, FuelQty;
    }
}