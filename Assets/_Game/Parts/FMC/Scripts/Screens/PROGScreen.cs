using Navigation.Data;
using TMPro;
using UnityEngine;

public class PROGScreen : ScreenBase
{
    [SerializeField] private TMP_Text _fromNode;
    [SerializeField] private TMP_Text _nextNodeDegrees;
    [SerializeField] private TMP_Text _nextNode;
    [SerializeField] private TMP_Text _secondNodeDegrees;
    [SerializeField] private TMP_Text _secondNode;
    [SerializeField] private TMP_Text _destinationNode;

    [SerializeField] private TMP_Text _alt;
    [SerializeField] private TMP_Text _ata;
    [SerializeField] private TMP_Text _fuelFrom;

    [SerializeField] private TMP_Text _dtg;
    [SerializeField] private TMP_Text _etaNext;
    [SerializeField] private TMP_Text _fuelNext;

    [SerializeField] private TMP_Text _altSecond;
    [SerializeField] private TMP_Text _etaSecond;
    [SerializeField] private TMP_Text _fuelSecond;

    [SerializeField] private TMP_Text _altDestination;
    [SerializeField] private TMP_Text _etaDestination;
    [SerializeField] private TMP_Text _fuelDestination;

    [SerializeField] private TMP_Text _wind;
    [SerializeField] private TMP_Text _fuel;

    public override void Show()
    {
        base.Show();

        Main.UpdatePageInfo(
           isMod: false,
           //firstInfo: "-------",
           secondInfo: "--------------------",
           pageTitle: "PROGRESS",
           currentPage: 0, totalPages: 1);


        InvokeRepeating(nameof(Refresh), 0, 1f);
    }

    public override void Hide()
    {
        base.Hide();

        CancelInvoke(nameof(Refresh));
    }


    private void Refresh()
    {
        var progRef = infoFMC.Instance.Fmc.Prog;

        _fromNode.text = progRef.PrvName;
        _nextNode.text = progRef.NxtName;
        _secondNode.text = progRef.SecondName;
        _destinationNode.text = progRef.Destination;

        _alt.text = progRef.PrvCrossAltitude;
        _dtg.text = progRef.NxtDTG;
        _altSecond.text = progRef.SecondDTG;
        _altDestination.text = progRef.DestDTG;

        _ata.text = progRef.PrvActualTime;
        _etaNext.text = progRef.NxtETA;
        _etaSecond.text = progRef.SecondETA;
        _etaDestination.text = progRef.DestETA;

        _fuel.text = progRef.PrvActualFuel;
        _fuelNext.text = progRef.NxtFUEL;
        _fuelSecond.text = progRef.SecondFUEL;
        _fuelDestination.text = progRef.DestFUEL;

        _fuel.text = progRef.FuelQty;
        _wind.text = progRef.ActualWind;
    }
}
