using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PFD_Animation : MonoBehaviour
{

    public Image speedTape;
    public Image AltTape;
    public Image AttPic;
    public Image Att;
    public Image TopIndex;
    public Image maxFspeed;
    public Image SpeedIdx;
    public Image AltIdx;
    public Text upSpeed, Vref;
    public Text Fspeed;
    public GameObject AltBasket;


    int upSpeedVal = 205;// Upspeed = Vref40+70
    int RSpeed = 220;
    double CSpeed = 250;
    float AttInc;
    int maxFlapSpeedPos;
    public static int LimitSpeed;

    public void Start()
    {
        upSpeed.transform.localPosition = new Vector2(92, upSpeedVal * 2 - 445);
        Vref.transform.localPosition = new Vector2(92, (upSpeedVal - 70) * 2 - 443);
        SyncVariables();
        AltUpdate(10000);//Init at 10k to update to initial Alt.
    }

    public void SyncVariables()
    {
        RSpeed = (int)Calculator.RSpeed;
        CSpeed = Calculator.CSpeed;
    }
    public void Flaps_Indexchange(int idx)
    {

        int[] F = new int[9] { 0, -20, -20, -40, -40, -50, 0, 0, 0 };
        string[] FDisplay = new string[9] { "", " 1", " 2", " 5", "10", "15", "", "", "" };
        int[] max = new int[9] { 429, 250, 250, 250, 172, 145, 95, 85, 65 };

        if (F[idx] != 0)
        {

            Fspeed.transform.localPosition = new Vector2(90, (upSpeedVal + F[idx]) * 2 - 441);
            Fspeed.GetComponent<UnityEngine.UI.Text>().text = "─" + FDisplay[idx];
        }
        else Fspeed.GetComponent<UnityEngine.UI.Text>().text = "";
        maxFlapSpeedPos = max[idx]; // red limit band
    }
    public void SpeedTapeUpdate()
    {
        RSpeed = Calculator.RSpeed;
        CSpeed = Calculator.CSpeed;
        int MachLimSpeed = (int)Calculator.Mach2Speed(0.82);
        speedTape.transform.localPosition = new Vector2(-150, (float)-CSpeed * 2 + 440);
        CheckSpeedIndicator();
        int LGLim = Calculator.LGDown ? 291 : 429;
        int MachLim = 243 + (MachLimSpeed - 245) * 2;

        int RedLimitBand = Mathf.Min(maxFlapSpeedPos, LGLim, MachLim);
        LimitSpeed = 220 + (int)((RedLimitBand - 190) / 2);
        maxFspeed.transform.localPosition = new Vector2(26, RedLimitBand);
        Calculator.Check_LimitSpeed();
    }
    public void SpeedIndexUpdate_Click()
    {
        RSpeed = Calculator.RSpeed;
        CSpeed = (int)Calculator.CSpeed;

        if ((RSpeed < CSpeed + 52) && (RSpeed > CSpeed - 52))
        {
            SpeedIdx.transform.localPosition = new Vector2(27, 2 * (RSpeed - 220));
        }
        else CheckSpeedIndicator();
    }
    public void CheckSpeedIndicator()
    {
        if (RSpeed >= CSpeed + 52) SpeedIdx.transform.localPosition = new Vector2(27, (2 * (((float)CSpeed + 52) - 220)));
        if (RSpeed <= CSpeed - 52) SpeedIdx.transform.localPosition = new Vector2(27, (2 * (((float)CSpeed - 52) - 220)));

    }
    public void AltUpdate(int CAltitude)

    {
        int R, Pos;
        R = CAltitude % 1000;
        Pos = 600 - R;
        if (Pos < 0)
        {
            Pos += 1000;
            AltBandChange((CAltitude / 1000) + 2);
        }
        else AltBandChange((CAltitude / 1000) + 1);

        Pos = (int)(Pos * 220 / 1000);
        AltTape.transform.localPosition = new Vector2(146, (Pos));
    }
    public void AltBandChange(int A)
    {
        int i;
        for (i = 1; i < 11; i++)
        {
            if (i == 2) A -= 1;
            if (i == 7) A -= 1;

            AltBasket = GameObject.Find("AltTxt (" + i + ")");
            AltBasket.GetComponent<UnityEngine.UI.Text>().text = A.ToString();
        }
    } //Locate the numbers on ALtitude Tape
    public void CheckAltitudeIndicator(int RAltitude, int CAltitude)
    {
        if (Mathf.Abs(RAltitude - CAltitude) < 550)
        {
            AltIdx.transform.localPosition = new Vector2(122, (int)((RAltitude - CAltitude) * 0.22) - 1);
        }
    } //Locate the Altitude Selection index
    public void AttRight_Click()
    {
        Att.transform.Rotate(0, 0, 1);
        TopIndex.transform.Rotate(0, 0, 1);


    }
    public void AttLeft_Click()
    {
        Att.transform.Rotate(0, 0, -1);
        TopIndex.transform.Rotate(0, 0, -1);
    }
    public void AttUpdate(int Att)
    {
        Att = Att / 5 * 5;
        AttInc = AttPic.transform.localPosition.y * -20;
        if (Att > AttInc) AttInc += 20f;
        if (Att < AttInc) AttInc -= 20f;
        if (Att != AttInc) AttPic.transform.localPosition = new Vector2(0, (AttInc / -20));
    }
}

