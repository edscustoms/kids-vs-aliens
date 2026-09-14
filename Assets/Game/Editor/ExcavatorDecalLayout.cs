using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Editor-only source data. The player uses baked MeshFilters and two shared materials.</summary>
public sealed class ExcavatorDecalLayout : ScriptableObject
{
    [Tooltip("Positions use the upright model frame: +X bucket/front, +Y up, +Z left/cab side. Metres at the FBX's authored 1.7 import scale.")]
    public List<Label> labels = new List<Label>();

    [Serializable]
    public sealed class Label
    {
        public string path;
        public bool safetyAtlas;
        [Tooltip("Pixel rectangle in the supplied 1448 x 1086 atlas, measured from its TOP LEFT.")]
        public Rect atlasPixels;
        public Vector3 surfacePosition;
        public Vector3 outwardNormal = Vector3.forward;
        public Vector3 textUp = Vector3.up;
        [Min(0.01f)] public float width = 1;
        [Min(0)] public float surfaceOffset = 0.002f;
    }
}
