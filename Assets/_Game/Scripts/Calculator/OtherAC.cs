using System.Collections.Generic;
using Navigation;
using UnityEngine;

public class OtherAC
{
    private ACInfo[] _acItems;

    private float dx, dy, hyp;
    private float x, y;
    private float Speed, SpeedCo;
    private string s;
    private Vector2 PtPos;
    private int PointIndex, AltitudeR;
    private float AltitudeC;
    private Vector2 finalPosition;
    private int HeadingW = 0;

    private string _aircraftKey;

    private int _acIndex;
    private int _currentPointIndex = 1;

    private GameObject _ac;

    public OtherAC(int acIndex, LevelDataScriptableObject levelData)
    {
        _acIndex = acIndex;
        _acItems = levelData.otherACs[acIndex].ACItems;

        AltitudeC = _acItems[0].Altitude;

        _aircraftKey = "AC (" + (acIndex + 1) + ")";

        finalPosition = Move.Instance.PointPos(_acItems[0].Point); //intial pos and alt
        
        _ac = GameObject.Find(_aircraftKey);
            if (_ac == null)
            {
                Debug.LogWarning($"Not found aircraftKey: {_aircraftKey}");
            }
    }

    public void Tick(Dictionary<string, string> acTexts, Dictionary<string, Vector2> acPositions)
    {
        if (_ac == null)
        {
            return;
        }
        //Moves  other ACs based on the Route and Alt(Altitude) arrays.

        if (_currentPointIndex < _acItems.Length)
        {

            PointIndex = _acItems[_currentPointIndex].Point;
            AltitudeR = _acItems[_currentPointIndex].Altitude;
            Speed = _acItems[_currentPointIndex].Speed;

            PtPos = Move.Instance.PointPos(PointIndex);

            Calculator.WindElements WE = Calculator.CalculateWindElements(AltitudeR, Speed, HeadingW);

            float GS = WE.GS; // Convert the speed to Ground Speed

            // if (ACnr  == 3 ) Debug.Log("   A: " + AltitudeR +"    S: " + Speed + "    GS: " + GS + "  H:" + HeadingW);
            //if (ACnr == 3) Debug.Log("   F: " + finalPosition + "    pt: " + PtPos + "  Aci: " + HeadingW);

            if (!acPositions.ContainsKey(_aircraftKey))
            {
                acPositions.Add(_aircraftKey, Vector2.zero);
                acTexts.Add(_aircraftKey, "");
            }



            SpeedCo = Session.Settings.AircraftTickDistance(GS);

            x = finalPosition.x;
            y = finalPosition.y;

            dx = PtPos.x - x;
            dy = PtPos.y - y;

            HeadingW = 90 - (int)(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);

            hyp = Mathf.Sqrt(dx * dx + dy * dy);


            AltitudeC -= ((AltitudeC - AltitudeR)) / hyp * SpeedCo;
            finalPosition = new Vector2(x + dx / hyp * SpeedCo, y + dy / hyp * SpeedCo); //Advance

            _ac.transform.localPosition = finalPosition;
            acPositions[_aircraftKey] = finalPosition;

            s = (AltitudeC - Calculator.CAltitude < 0) ? "-" : "+";
            if (Mathf.Abs(AltitudeC - (float)Calculator.CAltitude) < 1000) s += "0";
            if ((AltitudeC - (int)Calculator.CAltitude < 3000) && ((int)Calculator.CAltitude - AltitudeC < 6000))
            {
                s += (int)(Mathf.Abs(AltitudeC - (float)Calculator.CAltitude) / 100);
                _ac.GetComponent<UnityEngine.UI.Text>().text = "" + _acIndex;
            }
            else s = "";

            acTexts[_aircraftKey] = s;
            if ((hyp <= 1.5))
            {
                _currentPointIndex += 1;
                AltitudeC = AltitudeR;
            }
            //CollisionCheck(acTexts);
            
            
        }
        else
        {
            _ac.GetComponent<UnityEngine.UI.Text>().text = "";
        }

    }

    void CollisionCheck(Dictionary<string, string> acTexts)
    {

        float D = Vector2.Distance(Session.PlayerAircraft.NMPosition, finalPosition);

        int myACAlt = (int)Calculator.CAltitude;
        int ACAlt = (int)AltitudeC;

        if ((D < 2) && (Mathf.Abs(myACAlt - ACAlt) < 700))
        {
            _ac.GetComponent<UnityEngine.UI.Text>().color = Color.clear;
            acTexts[_aircraftKey] = "";

        }
    }
}