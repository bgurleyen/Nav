
using Gamelogic.Extensions;
using UnityEngine;
using UnityEngine.UI;

namespace Navigation.Data
{

    public class infoFMC : Singleton<infoFMC>
    {
        public Text Infotext, pages;
        private int previousPrvIndex = -1;
        private int Level = Calculator.Level;

        public FMC Fmc = new FMC();

        

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
             DisplayFields();
             InvokeRepeating(nameof(DisplayFields), 1f, 1f) ;

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

            Fmc.Crz.Destination = levelData.Destination;
            Fmc.Crz.Altitude = Calculator.FormatFmcCruiseAltitude(levelData.CrzAltitude);
            Fmc.Crz.Speed = Calculator.FormatFmcSpeedDisplay(
                levelData.CrzSpeed, levelData.CrzAltitude, machDigits: 3);
            Fmc.Crz.ActualWind = "" + Calculator.CWind;

            Fmc.Arr.Destination = levelData.Destination;
            Fmc.Arr.STAR = levelData.Star;
            Fmc.Arr.Transition = levelData.Transition;
            Fmc.Arr.RW = levelData.Runway;

            ApplyDesGateFields(activePoints, prvWptIdx, totalDistLeft);
            ApplyDesVerticalBearing(activePoints, RW_Alt);

            if (prvWptIdx < 0 || prvWptIdx + 1 >= activePoints.Points.Length)
            {
                return;
            }

            for (int i = prvWptIdx + 1; i < activePoints.Points.Length; i++)
            {
                Altitude = (int)activePoints.Points[i].Altitude.ComputedValue;
                Speed = activePoints.Points[i].Speed.ComputedValue;
                if (Speed < 0) Speed = 230; // Change Computed -1 speed

                Distance = System.Math.Round(activePoints.Points[i].Distance, 2);
                WE = Calculator.CalculateWindElements(Altitude, Speed, 0);
                VS = (int)((Altitude - PrvAltitude) / (Distance / WE.GS * 60));
                ff = (Altitude - RW_Alt) > 3000 ? Calculator.FuelFlowFor((PrvAltitude - Altitude) / 2, VS, Speed) : 270;
                if (ff < 95) ff = 95;
                fuelBurn = System.Math.Round((Distance / WE.GS * ff * 2 / 100), 3);
                fuelRemaining -= fuelBurn;

                totalDistLeft[i] = (i == prvWptIdx + 1)
                    ? Session.PlayerAircraft.ComputedDistanceLeftOnSegment
                    : totalDistLeft[i - 1] + Distance;
                PrvAltitude = Altitude;
                fr_onpoint[i] = fuelRemaining;
                GS_onpoint[i] = WE.GS;

            }

            ApplyDesGateFields(activePoints, prvWptIdx, totalDistLeft);
            ApplyDesVerticalBearing(activePoints, RW_Alt);

            Fmc.Crz.FuelAtDestination = "" + System.Math.Round(fr_onpoint[WPTCount - 1], 2);

              Fmc.Prog.PrvName = "" + activePoints.Points[prvWptIdx].Name; 
       


            if (prvWptIdx != previousPrvIndex) // Catch the actual info while passing the point
            {
                Fmc.Prog.PrvCrossAltitude = Calculator.FormatFmcAltitude((long)Calculator.CAltitude);
                Fmc.Prog.PrvActualTime = "" + GameTime.timerFMC;
                Fmc.Prog.PrvActualFuel = "" + System.Math.Round(Calculator.totalFuel / 100, 1);


            }

            previousPrvIndex = prvWptIdx;

            Fmc.Prog.NxtName = "" + activePoints.Points[prvWptIdx + 1].Name;
            Fmc.Prog.NxtDTG = "" + (int)totalDistLeft[prvWptIdx + 1];
            Fmc.Prog.NxtETA = "" + GameTime.FormatFMCTime(GameTime.timer +
                                                          (((float)totalDistLeft[prvWptIdx + 1] /
                                                            GS_onpoint[prvWptIdx + 1]) * 3600));
            Fmc.Prog.NxtFUEL = "" + System.Math.Round(fr_onpoint[prvWptIdx + 1], 1);
            Fmc.Prog.SecondName = prvWptIdx + 2 < activePoints.Points.Length
                ? "" + activePoints.Points[prvWptIdx + 2].Name
                : "";
            Fmc.Prog.SecondDTG =
                prvWptIdx + 2 < activePoints.Points.Length ? "" + (int)totalDistLeft[prvWptIdx + 2] : "";
            Fmc.Prog.SecondETA = prvWptIdx + 2 < activePoints.Points.Length
                ? "" + GameTime.FormatFMCTime(GameTime.timer +
                                              (((float)totalDistLeft[prvWptIdx + 2] / GS_onpoint[prvWptIdx + 2]) *
                                               3600))
                : "";
            Fmc.Prog.SecondFUEL = prvWptIdx + 2 < activePoints.Points.Length
                ? "" + System.Math.Round(fr_onpoint[prvWptIdx + 2], 1)
                : "";
            Fmc.Prog.Destination = levelData.Destination;
            Fmc.Prog.DestDTG = "" + (int)totalDistLeft[WPTCount - 1];
            Fmc.Prog.DestETA = "" + GameTime.FormatFMCTime(GameTime.timer +
                                                           (((float)totalDistLeft[WPTCount - 1] /
                                                             GS_onpoint[WPTCount - 1]) * 3600));
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

