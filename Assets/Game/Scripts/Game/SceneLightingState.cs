using System;
using UnityEngine;
using UnityEngine.Rendering;

// The two existing authored environments, not a second lighting controller or new shader.
[Serializable]
public sealed class SceneLightingState
{
    public Material sky;
    public AmbientMode ambientMode;
    public Color ambientSky, ambientEquator, ambientGround, fogColor, sunColor;
    public float ambientIntensity, reflectionIntensity, sunIntensity, fogStart, fogEnd, fogDensity;
    public bool fog;
    public FogMode fogMode;
    public Quaternion sunRotation;

    public static SceneLightingState Capture(Light sun) => new SceneLightingState {
        sky = RenderSettings.skybox, ambientMode = RenderSettings.ambientMode,
        ambientSky = RenderSettings.ambientSkyColor, ambientEquator = RenderSettings.ambientEquatorColor,
        ambientGround = RenderSettings.ambientGroundColor, ambientIntensity = RenderSettings.ambientIntensity,
        reflectionIntensity = RenderSettings.reflectionIntensity, fog = RenderSettings.fog,
        fogMode = RenderSettings.fogMode, fogColor = RenderSettings.fogColor,
        fogStart = RenderSettings.fogStartDistance, fogEnd = RenderSettings.fogEndDistance,
        fogDensity = RenderSettings.fogDensity, sunColor = sun.color, sunIntensity = sun.intensity,
        sunRotation = sun.transform.rotation
    };

    public void Apply()
    {
        RenderSettings.skybox = sky; RenderSettings.ambientMode = ambientMode;
        RenderSettings.ambientSkyColor = ambientSky; RenderSettings.ambientEquatorColor = ambientEquator;
        RenderSettings.ambientGroundColor = ambientGround; RenderSettings.ambientIntensity = ambientIntensity;
        RenderSettings.reflectionIntensity = reflectionIntensity; RenderSettings.fog = fog;
        RenderSettings.fogMode = fogMode; RenderSettings.fogColor = fogColor;
        RenderSettings.fogStartDistance = fogStart; RenderSettings.fogEndDistance = fogEnd;
        RenderSettings.fogDensity = fogDensity;
    }

    public static void Blend(SceneLightingState a, SceneLightingState b, float t, Material workingSky, Light sun)
    {
        workingSky.Lerp(a.sky, b.sky, t); RenderSettings.skybox = workingSky;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = Color.Lerp(a.ambientSky, b.ambientSky, t);
        RenderSettings.ambientEquatorColor = Color.Lerp(a.ambientEquator, b.ambientEquator, t);
        RenderSettings.ambientGroundColor = Color.Lerp(a.ambientGround, b.ambientGround, t);
        RenderSettings.ambientIntensity = Mathf.Lerp(a.ambientIntensity, b.ambientIntensity, t);
        RenderSettings.reflectionIntensity = Mathf.Lerp(a.reflectionIntensity, b.reflectionIntensity, t);
        RenderSettings.fog = b.fog; RenderSettings.fogMode = b.fogMode;
        RenderSettings.fogColor = Color.Lerp(a.fogColor, b.fogColor, t);
        RenderSettings.fogStartDistance = Mathf.Lerp(a.fog ? a.fogStart : 2000, b.fogStart, t);
        RenderSettings.fogEndDistance = Mathf.Lerp(a.fog ? a.fogEnd : 4000, b.fogEnd, t);
        RenderSettings.fogDensity = Mathf.Lerp(a.fog ? a.fogDensity : 0, b.fogDensity, t);
        sun.color = Color.Lerp(a.sunColor, b.sunColor, t);
        sun.intensity = Mathf.Lerp(a.sunIntensity, b.sunIntensity, t);
        sun.transform.rotation = Quaternion.Slerp(a.sunRotation, b.sunRotation, t);
    }
}
