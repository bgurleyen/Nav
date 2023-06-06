using Navigation;
using UnityEngine;

[CreateAssetMenu]
public class LevelDataScriptableObject : ScriptableObject
{
    public RouteScriptableObject MainRoute;
    [Space]
    [SerializeField] private VirtualPointsScriptableObject virtualPoints;
    [SerializeField] private ATCInstructionsScriptableObject aTCs;
    public LevelInfoScriptableObject levelInfo;
    public OtherACScriptableObject[] otherACs;
    public WindTableScriptableObject WindTable;

    public VirtualPoints[] VirtualPoints => virtualPoints.VirtualPointsItems;
    public ATCInstructionInfo[] ATCs => aTCs.ATCInstrucitonItems;
}