using System;
using UnityEngine;

[Serializable]
public struct PlasmaCoreConfig
{
    [Header("Core")]
    public Vector3 size;

    [Header("Arc Count")]
    public int minArcs;
    public int maxArcs;

    [Header("Arc Shape")]
    public int segments;
    public float arcLength;
    public float jitter;
    public float arcWidth;

    [Header("Arc Timing")]
    public float refreshRate;
}
