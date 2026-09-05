using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class ElectricGrenadeVfxSetup
{
    private const string GeneratedFolder =
        "Assets/Game/Generated/VFX/ElectricGrenadeV3";

    private const string GeneratedRootName =
        "Generated_ElectricBurstV3";

    private const string LineMaterialPath =
        GeneratedFolder + "/M_ElectricArc_Additive.mat";

    private const string CoreMaterialPath =
        GeneratedFolder + "/M_ElectricCore.mat";

    private const string HaloMaterialPath =
        GeneratedFolder + "/M_ElectricHalo_Additive.mat";

    private const string ParticleMaterialPath =
        GeneratedFolder + "/M_ElectricParticle_Additive.mat";

    private const string SoftDotTexturePath =
        GeneratedFolder + "/T_ElectricSoftDot.asset";

    [MenuItem("Tools/Kids VS Aliens/Setup/Electric Grenade VFX V3")]
    public static void Run()
    {
        ElectricGrenadeEffectData effectData =
            FindElectricEffectData();

        if (effectData == null)
        {
            Debug.LogError(
                "Electric Grenade VFX V3: could not find ElectricGrenadeEffectData.");

            return;
        }

        if (effectData.burstPrefab == null)
        {
            Debug.LogError(
                $"Electric Grenade VFX V3: {effectData.name} has no burstPrefab assigned.",
                effectData);

            return;
        }

        string prefabPath =
            AssetDatabase.GetAssetPath(
                effectData.burstPrefab.gameObject);

        if (string.IsNullOrWhiteSpace(prefabPath))
        {
            Debug.LogError(
                "Electric Grenade VFX V3: could not resolve the burst prefab asset path.",
                effectData);

            return;
        }

        EnsureGeneratedFolder();

        Texture2D softDot =
            GetOrCreateSoftDotTexture();

        Material lineMaterial =
            GetOrCreateAdditiveMaterial(
                LineMaterialPath,
                null,
                Color.white);

        Material coreMaterial =
            GetOrCreateCoreMaterial();

        Material haloMaterial =
            GetOrCreateAdditiveMaterial(
                HaloMaterialPath,
                null,
                Color.white);

        Material particleMaterial =
            GetOrCreateAdditiveMaterial(
                ParticleMaterialPath,
                softDot,
                Color.white);

        GameObject prefabRoot =
            PrefabUtility.LoadPrefabContents(
                prefabPath);

        try
        {
            ElectricGrenadeBurstVFX burst =
                prefabRoot.GetComponentInChildren<ElectricGrenadeBurstVFX>(true);

            if (burst == null)
            {
                Debug.LogError(
                    $"Electric Grenade VFX V3: no ElectricGrenadeBurstVFX found in {prefabPath}.");

                return;
            }

            DisableLegacyBillboardLayers(burst);

            Transform previousGenerated =
                burst.transform.Find(
                    GeneratedRootName);

            if (previousGenerated != null)
            {
                UnityEngine.Object.DestroyImmediate(
                    previousGenerated.gameObject);
            }

            Transform previousV2 =
                burst.transform.Find(
                    "Generated_ElectricBurstV2");

            if (previousV2 != null)
            {
                UnityEngine.Object.DestroyImmediate(
                    previousV2.gameObject);
            }

            GameObject generatedRootObject =
                new GameObject(
                    GeneratedRootName);

            Transform generatedRoot =
                generatedRootObject.transform;

            generatedRoot.SetParent(
                burst.transform,
                false);

            Renderer coreRenderer =
                CreateSphere(
                    generatedRoot,
                    "EnergyCore",
                    coreMaterial);

            Renderer haloRenderer =
                CreateSphere(
                    generatedRoot,
                    "EnergyHalo",
                    haloMaterial);

            LineRenderer shockRing =
                CreateShockRing(
                    generatedRoot,
                    lineMaterial);

            ElectricArcVFX[] radialArcs =
                CreateArcGroup(
                    generatedRoot,
                    "RadialArcs",
                    10,
                    lineMaterial);

            ElectricArcVFX[] secondaryArcs =
                CreateArcGroup(
                    generatedRoot,
                    "SecondaryArcs",
                    7,
                    lineMaterial);

            ElectricArcVFX[] targetArcs =
                CreateArcGroup(
                    generatedRoot,
                    "TargetArcs",
                    6,
                    lineMaterial);

            ElectricArcVFX[] groundArcs =
                CreateArcGroup(
                    generatedRoot,
                    "GroundArcs",
                    8,
                    lineMaterial);

            ParticleSystem sparks =
                CreateSparkBurst(
                    generatedRoot,
                    particleMaterial);

            ParticleSystem residual =
                CreateResidualMotes(
                    generatedRoot,
                    particleMaterial);

            WireBurst(
                burst,
                coreRenderer,
                haloRenderer,
                shockRing,
                sparks,
                residual,
                radialArcs,
                secondaryArcs,
                targetArcs,
                groundArcs);

            PrefabUtility.SaveAsPrefabAsset(
                prefabRoot,
                prefabPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    prefabPath);

            Debug.Log(
                "Electric Grenade VFX V3 configured. " +
                "V3 fixes electric color/fading, keeps the core compact, extends the fading shock ring, " +
                "and uses finer jagged lightning plus colored sparks. Throw one grenade and inspect the burst.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(
                prefabRoot);
        }
    }

    private static ElectricGrenadeEffectData FindElectricEffectData()
    {
        string[] guids =
            AssetDatabase.FindAssets(
                "t:ElectricGrenadeEffectData");

        ElectricGrenadeEffectData fallback = null;

        for (int i = 0;
             i < guids.Length;
             i++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(
                    guids[i]);

            ElectricGrenadeEffectData data =
                AssetDatabase.LoadAssetAtPath<ElectricGrenadeEffectData>(
                    path);

            if (data == null)
                continue;

            fallback ??= data;

            if (data.name.IndexOf(
                    "ElectricGrenade",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return data;
            }
        }

        return fallback;
    }

    private static void DisableLegacyBillboardLayers(
        ElectricGrenadeBurstVFX burst)
    {
        SerializedObject serialized =
            new SerializedObject(burst);

        DisableParticleReference(
            serialized.FindProperty("coreFlash"));

        DisableParticleReference(
            serialized.FindProperty("shockRing"));

        // V2 generates its own coherent spark/mote layers as well.
        DisableParticleReference(
            serialized.FindProperty("sparks"));

        DisableParticleReference(
            serialized.FindProperty("residualParticles"));

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void DisableParticleReference(
        SerializedProperty property)
    {
        if (property == null)
            return;

        ParticleSystem particles =
            property.objectReferenceValue as ParticleSystem;

        if (particles != null)
        {
            ParticleSystem.MainModule main =
                particles.main;

            main.playOnAwake = false;

            particles.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear);

            particles.gameObject.SetActive(false);
        }

        property.objectReferenceValue = null;
    }

    private static Renderer CreateSphere(
        Transform parent,
        string name,
        Material material)
    {
        GameObject sphere =
            GameObject.CreatePrimitive(
                PrimitiveType.Sphere);

        sphere.name = name;

        sphere.transform.SetParent(
            parent,
            false);

        sphere.transform.localPosition =
            Vector3.zero;

        sphere.transform.localRotation =
            Quaternion.identity;

        sphere.transform.localScale =
            Vector3.zero;

        Collider collider =
            sphere.GetComponent<Collider>();

        if (collider != null)
        {
            UnityEngine.Object.DestroyImmediate(
                collider);
        }

        Renderer renderer =
            sphere.GetComponent<Renderer>();

        renderer.sharedMaterial = material;
        renderer.shadowCastingMode =
            ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage =
            LightProbeUsage.Off;
        renderer.reflectionProbeUsage =
            ReflectionProbeUsage.Off;
        renderer.enabled = false;

        return renderer;
    }

    private static LineRenderer CreateShockRing(
        Transform parent,
        Material material)
    {
        GameObject ringObject =
            new GameObject(
                "ShockRing3D");

        ringObject.transform.SetParent(
            parent,
            false);

        LineRenderer line =
            ringObject.AddComponent<LineRenderer>();

        ConfigureLineRenderer(
            line,
            material);

        line.loop = true;
        line.positionCount = 40;
        line.startWidth = 0.065f;
        line.endWidth = 0.065f;
        line.enabled = false;

        return line;
    }

    private static ElectricArcVFX[] CreateArcGroup(
        Transform parent,
        string groupName,
        int count,
        Material material)
    {
        GameObject groupObject =
            new GameObject(
                groupName);

        groupObject.transform.SetParent(
            parent,
            false);

        ElectricArcVFX[] arcs =
            new ElectricArcVFX[count];

        for (int i = 0;
             i < count;
             i++)
        {
            GameObject arcObject =
                new GameObject(
                    $"Arc_{i + 1:00}");

            arcObject.transform.SetParent(
                groupObject.transform,
                false);

            LineRenderer line =
                arcObject.AddComponent<LineRenderer>();

            ConfigureLineRenderer(
                line,
                material);

            line.positionCount = 18;
            line.enabled = false;

            ElectricArcVFX arc =
                arcObject.AddComponent<ElectricArcVFX>();

            arcs[i] = arc;
        }

        return arcs;
    }

    private static void ConfigureLineRenderer(
        LineRenderer line,
        Material material)
    {
        line.sharedMaterial = material;
        line.useWorldSpace = true;
        line.alignment =
            LineAlignment.View;
        line.textureMode =
            LineTextureMode.Stretch;
        line.numCornerVertices = 0;
        line.numCapVertices = 0;
        line.shadowCastingMode =
            ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.lightProbeUsage =
            LightProbeUsage.Off;
        line.reflectionProbeUsage =
            ReflectionProbeUsage.Off;
    }

    private static ParticleSystem CreateSparkBurst(
        Transform parent,
        Material material)
    {
        GameObject objectRoot =
            new GameObject(
                "Sparks");

        objectRoot.transform.SetParent(
            parent,
            false);

        ParticleSystem system =
            objectRoot.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main =
            system.main;

        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace =
            ParticleSystemSimulationSpace.World;
        main.startLifetime =
            new ParticleSystem.MinMaxCurve(
                0.11f,
                0.22f);
        main.startSpeed =
            new ParticleSystem.MinMaxCurve(
                4.5f,
                8.5f);
        main.startSize =
            new ParticleSystem.MinMaxCurve(
                0.014f,
                0.032f);
        main.maxParticles = 56;

        ParticleSystem.EmissionModule emission =
            system.emission;

        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(
            new[]
            {
                new ParticleSystem.Burst(
                    0f,
                    (short)30,
                    (short)40)
            });

        ParticleSystem.ShapeModule shape =
            system.shape;

        shape.enabled = true;
        shape.shapeType =
            ParticleSystemShapeType.Sphere;
        shape.radius = 0.12f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime =
            system.colorOverLifetime;

        colorOverLifetime.enabled = true;
        colorOverLifetime.color =
            CreateElectricGradient(
                new Color(0.25f, 0.88f, 1f, 1f),
                new Color(0.58f, 0.18f, 1f, 1f));

        ParticleSystemRenderer renderer =
            objectRoot.GetComponent<ParticleSystemRenderer>();

        renderer.sharedMaterial = material;
        renderer.renderMode =
            ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.08f;
        renderer.lengthScale = 0.28f;
        renderer.shadowCastingMode =
            ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        system.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear);

        return system;
    }

    private static ParticleSystem CreateResidualMotes(
        Transform parent,
        Material material)
    {
        GameObject objectRoot =
            new GameObject(
                "ResidualMotes");

        objectRoot.transform.SetParent(
            parent,
            false);

        ParticleSystem system =
            objectRoot.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main =
            system.main;

        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace =
            ParticleSystemSimulationSpace.World;
        main.startLifetime =
            new ParticleSystem.MinMaxCurve(
                0.35f,
                0.62f);
        main.startSpeed =
            new ParticleSystem.MinMaxCurve(
                0.5f,
                2.1f);
        main.startSize =
            new ParticleSystem.MinMaxCurve(
                0.025f,
                0.055f);
        main.maxParticles = 40;

        ParticleSystem.EmissionModule emission =
            system.emission;

        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(
            new[]
            {
                new ParticleSystem.Burst(
                    0.035f,
                    (short)16,
                    (short)24)
            });

        ParticleSystem.ShapeModule shape =
            system.shape;

        shape.enabled = true;
        shape.shapeType =
            ParticleSystemShapeType.Sphere;
        shape.radius = 0.2f;

        ParticleSystem.NoiseModule noise =
            system.noise;

        noise.enabled = true;
        noise.strength = 0.22f;
        noise.frequency = 0.75f;
        noise.scrollSpeed = 0.35f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime =
            system.colorOverLifetime;

        colorOverLifetime.enabled = true;
        colorOverLifetime.color =
            CreateElectricGradient(
                new Color(0.32f, 0.82f, 1f, 1f),
                new Color(0.75f, 0.2f, 1f, 1f));

        ParticleSystemRenderer renderer =
            objectRoot.GetComponent<ParticleSystemRenderer>();

        renderer.sharedMaterial = material;
        renderer.renderMode =
            ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode =
            ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        system.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear);

        return system;
    }

    private static ParticleSystem.MinMaxGradient CreateElectricGradient(
        Color start,
        Color end)
    {
        Gradient gradient =
            new Gradient();

        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(start, 0f),
                new GradientColorKey(Color.Lerp(start, end, 0.28f), 0.38f),
                new GradientColorKey(end, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.85f, 0.45f),
                new GradientAlphaKey(0f, 1f)
            });

        return new ParticleSystem.MinMaxGradient(
            gradient);
    }

    private static void WireBurst(
        ElectricGrenadeBurstVFX burst,
        Renderer coreRenderer,
        Renderer haloRenderer,
        LineRenderer shockRing,
        ParticleSystem sparks,
        ParticleSystem residual,
        ElectricArcVFX[] radialArcs,
        ElectricArcVFX[] secondaryArcs,
        ElectricArcVFX[] targetArcs,
        ElectricArcVFX[] groundArcs)
    {
        SerializedObject serialized =
            new SerializedObject(burst);

        SetObjectReference(
            serialized,
            "energyCore",
            coreRenderer.transform);

        SetObjectReference(
            serialized,
            "energyCoreRenderer",
            coreRenderer);

        SetObjectReference(
            serialized,
            "energyHalo",
            haloRenderer.transform);

        SetObjectReference(
            serialized,
            "energyHaloRenderer",
            haloRenderer);

        SetObjectReference(
            serialized,
            "shockRingLine",
            shockRing);

        SetObjectReference(
            serialized,
            "sparks",
            sparks);

        SetObjectReference(
            serialized,
            "residualParticles",
            residual);

        SetArray(
            serialized,
            "radialArcs",
            radialArcs);

        SetArray(
            serialized,
            "secondaryArcs",
            secondaryArcs);

        SetArray(
            serialized,
            "targetArcs",
            targetArcs);

        SetArray(
            serialized,
            "groundArcs",
            groundArcs);

        // The component already existed in V2, so field initializers alone
        // would not migrate its serialized timings. Re-apply the V3 authored
        // presentation defaults every time this setup command is run.
        SetFloat(serialized, "presentationDuration", 0.85f);
        SetFloat(serialized, "coreDuration", 0.24f);
        SetFloat(serialized, "corePeakDiameterFactor", 0.085f);
        SetFloat(serialized, "coreIntensity", 2.2f);
        SetFloat(serialized, "haloIntensity", 0.85f);
        SetFloat(serialized, "shockRingDuration", 0.62f);
        SetFloat(serialized, "shockRingRadiusFactor", 1.08f);
        SetInt(serialized, "shockRingSegments", 48);
        SetFloat(serialized, "radialDuration", 0.38f);
        SetFloat(serialized, "radialRetargetInterval", 0.05f);
        SetVector2(serialized, "radialRadiusRange", new Vector2(0.38f, 0.98f));
        SetFloat(serialized, "radialArcLifetime", 0.12f);
        SetFloat(serialized, "radialWidthMultiplier", 0.88f);
        SetFloat(serialized, "radialJitterMultiplier", 0.75f);
        SetFloat(serialized, "secondaryDuration", 0.48f);
        SetFloat(serialized, "secondaryRetargetInterval", 0.07f);
        SetFloat(serialized, "secondaryArcLifetime", 0.13f);
        SetFloat(serialized, "secondaryWidthMultiplier", 0.62f);
        SetFloat(serialized, "secondaryJitterMultiplier", 0.9f);
        SetFloat(serialized, "targetRetargetInterval", 0.11f);
        SetFloat(serialized, "targetArcLifetime", 0.18f);
        SetFloat(serialized, "targetArcWidthMultiplier", 1.0f);
        SetFloat(serialized, "targetArcJitterMultiplier", 0.55f);
        SetFloat(serialized, "groundRetargetInterval", 0.085f);
        SetVector2(serialized, "groundArcRadiusRange", new Vector2(0.32f, 0.95f));
        SetFloat(serialized, "groundArcLifetime", 0.16f);
        SetFloat(serialized, "groundArcWidthMultiplier", 0.85f);
        SetFloat(serialized, "groundArcJitterMultiplier", 0.55f);
        SetFloat(serialized, "originLift", 0.12f);
        SetFloat(serialized, "targetLift", 0.08f);
        SetFloat(serialized, "groundHeightJitter", 0.035f);
        SetColor(serialized, "secondaryColor", new Color(0.58f, 0.18f, 1f, 1f));

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(burst);
    }

    private static void SetObjectReference(
        SerializedObject serialized,
        string propertyName,
        UnityEngine.Object value)
    {
        SerializedProperty property =
            serialized.FindProperty(
                propertyName);

        if (property != null)
        {
            property.objectReferenceValue =
                value;
        }
    }

    private static void SetArray<T>(
        SerializedObject serialized,
        string propertyName,
        T[] values)
        where T : UnityEngine.Object
    {
        SerializedProperty property =
            serialized.FindProperty(
                propertyName);

        if (property == null)
            return;

        property.arraySize =
            values != null
                ? values.Length
                : 0;

        for (int i = 0;
             i < property.arraySize;
             i++)
        {
            property.GetArrayElementAtIndex(i)
                .objectReferenceValue =
                values[i];
        }
    }

    private static void SetFloat(
        SerializedObject serialized,
        string propertyName,
        float value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null) property.floatValue = value;
    }

    private static void SetInt(
        SerializedObject serialized,
        string propertyName,
        int value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null) property.intValue = value;
    }

    private static void SetVector2(
        SerializedObject serialized,
        string propertyName,
        Vector2 value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null) property.vector2Value = value;
    }

    private static void SetColor(
        SerializedObject serialized,
        string propertyName,
        Color value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null) property.colorValue = value;
    }

    private static void EnsureGeneratedFolder()
    {
        string[] parts =
            GeneratedFolder.Split('/');

        string current =
            parts[0];

        for (int i = 1;
             i < parts.Length;
             i++)
        {
            string next =
                current +
                "/" +
                parts[i];

            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(
                    current,
                    parts[i]);
            }

            current = next;
        }
    }

    private static Texture2D GetOrCreateSoftDotTexture()
    {
        Texture2D existing =
            AssetDatabase.LoadAssetAtPath<Texture2D>(
                SoftDotTexturePath);

        if (existing != null)
            return existing;

        const int size = 64;

        Texture2D texture =
            new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                false,
                true)
            {
                name = "T_ElectricSoftDot",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

        Color[] pixels =
            new Color[size * size];

        for (int y = 0;
             y < size;
             y++)
        {
            for (int x = 0;
                 x < size;
                 x++)
            {
                float nx =
                    (x + 0.5f) /
                    size *
                    2f -
                    1f;

                float ny =
                    (y + 0.5f) /
                    size *
                    2f -
                    1f;

                float distance =
                    Mathf.Sqrt(
                        nx * nx +
                        ny * ny);

                float alpha =
                    Mathf.Clamp01(
                        1f - distance);

                alpha =
                    alpha *
                    alpha *
                    (3f - 2f * alpha);

                pixels[
                    y * size + x] =
                    new Color(
                        1f,
                        1f,
                        1f,
                        alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, false);

        AssetDatabase.CreateAsset(
            texture,
            SoftDotTexturePath);

        return texture;
    }

    private static Material GetOrCreateCoreMaterial()
    {
        Material material =
            AssetDatabase.LoadAssetAtPath<Material>(
                CoreMaterialPath);

        Shader shader =
            Shader.Find(
                "KidsVsAliens/VFX/ElectricAdditive");

        if (shader == null)
        {
            throw new InvalidOperationException(
                "Electric Grenade VFX V3 requires Assets/Game/Shaders/VFX/ElectricAdditive.shader.");
        }

        if (material == null)
        {
            material =
                new Material(shader)
                {
                    name = "M_ElectricCore"
                };

            AssetDatabase.CreateAsset(
                material,
                CoreMaterialPath);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor(
                "_BaseColor",
                Color.white);
        }

        if (material.HasProperty("_Intensity"))
        {
            material.SetFloat(
                "_Intensity",
                1.45f);
        }

        material.renderQueue =
            (int)RenderQueue.Transparent;

        EditorUtility.SetDirty(material);

        return material;
    }

    private static Material GetOrCreateAdditiveMaterial(
        string path,
        Texture texture,
        Color baseColor)
    {
        Material material =
            AssetDatabase.LoadAssetAtPath<Material>(
                path);

        Shader shader =
            Shader.Find(
                "KidsVsAliens/VFX/ElectricAdditive");

        if (shader == null)
        {
            throw new InvalidOperationException(
                "Electric Grenade VFX V3 requires Assets/Game/Shaders/VFX/ElectricAdditive.shader.");
        }

        if (material == null)
        {
            material =
                new Material(shader);

            material.name =
                System.IO.Path.GetFileNameWithoutExtension(
                    path);

            AssetDatabase.CreateAsset(
                material,
                path);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor(
                "_BaseColor",
                baseColor);
        }

        if (texture != null &&
            material.HasProperty("_BaseMap"))
        {
            material.SetTexture(
                "_BaseMap",
                texture);
        }

        if (material.HasProperty("_Intensity"))
        {
            float intensity =
                path == LineMaterialPath
                    ? 1.55f
                    : path == ParticleMaterialPath
                        ? 1.65f
                        : 1.15f;

            material.SetFloat(
                "_Intensity",
                intensity);
        }

        material.renderQueue =
            (int)RenderQueue.Transparent;

        EditorUtility.SetDirty(material);

        return material;
    }
}
