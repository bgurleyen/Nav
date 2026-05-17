using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Gamelogic.Extensions;
using Navigation;
using UnityEngine;
using UnityEngine.UI;

namespace Navigation.Data
{

    public class infoFMC : Singleton<infoFMC>
    {
        public Text Infotext, pages;
        private int previousPrvIndex = 0;
        private int Level = Calculator.Level;

        public FMC Fmc = new FMC();

        

        private void Start()
        {
             InvokeRepeating(nameof(DisplayFields), 1f, 1f) ;

        }

        private void ComputeFMCFields()
        {

            double PrvAltitude, Altitude;
            int Speed, VS, ff;
            double Distance;

            RouteScriptableObject activePoints = Session.ActiveRoute;


            PrvAltitude = Calculator.CAltitude;

            Calculator.WindElements WE;
            int prvWptIdx = PositionVirtualNode.PassedNodeIndex;
            int WPTCount = activePoints.Points.Length;
            double[] fr_onpoint = new double[WPTCount];
            int[] GS_onpoint = new int[WPTCount];
            double[] totalDistLeft = new double[WPTCount];
            double RW_Alt = activePoints.Points[WPTCount - 1].Altitude.ComputedValue;
            double fuelBurn, fuelRemaining = Calculator.totalFuel / 100;
            float DirectDistance = (Vector2.Distance(Session.ActiveRoute.GetCartesianPosition(WPTCount - 1),
                Session.PlayerAircraft.NMPosition));
            if (prvWptIdx < 0) return;
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

                //Infotext.text +=  activePoints.Points[i].Name;+ " D:" + Distance + " S:" + WE.GS + " A:" + Altitude + " V:" + VS + "   ff:" + ff + "   fb:" + fb + "   fr:" + System.Math.Round(fr,2) + "\n";

                totalDistLeft[i] = (i == prvWptIdx + 1)
                    ? Session.PlayerAircraft.ComputedDistanceLeftOnSegment
                    : totalDistLeft[i - 1] + Distance;
                PrvAltitude = Altitude;
                fr_onpoint[i] = fuelRemaining;
                GS_onpoint[i] = WE.GS;

            }

            var levelData = Session.CurrentLevel.levelInfo;

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
            Fmc.Des.RWAltitude = "" + RW_Alt; // Remove //
            //Fmc.Des.WptAltFix = activePoints.Points[levelsInfoData[Level].GateIdx].Name+ "/" + (int)activePoints.Points[levelsInfoData[Level].GateIdx].Altitude.ComputedValue;

            Fmc.Des.FPA = "" +
                          System.Math.Round(
                              Mathf.Atan((float)(-Calculator.CVS / (Calculator.GS / 60 * 6076))) * Mathf.Rad2Deg, 2);
            Fmc.Des.VB = "" +
                         System.Math.Round(
                             Mathf.Atan((float)(Calculator.CAltitude - RW_Alt) / (DirectDistance * 6076)) *
                             Mathf.Rad2Deg, 2);
            Fmc.Des.VS = "" + (int)((Calculator.CAltitude - RW_Alt) / (DirectDistance / Calculator.GS * 60));


            Fmc.Crz.Destination = levelData.Destination;
            Fmc.Crz.Altitude = ""+levelData.CrzAltitude;
            Fmc.Crz.Speed = ""+levelData.CrzSpeed;
            Fmc.Crz.Destination = levelData.Destination;
            Fmc.Crz.FuelAtDestination = "" + System.Math.Round(fr_onpoint[WPTCount - 1], 2);
            Fmc.Crz.ActualWind = "" + Calculator.CWind;


            Fmc.Arr.Destination = levelData.Destination;
            /*Fmc.Arr.STAR = levelData.Runway;
            Fmc.Arr.Transition = levelData.Star;
            Fmc.Arr.RW = levelData.Transition;*/
            Fmc.Arr.STAR = levelData.Star;
            Fmc.Arr.Transition = levelData.Transition;
            Fmc.Arr.RW = levelData.Runway;


            Fmc.Prog.PrvName = "" + activePoints.Points[prvWptIdx].Name;

            if (prvWptIdx != previousPrvIndex) // Catch the actual info while passing the point
            {
                Fmc.Prog.PrvCrossAltitude = "" + Calculator.CAltitude;
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
                         Fmc.Des.WptAltFix + "\n" +
                         " FPA   : " + Fmc.Des.FPA + "\n" +
                         "  VB   : " + Fmc.Des.VB + "\n" +
                         "  VS   : " + Fmc.Des.VS + "\n \n" +
                         "CRZ      : --CRZ ALT-- " + "\n" +
                         Fmc.Crz.Destination + "\n" +
                         " --CRZ SPD-- " + "\n" +
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
        public string Destination, RW;
    }

    public class DES
    {
        public string RWAltitude, Destination, WptAltFix, FPA, VB, VS;
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