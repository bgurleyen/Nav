using Navigation;
using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;


public class LevelStartInformation : MonoBehaviour
{
    public Text msgText;
    public Text msgText1;
    public Image LevelStartInfo;
    public Action<MapMode> OnMapModeSet;
    void Start()
    {
       
    }

    public void ShowInfo()
    {
     //   LevelStartInfo.transform.position = new Vector3(LevelStartInfo.transform.position.x,
     //                                              LevelStartInfo.transform.position.y,
     //                                              -200);
        LevelStartInfo.enabled = true;
        msgText.text = BuildMessage();
        msgText1.text = BuildMessage1();

    }

    private static string BuildMessage()
    {
     
        var builder = new StringBuilder();
        builder.AppendLine($"Level {Session.CurrentLevel.levelInfo.LevelNumber}");
        AppendIfNotEmpty(builder, "Destination", Session.CurrentLevel.levelInfo.Destination);
        AppendIfNotEmpty(builder, "STAR", Session.CurrentLevel.levelInfo.Star);
        AppendIfNotEmpty(builder, "Transition", Session.CurrentLevel.levelInfo.Transition);
        AppendIfNotEmpty(builder, "Runway", Session.CurrentLevel.levelInfo.Runway);
        
        return builder.ToString();
    }
    private static string BuildMessage1()
    {

        var builder = new StringBuilder();

        builder.AppendLine($"Course: {Session.CurrentLevel.levelInfo.Course}");
        builder.AppendLine($"Cruise: FL{Session.CurrentLevel.levelInfo.CrzAltitude / 100} / {Session.CurrentLevel.levelInfo.CrzSpeed} KT");
        builder.AppendLine($"ZFW/Fuel: {Session.CurrentLevel.levelInfo.ZFW:0.#} / {Session.CurrentLevel.levelInfo.Fuel:0.#}");
        builder.AppendLine();
        builder.Append("Validate FMC, call when Ready for Descent");

        return builder.ToString();
    }
    private static void AppendIfNotEmpty(StringBuilder builder, string label, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            builder.AppendLine($"{label}: {value}");
        }
    }
    public  void ButtonClick()
    {

        LevelStartInfo.transform.position =  new Vector3( LevelStartInfo.transform.position.x,
                                                          LevelStartInfo.transform.position.y,
                                                          200);
    }

}
