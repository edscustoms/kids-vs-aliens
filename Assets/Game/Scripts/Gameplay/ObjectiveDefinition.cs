using UnityEngine;

public enum ObjectiveProgressMode { None, Count }
public enum ObjectiveState { Inactive, Active, Completed }

[CreateAssetMenu(menuName = "Gameplay/Objective")]
public sealed class ObjectiveDefinition : ScriptableObject
{
    [Tooltip("Stable author-facing identifier. Asset references/GUIDs resolve saved content.")]
    public string id;
    public string title;
    public ObjectiveProgressMode progressMode;
    [Min(1)] public int targetCount = 1;
}
