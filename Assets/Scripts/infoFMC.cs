using System.Collections;
using System.Collections.Generic;
using Gamelogic.Extensions;
using UnityEngine;
using UnityEngine.UI;


public class infoFMC : Singleton<infoFMC>
{
    public LevelInfoScriptableObject[] levelsInfoData;

    public Text Infotext,pages;
    int previousPrvIndex = 0;

    public class INITREF { public string GWT, Destination, RW, Field, FreqCourse,F15,F30,F40,Vref; }
    public class RTE { public string Destination, RW; }
    public class DES { public string RWAltitude, Destination,WptAltFix,FPA,VB,VS; }
    public class CRZ { public string Destination,FuelAtDestination, ActualWind; }
    public class ARR { public string Destination,STAR, Transition, RW; }
    public class PROG 
    {
        public string PrvName, PrvCrossAltitude,PrvActualTime,PrvActualFuel;
        public string NxtName, NxtDTG, NxtETA, NxtFUEL;
        public string SecondName, SecondDTG, SecondETA, SecondFUEL;
        public string Destination, DestDTG, DestETA, DestFUEL;
        public string ActualWind, FuelQty;
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
    private void Start()
    {
        InvokeRepeating("DisplayFields", 1f, 1f) ;
    }

    public FMC FMCFields()
    {
        FMC Fmc = new FMC();
        double PrvAltitude,Altitude;
        int   Speed,VS,ff;
        double Distance;
       
        RouteScriptableObject activePoints = GameManager.Instance.ActiveRoute;
       
  
        PrvAltitude = Calculator.CAltitude;
         
        Calculator.WindElements WE;
        int prvWptIdx = PositionVirtualNode.PassedNodeIndex;
        int WPTCount = activePoints.Points.Length ;
        double[] fr_onpoint = new double[WPTCount];
        int[] GS_onpoint = new int[WPTCount];
        double[] totalDistLeft = new double[WPTCount];
        totalDistLeft[prvWptIdx] = GameManager.Instance.Aircraft.ComputedDistanceLeft;;
        double RW_Alt = activePoints.Points[WPTCount-1].Altitude.ComputedValue;
        double fuelBurn,fuelRemaining = Calculator.totalFuel / 100;  
        float DirectDistance = (Vector2.Distance(GameManager.Instance.PathLines.GetNodePosition(WPTCount - 1), GameManager.Instance.Aircraft.Position));

        for (int i = prvWptIdx + 1; i < activePoints.Points.Length; i++)
        {
            Altitude = (int)activePoints.Points[i].Altitude.ComputedValue;
            Speed =  activePoints.Points[i].Speed.ComputedValue;
            if (Speed < 0) Speed = 230; // Change Computed -1 speed
    
            Distance = System.Math.Round(activePoints.Points[i].Distance,2);
            WE = Calculator.CalculateWindElements(Altitude, Speed, 0);
            VS = (int)((Altitude- PrvAltitude) /(Distance/WE.GS*60));
            ff = (Altitude-RW_Alt) >3000 ? Calculator.FuelFlowFor((PrvAltitude-Altitude)/2, VS, Speed): 270;
            if (ff < 95) ff = 95; 
            fuelBurn = System.Math.Round((Distance / WE.GS * ff*2 / 100),3);
            fuelRemaining -= fuelBurn;
            
            //Infotext.text +=  activePoints.Points[i].Name;+ " D:" + Distance + " S:" + WE.GS + " A:" + Altitude + " V:" + VS + "   ff:" + ff + "   fb:" + fb + "   fr:" + System.Math.Round(fr,2) + "\n";
          
            PrvAltitude = Altitude;
            fr_onpoint[i] = fuelRemaining;
            GS_onpoint[i] = WE.GS;
            totalDistLeft[i] = totalDistLeft[i - 1] + Distance;
        }

     
        // Fmc.Initref.GWT = "[ZFW]+Calculator.TotalFuel";
        Fmc.Initref.GWT = levelsInfoData[0].ZFW.ToString();
        Fmc.Initref.Destination = "[destination]";
        Fmc.Initref.RW = "[rw]";
        Fmc.Initref.Field = "[field]";
        Fmc.Initref.FreqCourse = "[ils/crs]";
        Fmc.Initref.F15 = "F15";
        Fmc.Initref.F30 = "F30";
        Fmc.Initref.F40 = "F40";
        Fmc.Initref.Vref = "Vref";
       
        
        Fmc.Rte.Destination = "[destination]";
        Fmc.Rte.RW = "[rw]";
        Fmc.Des.RWAltitude = "" + RW_Alt;
        Fmc.Des.WptAltFix = "[WPT/ALT]";
        Fmc.Des.FPA = "" + System.Math.Round(Mathf.Atan((float)(-Calculator.CVS / (Calculator.GS / 60 * 6076))) * Mathf.Rad2Deg, 2);
        Fmc.Des.VB = "" + System.Math.Round(Mathf.Atan((float)(Calculator.CAltitude - RW_Alt) / (DirectDistance * 6076)) * Mathf.Rad2Deg, 2);
        Fmc.Des.VS = "" + (int)((Calculator.CAltitude - RW_Alt) / (DirectDistance / Calculator.GS * 60));
        
        
        Fmc.Crz.Destination = "[DEST]";
        Fmc.Crz.FuelAtDestination = "" + System.Math.Round(fr_onpoint[WPTCount - 1], 2);
        Fmc.Crz.ActualWind = "" + Calculator.CWind;
    
        
        Fmc.Arr.Destination = "[destination]";
        Fmc.Arr.STAR = "[rw]";
        Fmc.Arr.Transition = "[star]";
        Fmc.Arr.RW = "[trans]";
     
        
        Fmc.Prog.PrvName = "" + activePoints.Points[prvWptIdx].Name; ;
        
        if (prvWptIdx != previousPrvIndex)   // Catch the actual info while passing the point
        {
            Fmc.Prog.PrvCrossAltitude = "" + Calculator.CAltitude;
            Fmc.Prog.PrvActualTime = "" + GameTime.timerFMC;
            Fmc.Prog.PrvActualFuel = "" + System.Math.Round(Calculator.totalFuel / 100, 1);
        }
        previousPrvIndex = prvWptIdx;

        Fmc.Prog.NxtName = "" + activePoints.Points[prvWptIdx + 1].Name;
        Fmc.Prog.NxtDTG = "" + (int)totalDistLeft[prvWptIdx + 1];
        Fmc.Prog.NxtETA = "" + GameTime.FormatFMCTime(GameTime.timer + (((float)totalDistLeft[prvWptIdx + 1] / GS_onpoint[prvWptIdx + 1]) * 3600));
        Fmc.Prog.NxtFUEL = "" + System.Math.Round(fr_onpoint[prvWptIdx + 1], 1);
        Fmc.Prog.SecondName = "" + activePoints.Points[prvWptIdx + 2].Name; ;
        Fmc.Prog.SecondDTG = "" + (int)totalDistLeft[prvWptIdx + 2];
        Fmc.Prog.SecondETA = "" + GameTime.FormatFMCTime(GameTime.timer + (((float)totalDistLeft[prvWptIdx + 2] / GS_onpoint[prvWptIdx + 2]) * 3600));
        Fmc.Prog.SecondFUEL = "" + System.Math.Round(fr_onpoint[prvWptIdx + 2], 1);
        Fmc.Prog.Destination = "[Destination]";
        Fmc.Prog.DestDTG = "" + (int)totalDistLeft[WPTCount - 1];
        Fmc.Prog.DestETA = "" + GameTime.FormatFMCTime(GameTime.timer + (((float)totalDistLeft[WPTCount - 1] / GS_onpoint[WPTCount - 1]) * 3600));
        Fmc.Prog.DestFUEL = "" + System.Math.Round(fr_onpoint[WPTCount - 1], 1);
        Fmc.Prog.ActualWind = "" + Calculator.CWind;
        Fmc.Prog.FuelQty = "" + System.Math.Round(Calculator.totalFuel / 100, 1);

        return Fmc;
    }
    void DisplayFields()
    {
        FMC Fmc = FMCFields();
        pages.text = "INIT REF : " + Fmc.Initref.GWT +
                                  Fmc.Initref.Destination +
                                  Fmc.Initref.RW +
                                  Fmc.Initref.Field +
                                  Fmc.Initref.FreqCourse +
                                  Fmc.Initref.F15 +
                                  Fmc.Initref.F30 +
                                  Fmc.Initref.F40 +
                                  Fmc.Initref.Vref + "\n \n" +
                 "RTE       :  " + Fmc.Rte.Destination +
                                  Fmc.Rte.RW + "\n \n" +
                 "DES      : --Descent Speed Mode-- " +
                               " RW alt :" + Fmc.Des.RWAltitude +
                               "--TGT SPEED--" +
                                Fmc.Des.WptAltFix +
                               " FPA   : " + Fmc.Des.FPA +
                               "  VB   : " + Fmc.Des.VB +
                               "  VS   : " + Fmc.Des.VS + "\n \n" +
                "CRZ      : --CRZ ALT-- " +
                                Fmc.Crz.Destination +
                                " --CRZ SPD-- " +
                                "FuelATDESt   : " + Fmc.Crz.FuelAtDestination +
                               "Actual Wind  :" + Fmc.Crz.ActualWind + "\n \n" +
                "ARR      :    " + Fmc.Arr.Destination +
                                  Fmc.Arr.STAR +
                                  Fmc.Arr.Transition +
                                  Fmc.Arr.RW + "\n \n" +

               "PROG      :    " + Fmc.Prog.PrvName + ": X Alt:" + Fmc.Prog.PrvCrossAltitude +
                                     "  Actual Time:" + Fmc.Prog.PrvActualTime +
                                     "  Actual Fuel:" + Fmc.Prog.PrvActualFuel + "\n" +
                  Fmc.Prog.NxtName + ": DTG : " + Fmc.Prog.NxtDTG +
                                     "  ETA : " + Fmc.Prog.NxtETA +
                                     "  FUEL: " + Fmc.Prog.NxtFUEL + "\n" +
               Fmc.Prog.SecondName + ": DTG : " + Fmc.Prog.SecondDTG +
                                     "  ETA : " + Fmc.Prog.SecondETA +
                                     "  FUEL: " + Fmc.Prog.SecondFUEL + "\n" +
              Fmc.Prog.Destination + ": DTG : " + Fmc.Prog.DestDTG +
                                     "  ETA : " + Fmc.Prog.DestETA +
                                     "  FUEL: " + Fmc.Prog.DestFUEL + "\n" +
                                     "     wind     : " + Fmc.Prog.ActualWind +
                                     "     fuel qty : " + Fmc.Prog.FuelQty + "\n";
    }
}