        private void ApplyDesGateFields(RouteScriptableObject route, int prvWptIdx, double[] totalDistLeft)
        {
            if (!route.TryGetGatePoint(out var gate, out var gateIdx))
            {
                Fmc.Des.WptAltFix = "";
                Fmc.Des.FPA = FormatFpaFromVerticalSpeed(Calculator.CVS, Calculator.GS);
                Fmc.Des.VS = "";
                return;
            }

            var gateAlt = ResolvePointAltitude(gate);
            Fmc.Des.WptAltFix = $"{gate.Name}/{Calculator.FormatFmcAltitude((long)gateAlt)}";

            double distToGateNm;
            if (gateIdx > prvWptIdx && totalDistLeft != null && gateIdx < totalDistLeft.Length)
            {
                distToGateNm = totalDistLeft[gateIdx];
            }
            else
            {
                distToGateNm = Vector2.Distance(
                    route.GetCartesianPosition(gateIdx),
                    Session.PlayerAircraft.NMPosition);
            }

            if (distToGateNm <= 0.01 || Calculator.GS <= 0)
            {
                Fmc.Des.FPA = "0";
                Fmc.Des.VS = "0";
                return;
            }

            var requiredVs = (Calculator.CAltitude - gateAlt) / (distToGateNm / Calculator.GS * 60);
            Fmc.Des.FPA = FormatFpaFromVerticalSpeed(-requiredVs, Calculator.GS);
            Fmc.Des.VS = "" + (int)requiredVs;
        }

        private void ApplyDesVerticalBearing(RouteScriptableObject route, double rwAlt)
        {
            if (route?.Points == null || route.Points.Length == 0 || Session.PlayerAircraft == null)
            {
                Fmc.Des.VB = "";
                return;
            }

            var distNm = Vector2.Distance(
                route.GetCartesianPosition(route.Points.Length - 1),
                Session.PlayerAircraft.NMPosition);
            if (distNm <= 0.01)
            {
                Fmc.Des.VB = "0";
                return;
            }

            Fmc.Des.VB = "" +
                         System.Math.Round(
                             Mathf.Atan((float)((Calculator.CAltitude - rwAlt) / (distNm * 6076))) *
                             Mathf.Rad2Deg, 2);
        }

        private static string FormatFpaFromVerticalSpeed(double vsFeetPerMin, float gsKnots)
        {
            if (gsKnots <= 0.01f)
            {
                return "0";
            }

            var deg = Mathf.Atan((float)(-vsFeetPerMin / (gsKnots / 60f * 6076f))) * Mathf.Rad2Deg;
            return "" + System.Math.Round(deg, 2);
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

        private void DisplayFields()
        {
            ComputeFMCFields();
            pages.text = "INIT REF : " + Fmc.Initref.GWT + "\n" +
                         Fmc.Initref.Destination + "\n" +
                         Fmc.Initref.RW + "\n" +
                         Fmc.Initref.Field + "\n" +
                         Fmc.Initref.Freq + "\n" +
                         Fmc.Initref.Course + "\n" +
                         Fmc.Initref.F15 + "\n" +
                         Fmc.Initref.F30 + "\n" +
                         Fmc.Initref.F40 + "\n" +
                         Fmc.Initref.Vref + "\n \n" +
                         "RTE       :  " + Fmc.Rte.Destination + "\n" +
                         Fmc.Rte.RW + "\n \n" +
                         "DES      : --Descent Speed Mode-- " + "\n" +
                         " RW alt :" + Fmc.Des.RWAltitude + "\n" +
                         "--TGT SPEED--" + "\n" +
                         Fmc.Des.EconSpeed + "\n" +
                         Fmc.Des.WptAltFix + "\n" +
                         " FPA   : " + Fmc.Des.FPA + "\n" +
                         "  VB   : " + Fmc.Des.VB + "\n" +
                         "  VS   : " + Fmc.Des.VS + "\n \n" +
                         "CRZ      : --CRZ ALT-- " + "\n" +
                         Fmc.Crz.Destination + "\n" +
                         " --CRZ SPD-- " + "\n" +
                         Fmc.Crz.Speed + "\n" +
                         "FuelATDESt   : " + Fmc.Crz.FuelAtDestination + "\n" +
                         "Actual Wind  :" + Fmc.Crz.ActualWind + "\n \n" +
                         "ARR      :    " + Fmc.Arr.Destination + "\n" +
                         Fmc.Arr.STAR + "\n" +
                         Fmc.Arr.Transition + "\n" +
                         Fmc.Arr.RW + "\n \n" +

                         "PROG      :    " + Fmc.Prog.PrvName + ": X Alt:" + Fmc.Prog.PrvCrossAltitude + "   " +
                         "  Actual Time:" + Fmc.Prog.PrvActualTime + "   " +
                         "  Actual Fuel:" + Fmc.Prog.PrvActualFuel + "\n" +
                         Fmc.Prog.NxtName + ": DTG : " + Fmc.Prog.NxtDTG + "   " +
                         "  ETA : " + Fmc.Prog.NxtETA + "   " +
                         "  FUEL: " + Fmc.Prog.NxtFUEL + "\n" +
                         Fmc.Prog.SecondName + ": DTG : " + Fmc.Prog.SecondDTG + "   " +
                         "  ETA : " + Fmc.Prog.SecondETA + "   " +
                         "  FUEL: " + Fmc.Prog.SecondFUEL + "\n" +
                         Fmc.Prog.Destination + ": DTG : " + Fmc.Prog.DestDTG + "   " +
                         "  ETA : " + Fmc.Prog.DestETA + "   " +
                         "  FUEL: " + Fmc.Prog.DestFUEL + "\n" +
                         "     wind     : " + Fmc.Prog.ActualWind + "   " +
                         "     fuel qty : " + Fmc.Prog.FuelQty + "\n";
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