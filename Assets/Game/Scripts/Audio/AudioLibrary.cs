using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Audio/Audio Library")]
public sealed class AudioLibrary : ScriptableObject
{
    [Tooltip("Runtime/build/preload manifest: add only events wired into the game. Unused candidates remain visible in the Editor Audio Library without inclusion here.")]
    public List<SoundEvent> events = new List<SoundEvent>();
}
