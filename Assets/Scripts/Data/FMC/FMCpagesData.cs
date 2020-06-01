using System;
using UnityEngine;


[Serializable]
public class FMCpagesData
{


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
