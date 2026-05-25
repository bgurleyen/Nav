using System.Collections.Generic;

[System.Serializable]
public class DDL_data
{
    public List<double> altitude = new List<double>();
    public List<double> speed = new List<double>();
    public List<double> flap = new List<double>();
    public List<double> speedBrake = new List<double>();
    public List<double> landingGear = new List<double>();
    public List<double> verticalMode = new List<double>();
    public List<double> fuelFlow = new List<double>();
    public List<double> distance = new List<double>();
    public double remainingFuel;
    public List<string> time = new List<string>();
}
