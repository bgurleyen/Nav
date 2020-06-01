using System;
using UnityEngine;


[Serializable]
public class FMCpagesData 
{

    public class LevelInfo
    {
        public int LevelNumber;
        public string Destination;
        public string Star;
        public string Transition;
        public string Runway;
        public string FieldInfo;
        public string FreqCourse;
        public float ZFW;
        public float Fuel;
        public long CrzAltitude;
        public int CrzSpeed;
        public int F30Speed;
        public int DesEconSpeed;
        public int DesEconMach;
        public int GateIdx;
    }
    public class WindTable
    {
        public long Altitude;
        public int Degrees;
        public int Knots;
    }

    public class ATCinstructions
    {
        public int point;
        public int mode;  //0 nochange, 1 DCT , 2 HDG , 3 ClrILS
        public long Altitude;
        public int VS;
        public int VS_nx;  //0 exact, 1 min , 2 max
        public int Speed;
        public int Speed_nx;
        
    }

    public class OtherAC
    {
        public class AC
    {
        public int Point;
        public int Altitude; // keep in integer limits
        public int Speed;
    }     
        
        public AC A1 = new AC();
        public AC A2 = new AC();
        public AC A3 = new AC();
        public AC A4 = new AC();
        public AC A5 = new AC();
        public AC A6 = new AC();
        public AC A7 = new AC();
        public AC A8 = new AC();
        public AC A9 = new AC();
    }
}
