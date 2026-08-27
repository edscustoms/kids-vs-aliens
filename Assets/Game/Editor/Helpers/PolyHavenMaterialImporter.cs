#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace KidsVsAliens.EditorTools
{
    public sealed class PolyHavenMaterialImporter : EditorWindow
    {
        private const string MenuPath = "Tools/Kids VS Aliens/Helpers/Poly Haven Material Importer";
        private const string ApiBase = "https://api.polyhaven.com";
        private const string MaterialsRoot = "Assets/Game/Art/Environment/Materials";
        private const string TexturesRoot = "Assets/Game/Art/Environment/Textures";
        private const string UserAgent = "KidsVSAliens-Unity-PolyHaven-Importer/1.0";

        private static readonly HttpClient Http = CreateHttpClient();

        private string _url = string.Empty;
        private string _assetId = string.Empty;
        private string _status = "Paste a Poly Haven texture URL, then load it.";
        private MessageType _statusType = MessageType.Info;
        private bool _busy;
        private Vector2 _scroll;

        private readonly List<FileVariant> _variants = new();
        private readonly Dictionary<MapType, bool> _selected = new();
        private readonly Dictionary<MapType, string> _preferredFormats = new();

        private List<string> _resolutions = new() { "1k" };
        private int _resolutionIndex;

        private List<string> _groups = new() { "Ground" };
        private int _groupIndex;

        [MenuItem(MenuPath)]
        public static void ShowWindow()
        {
            var window = GetWindow<PolyHavenMaterialImporter>("Poly Haven Importer");
            window.minSize = new Vector2(520f, 560f);
            window.Show();
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            return client;
        }

        private void OnEnable()
        {
            RefreshGroups();
            EnsureSelectionKeys();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            GUILayout.Space(8);
            EditorGUILayout.LabelField("Poly Haven Material Importer", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Downloads only the maps you select, keeps Poly Haven filenames untouched, and never overwrites existing assets.",
                EditorStyles.wordWrappedMiniLabel
            );
            GUILayout.Space(8);

            using (new EditorGUI.DisabledScope(_busy))
            {
                EditorGUILayout.LabelField("Poly Haven URL", EditorStyles.boldLabel);
                _url = EditorGUILayout.TextField(_url);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Load / Refresh Asset", GUILayout.Height(26)))
                        LoadAssetAsync();

                    if (GUILayout.Button("Clear", GUILayout.Width(80), GUILayout.Height(26)))
                        ClearAsset();
                }
            }

            if (!string.IsNullOrWhiteSpace(_assetId))
            {
                GUILayout.Space(10);
                EditorGUILayout.LabelField("Asset", _assetId);
                EditorGUILayout.LabelField("Material", GetMaterialName(_assetId));

                using (new EditorGUI.DisabledScope(_busy))
                {
                    DrawDestinationGroup();
                    DrawResolution();
                    DrawMapSelection();

                    GUILayout.Space(10);
                    DrawDestinationPreview();

                    GUILayout.Space(10);
                    if (GUILayout.Button("IMPORT SELECTED", GUILayout.Height(34)))
                        ImportSelectedAsync();
                }
            }

            GUILayout.Space(12);
            EditorGUILayout.HelpBox(_status, _statusType);

            GUILayout.FlexibleSpace();
            GUILayout.Space(8);
            EditorGUILayout.LabelField("Powered by Poly Haven", EditorStyles.centeredGreyMiniLabel);
            GUILayout.Space(4);

            EditorGUILayout.EndScrollView();
        }

        private void DrawDestinationGroup()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _groupIndex = EditorGUILayout.Popup(
                    "Environment Group",
                    _groupIndex,
                    _groups.ToArray()
                );
                if (GUILayout.Button("↻", GUILayout.Width(28)))
                    RefreshGroups();
            }
        }

        private void DrawResolution()
        {
            if (_resolutions.Count == 0)
                _resolutions.Add("1k");

            _resolutionIndex = Mathf.Clamp(_resolutionIndex, 0, _resolutions.Count - 1);
            _resolutionIndex = EditorGUILayout.Popup(
                "Resolution",
                _resolutionIndex,
                _resolutions.ToArray()
            );
        }

        private void DrawMapSelection()
        {
            GUILayout.Space(10);
            EditorGUILayout.LabelField("Maps", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Select Recommended"))
                    SelectRecommended();
                if (GUILayout.Button("Clear All"))
                    ClearSelections();
            }

            GUILayout.Space(4);

            var resolution = CurrentResolution;
            foreach (var mapType in MapDisplayOrder)
            {
                var formats = GetAvailableFormats(mapType, resolution);
                if (formats.Count == 0)
                    continue;

                EnsureSelectionKeys();
                EnsurePreferredFormat(mapType, formats);

                using (new EditorGUILayout.HorizontalScope())
                {
                    _selected[mapType] = EditorGUILayout.ToggleLeft(
                        GetDisplayName(mapType),
                        _selected[mapType],
                        GUILayout.MinWidth(210)
                    );

                    var currentFormat = _preferredFormats[mapType];
                    var currentIndex = Mathf.Max(
                        0,
                        formats.FindIndex(f =>
                            string.Equals(f, currentFormat, StringComparison.OrdinalIgnoreCase)
                        )
                    );
                    currentIndex = EditorGUILayout.Popup(
                        currentIndex,
                        formats.Select(f => f.ToUpperInvariant()).ToArray(),
                        GUILayout.Width(90)
                    );
                    _preferredFormats[mapType] = formats[currentIndex];
                }
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(
                "Recommended = Diffuse + Normal (GL) + ARM. ARM is converted to a URP-packed mask automatically.",
                EditorStyles.wordWrappedMiniLabel
            );
        }

        private void DrawDestinationPreview()
        {
            var group = CurrentGroup;
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(
                $"Textures: {TexturesRoot}/{group}/{_assetId}",
                EditorStyles.textField,
                GUILayout.Height(EditorGUIUtility.singleLineHeight)
            );
            EditorGUILayout.SelectableLabel(
                $"Material: {MaterialsRoot}/{group}/{GetMaterialName(_assetId)}.mat",
                EditorStyles.textField,
                GUILayout.Height(EditorGUIUtility.singleLineHeight)
            );
        }

        private async void LoadAssetAsync()
        {
            if (_busy)
                return;

            var id = ExtractAssetId(_url);
            if (string.IsNullOrWhiteSpace(id))
            {
                SetStatus(
                    "Could not read a Poly Haven asset ID from that URL. Expected something like https://polyhaven.com/a/blue_metal_plate",
                    MessageType.Error
                );
                return;
            }

            _busy = true;
            _assetId = id;
            _variants.Clear();
            SetStatus($"Loading {_assetId} from Poly Haven…", MessageType.Info);
            Repaint();

            try
            {
                var infoJson = await Http.GetStringAsync(
                    $"{ApiBase}/info/{Uri.EscapeDataString(_assetId)}"
                );
                var info = MiniJson.Parse(infoJson) as Dictionary<string, object>;
                if (info == null)
                    throw new Exception("Poly Haven returned invalid asset metadata.");

                if (TryGetLong(info, "type", out var type) && type != 1)
                    throw new Exception(
                        "This helper currently supports Poly Haven texture assets only."
                    );

                var filesJson = await Http.GetStringAsync(
                    $"{ApiBase}/files/{Uri.EscapeDataString(_assetId)}"
                );
                var parsed = MiniJson.Parse(filesJson);
                CollectFileVariants(parsed, new List<string>(), _variants);

                if (_variants.Count == 0)
                    throw new Exception("No downloadable texture maps were found for this asset.");

                RebuildResolutionList();
                ResetFormatsForCurrentResolution();
                SelectRecommended();

                SetStatus(
                    $"Loaded {_assetId}. Found {_variants.Count} downloadable variants.",
                    MessageType.Info
                );
            }
            catch (Exception ex)
            {
                _variants.Clear();
                SetStatus($"Load failed: {ex.Message}", MessageType.Error);
            }
            finally
            {
                _busy = false;
                Repaint();
            }
        }

        private async void ImportSelectedAsync()
        {
            if (_busy || string.IsNullOrWhiteSpace(_assetId))
                return;

            var selectedTypes = MapDisplayOrder
                .Where(t => _selected.TryGetValue(t, out var isSelected) && isSelected)
                .ToList();

            if (selectedTypes.Count == 0)
            {
                SetStatus("Select at least one map first.", MessageType.Warning);
                return;
            }

            _busy = true;
            SetStatus("Importing selected maps…", MessageType.Info);
            Repaint();

            var downloaded = 0;
            var skipped = 0;
            var missing = 0;
            var importedPaths = new Dictionary<MapType, string>();

            try
            {
                var group = CurrentGroup;
                var textureFolder = $"{TexturesRoot}/{group}/{_assetId}";
                var materialFolder = $"{MaterialsRoot}/{group}";
                EnsureAssetFolder(textureFolder);
                EnsureAssetFolder(materialFolder);

                foreach (var mapType in selectedTypes)
                {
                    var variant = GetChosenVariant(mapType, CurrentResolution);
                    if (variant == null)
                    {
                        missing++;
                        continue;
                    }

                    var fileName = GetFileNameFromUrl(variant.Url);
                    if (string.IsNullOrWhiteSpace(fileName))
                    {
                        missing++;
                        continue;
                    }

                    var existing = FindExistingTextureByStem(fileName);
                    if (!string.IsNullOrEmpty(existing))
                    {
                        importedPaths[mapType] = existing;
                        skipped++;
                        continue;
                    }

                    var destinationAssetPath = $"{textureFolder}/{fileName}";
                    var destinationAbsolutePath = ToAbsolutePath(destinationAssetPath);

                    SetStatus($"Downloading {GetDisplayName(mapType)}…", MessageType.Info);
                    Repaint();

                    var bytes = await Http.GetByteArrayAsync(variant.Url);
                    File.WriteAllBytes(destinationAbsolutePath, bytes);
                    AssetDatabase.ImportAsset(
                        destinationAssetPath,
                        ImportAssetOptions.ForceSynchronousImport
                    );
                    ConfigureImportedTexture(destinationAssetPath, mapType);

                    importedPaths[mapType] = destinationAssetPath;
                    downloaded++;
                }

                AssetDatabase.Refresh();

                var materialName = GetMaterialName(_assetId);
                var existingMaterialPath = FindExistingMaterialByName(materialName);
                var materialCreated = false;

                if (string.IsNullOrEmpty(existingMaterialPath))
                {
                    var packedPath = BuildUrpPackedMapIfUseful(
                        importedPaths,
                        textureFolder,
                        CurrentResolution
                    );
                    var newMaterialPath = $"{materialFolder}/{materialName}.mat";
                    CreateUrpMaterial(newMaterialPath, importedPaths, packedPath);
                    materialCreated = true;
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                var materialResult = materialCreated ? "created" : "already existed — untouched";
                SetStatus(
                    $"Done. Downloaded: {downloaded} | Skipped existing: {skipped} | Missing: {missing} | Material: {materialResult}.",
                    missing > 0 ? MessageType.Warning : MessageType.Info
                );
            }
            catch (Exception ex)
            {
                SetStatus($"Import failed: {ex.Message}", MessageType.Error);
                Debug.LogException(ex);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                _busy = false;
                Repaint();
            }
        }

        private string BuildUrpPackedMapIfUseful(
            IReadOnlyDictionary<MapType, string> importedPaths,
            string textureFolder,
            string resolution
        )
        {
            if (importedPaths.TryGetValue(MapType.Arm, out var armPath))
            {
                return BuildUrpPackedFromArm(armPath, textureFolder, resolution);
            }

            var hasRough = importedPaths.TryGetValue(MapType.Roughness, out var roughPath);
            var hasMetal = importedPaths.TryGetValue(MapType.Metallic, out var metalPath);
            var hasAo = importedPaths.TryGetValue(MapType.AmbientOcclusion, out var aoPath);

            if (!hasRough && !hasMetal)
                return null;

            return BuildUrpPackedFromSeparateMaps(
                hasAo ? aoPath : null,
                hasRough ? roughPath : null,
                hasMetal ? metalPath : null,
                textureFolder,
                resolution
            );
        }

        private string BuildUrpPackedFromArm(
            string armAssetPath,
            string textureFolder,
            string resolution
        )
        {
            var outputFileName = $"{_assetId}_urp_mask_{resolution}.png";
            var existing = FindExistingTextureByStem(outputFileName);
            if (!string.IsNullOrEmpty(existing))
                return existing;

            var outputAssetPath = $"{textureFolder}/{outputFileName}";
            var source = LoadTextureLinear(armAssetPath);
            if (source == null)
                return null;

            try
            {
                var sourcePixels = source.GetPixels32();
                var outputPixels = new Color32[sourcePixels.Length];

                for (var i = 0; i < sourcePixels.Length; i++)
                {
                    // Poly Haven ARM: R = AO, G = Roughness, B = Metallic.
                    // URP packed:      R = Metallic, G = AO, B = unused, A = Smoothness.
                    var p = sourcePixels[i];
                    outputPixels[i] = new Color32(p.b, p.r, 0, (byte)(255 - p.g));
                }

                WritePackedPng(outputAssetPath, source.width, source.height, outputPixels);
                return outputAssetPath;
            }
            finally
            {
                DestroyImmediate(source);
            }
        }

        private string BuildUrpPackedFromSeparateMaps(
            string aoAssetPath,
            string roughAssetPath,
            string metallicAssetPath,
            string textureFolder,
            string resolution
        )
        {
            var outputFileName = $"{_assetId}_urp_mask_{resolution}.png";
            var existing = FindExistingTextureByStem(outputFileName);
            if (!string.IsNullOrEmpty(existing))
                return existing;

            var ao = string.IsNullOrEmpty(aoAssetPath) ? null : LoadTextureLinear(aoAssetPath);
            var rough = string.IsNullOrEmpty(roughAssetPath)
                ? null
                : LoadTextureLinear(roughAssetPath);
            var metal = string.IsNullOrEmpty(metallicAssetPath)
                ? null
                : LoadTextureLinear(metallicAssetPath);

            try
            {
                var reference = rough ?? metal ?? ao;
                if (reference == null)
                    return null;

                if (
                    (ao != null && (ao.width != reference.width || ao.height != reference.height))
                    || (
                        rough != null
                        && (rough.width != reference.width || rough.height != reference.height)
                    )
                    || (
                        metal != null
                        && (metal.width != reference.width || metal.height != reference.height)
                    )
                )
                {
                    throw new Exception(
                        "AO/Rough/Metal source maps have different dimensions; URP mask packing was skipped."
                    );
                }

                var aoPixels = ao?.GetPixels32();
                var roughPixels = rough?.GetPixels32();
                var metalPixels = metal?.GetPixels32();
                var outputPixels = new Color32[reference.width * reference.height];

                for (var i = 0; i < outputPixels.Length; i++)
                {
                    var aoValue = aoPixels != null ? aoPixels[i].r : (byte)255;
                    var roughValue = roughPixels != null ? roughPixels[i].r : (byte)127;
                    var metalValue = metalPixels != null ? metalPixels[i].r : (byte)0;

                    outputPixels[i] = new Color32(metalValue, aoValue, 0, (byte)(255 - roughValue));
                }

                var outputAssetPath = $"{textureFolder}/{outputFileName}";
                WritePackedPng(outputAssetPath, reference.width, reference.height, outputPixels);
                return outputAssetPath;
            }
            finally
            {
                if (ao != null)
                    DestroyImmediate(ao);
                if (rough != null)
                    DestroyImmediate(rough);
                if (metal != null)
                    DestroyImmediate(metal);
            }
        }

        private static void WritePackedPng(
            string outputAssetPath,
            int width,
            int height,
            Color32[] pixels
        )
        {
            var packed = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            try
            {
                packed.SetPixels32(pixels);
                packed.Apply(false, false);
                File.WriteAllBytes(ToAbsolutePath(outputAssetPath), packed.EncodeToPNG());
            }
            finally
            {
                DestroyImmediate(packed);
            }

            AssetDatabase.ImportAsset(outputAssetPath, ImportAssetOptions.ForceSynchronousImport);
            ConfigureDataTexture(outputAssetPath);
        }

        private static Texture2D LoadTextureLinear(string assetPath)
        {
            var absolute = ToAbsolutePath(assetPath);
            if (!File.Exists(absolute))
                return null;

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(absolute), false))
            {
                DestroyImmediate(texture);
                return null;
            }

            return texture;
        }

        private static void CreateUrpMaterial(
            string materialAssetPath,
            IReadOnlyDictionary<MapType, string> texturePaths,
            string packedAssetPath
        )
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new Exception(
                    "Could not find shader 'Universal Render Pipeline/Lit'. Is URP installed/active?"
                );

            var material = new Material(shader);

            if (TryLoadTexture(texturePaths, MapType.Diffuse, out var diffuse))
            {
                material.SetTexture("_BaseMap", diffuse);
                material.SetColor("_BaseColor", Color.white);
            }

            if (TryLoadTexture(texturePaths, MapType.NormalGl, out var normal))
            {
                material.SetTexture("_BumpMap", normal);
                material.SetFloat("_BumpScale", 1f);
                material.EnableKeyword("_NORMALMAP");
            }

            if (!string.IsNullOrEmpty(packedAssetPath))
            {
                var packed = AssetDatabase.LoadAssetAtPath<Texture2D>(packedAssetPath);
                if (packed != null)
                {
                    material.SetTexture("_MetallicGlossMap", packed);
                    material.SetTexture("_OcclusionMap", packed);
                    material.SetFloat("_Metallic", 1f);
                    material.SetFloat("_Smoothness", 1f);
                    material.SetFloat("_OcclusionStrength", 1f);
                    material.EnableKeyword("_METALLICSPECGLOSSMAP");
                }
            }
            else if (TryLoadTexture(texturePaths, MapType.AmbientOcclusion, out var ao))
            {
                material.SetTexture("_OcclusionMap", ao);
                material.SetFloat("_OcclusionStrength", 1f);
            }

            if (TryLoadTexture(texturePaths, MapType.Displacement, out var displacement))
            {
                material.SetTexture("_ParallaxMap", displacement);
                material.SetFloat("_Parallax", 0.005f);
                material.EnableKeyword("_PARALLAXMAP");
            }

            if (TryLoadTexture(texturePaths, MapType.Emission, out var emission))
            {
                material.SetTexture("_EmissionMap", emission);
                material.SetColor("_EmissionColor", Color.white);
                material.EnableKeyword("_EMISSION");
            }

            AssetDatabase.CreateAsset(material, materialAssetPath);
        }

        private static bool TryLoadTexture(
            IReadOnlyDictionary<MapType, string> texturePaths,
            MapType type,
            out Texture2D texture
        )
        {
            texture = null;
            if (!texturePaths.TryGetValue(type, out var path) || string.IsNullOrEmpty(path))
                return false;

            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            return texture != null;
        }

        private static void ConfigureImportedTexture(string assetPath, MapType type)
        {
            if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
                return;

            importer.mipmapEnabled = true;

            switch (type)
            {
                case MapType.Diffuse:
                case MapType.Emission:
                    importer.textureType = TextureImporterType.Default;
                    importer.sRGBTexture = true;
                    break;

                case MapType.NormalGl:
                case MapType.NormalDx:
                    importer.textureType = TextureImporterType.NormalMap;
                    importer.sRGBTexture = false;
                    importer.convertToNormalmap = false;
                    break;

                default:
                    importer.textureType = TextureImporterType.Default;
                    importer.sRGBTexture = false;
                    break;
            }

            importer.SaveAndReimport();
        }

        private static void ConfigureDataTexture(string assetPath)
        {
            if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
                return;

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.mipmapEnabled = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.SaveAndReimport();
        }

        private FileVariant GetChosenVariant(MapType type, string resolution)
        {
            var candidates = _variants
                .Where(v =>
                    v.MapType == type
                    && string.Equals(v.Resolution, resolution, StringComparison.OrdinalIgnoreCase)
                )
                .ToList();

            if (candidates.Count == 0)
                return null;

            _preferredFormats.TryGetValue(type, out var preferred);
            if (!string.IsNullOrEmpty(preferred))
            {
                var exact = candidates.FirstOrDefault(v =>
                    string.Equals(v.Format, preferred, StringComparison.OrdinalIgnoreCase)
                );
                if (exact != null)
                    return exact;
            }

            foreach (var fallback in new[] { "jpg", "png", "exr" })
            {
                var match = candidates.FirstOrDefault(v =>
                    string.Equals(v.Format, fallback, StringComparison.OrdinalIgnoreCase)
                );
                if (match != null)
                    return match;
            }

            return candidates[0];
        }

        private List<string> GetAvailableFormats(MapType type, string resolution)
        {
            var formats = _variants
                .Where(v =>
                    v.MapType == type
                    && string.Equals(v.Resolution, resolution, StringComparison.OrdinalIgnoreCase)
                )
                .Select(v => v.Format.ToLowerInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(FormatSortOrder)
                .ToList();

            return formats;
        }

        private static int FormatSortOrder(string format)
        {
            return format.ToLowerInvariant() switch
            {
                "jpg" => 0,
                "png" => 1,
                "exr" => 2,
                _ => 10,
            };
        }

        private void EnsurePreferredFormat(MapType type, List<string> availableFormats)
        {
            if (
                _preferredFormats.TryGetValue(type, out var existing)
                && availableFormats.Contains(existing, StringComparer.OrdinalIgnoreCase)
            )
                return;

            var preferred = type is MapType.NormalGl or MapType.NormalDx
                ? availableFormats.FirstOrDefault(f =>
                    string.Equals(f, "png", StringComparison.OrdinalIgnoreCase)
                )
                : availableFormats.FirstOrDefault(f =>
                    string.Equals(f, "jpg", StringComparison.OrdinalIgnoreCase)
                );

            _preferredFormats[type] = preferred ?? availableFormats[0];
        }

        private void ResetFormatsForCurrentResolution()
        {
            _preferredFormats.Clear();
            foreach (var type in MapDisplayOrder)
            {
                var formats = GetAvailableFormats(type, CurrentResolution);
                if (formats.Count > 0)
                    EnsurePreferredFormat(type, formats);
            }
        }

        private void RebuildResolutionList()
        {
            _resolutions = _variants
                .Select(v => v.Resolution)
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(ResolutionSortOrder)
                .ToList();

            if (_resolutions.Count == 0)
                _resolutions.Add("1k");

            var oneK = _resolutions.FindIndex(r =>
                string.Equals(r, "1k", StringComparison.OrdinalIgnoreCase)
            );
            _resolutionIndex = oneK >= 0 ? oneK : 0;
        }

        private static int ResolutionSortOrder(string resolution)
        {
            var match = Regex.Match(resolution ?? string.Empty, @"(\d+)", RegexOptions.IgnoreCase);
            return match.Success && int.TryParse(match.Groups[1].Value, out var number)
                ? number
                : int.MaxValue;
        }

        private void SelectRecommended()
        {
            ClearSelections();
            var resolution = CurrentResolution;

            SetSelectedIfAvailable(MapType.Diffuse, resolution);
            SetSelectedIfAvailable(MapType.NormalGl, resolution);

            if (HasMap(MapType.Arm, resolution))
            {
                _selected[MapType.Arm] = true;
            }
            else
            {
                SetSelectedIfAvailable(MapType.AmbientOcclusion, resolution);
                SetSelectedIfAvailable(MapType.Roughness, resolution);
                SetSelectedIfAvailable(MapType.Metallic, resolution);
            }
        }

        private void SetSelectedIfAvailable(MapType type, string resolution)
        {
            if (HasMap(type, resolution))
                _selected[type] = true;
        }

        private bool HasMap(MapType type, string resolution)
        {
            return _variants.Any(v =>
                v.MapType == type
                && string.Equals(v.Resolution, resolution, StringComparison.OrdinalIgnoreCase)
            );
        }

        private void ClearSelections()
        {
            EnsureSelectionKeys();
            foreach (var key in MapDisplayOrder)
                _selected[key] = false;
        }

        private void EnsureSelectionKeys()
        {
            foreach (var mapType in MapDisplayOrder)
            {
                if (!_selected.ContainsKey(mapType))
                    _selected[mapType] = false;
            }
        }

        private void ClearAsset()
        {
            _url = string.Empty;
            _assetId = string.Empty;
            _variants.Clear();
            _preferredFormats.Clear();
            ClearSelections();
            _resolutions = new List<string> { "1k" };
            _resolutionIndex = 0;
            SetStatus("Paste a Poly Haven texture URL, then load it.", MessageType.Info);
        }

        private void RefreshGroups()
        {
            var previous = CurrentGroup;
            var absoluteRoot = ToAbsolutePath(MaterialsRoot);

            if (Directory.Exists(absoluteRoot))
            {
                _groups = Directory
                    .GetDirectories(absoluteRoot)
                    .Select(Path.GetFileName)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            if (_groups.Count == 0)
                _groups.Add("Ground");

            var previousIndex = _groups.FindIndex(g =>
                string.Equals(g, previous, StringComparison.OrdinalIgnoreCase)
            );
            _groupIndex = previousIndex >= 0 ? previousIndex : 0;
        }

        private static void EnsureAssetFolder(string assetFolder)
        {
            var absolute = ToAbsolutePath(assetFolder);
            if (!Directory.Exists(absolute))
            {
                Directory.CreateDirectory(absolute);
                AssetDatabase.Refresh();
            }
        }

        private static string FindExistingTextureByStem(string fileName)
        {
            var baseName = Path.GetFileNameWithoutExtension(fileName);
            foreach (var guid in AssetDatabase.FindAssets($"{baseName} t:Texture2D"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (
                    string.Equals(
                        Path.GetFileNameWithoutExtension(path),
                        baseName,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                    return path;
            }

            return null;
        }

        private static string FindExistingMaterialByName(string materialName)
        {
            foreach (var guid in AssetDatabase.FindAssets($"{materialName} t:Material"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (
                    string.Equals(
                        Path.GetFileNameWithoutExtension(path),
                        materialName,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                    return path;
            }

            return null;
        }

        private static string GetMaterialName(string assetId)
        {
            var parts = assetId.Split(
                new[] { '_', '-', ' ' },
                StringSplitOptions.RemoveEmptyEntries
            );
            var formatted = parts.Select(part =>
            {
                if (part.Length == 0)
                    return part;
                if (part.All(char.IsDigit))
                    return part;
                return char.ToUpperInvariant(part[0]) + part.Substring(1);
            });

            return "M_" + string.Join("_", formatted);
        }

        private static string ExtractAssetId(string urlOrId)
        {
            if (string.IsNullOrWhiteSpace(urlOrId))
                return null;

            var value = urlOrId.Trim();
            if (!value.Contains("://"))
                return SanitizeAssetId(value);

            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
                return null;

            var segments = uri.AbsolutePath.Split(
                new[] { '/' },
                StringSplitOptions.RemoveEmptyEntries
            );

            var aIndex = Array.FindIndex(
                segments,
                s => string.Equals(s, "a", StringComparison.OrdinalIgnoreCase)
            );
            if (aIndex >= 0 && aIndex + 1 < segments.Length)
                return SanitizeAssetId(segments[aIndex + 1]);

            return segments.Length > 0 ? SanitizeAssetId(segments[^1]) : null;
        }

        private static string SanitizeAssetId(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;

            var cleaned = Regex.Replace(input.Trim(), @"[^A-Za-z0-9_\-]", string.Empty);
            return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
        }

        private static string GetFileNameFromUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return null;
            return Uri.UnescapeDataString(Path.GetFileName(uri.LocalPath));
        }

        private static string ToAbsolutePath(string assetPath)
        {
            var projectRoot =
                Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new Exception("Could not resolve Unity project root.");
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }

        private void SetStatus(string text, MessageType type)
        {
            _status = text;
            _statusType = type;
            Repaint();
        }

        private string CurrentResolution =>
            _resolutions.Count == 0
                ? "1k"
                : _resolutions[Mathf.Clamp(_resolutionIndex, 0, _resolutions.Count - 1)];

        private string CurrentGroup =>
            _groups.Count == 0 ? "Ground" : _groups[Mathf.Clamp(_groupIndex, 0, _groups.Count - 1)];

        private static readonly MapType[] MapDisplayOrder =
        {
            MapType.Diffuse,
            MapType.NormalGl,
            MapType.NormalDx,
            MapType.Arm,
            MapType.AmbientOcclusion,
            MapType.Roughness,
            MapType.Metallic,
            MapType.Displacement,
            MapType.Bump,
            MapType.Specular,
            MapType.Alpha,
            MapType.Emission,
        };

        private static string GetDisplayName(MapType type)
        {
            return type switch
            {
                MapType.Diffuse => "Diffuse",
                MapType.NormalGl => "Normal (GL)",
                MapType.NormalDx => "Normal (DX)",
                MapType.Arm => "AO / Rough / Metal",
                MapType.AmbientOcclusion => "AO",
                MapType.Roughness => "Rough",
                MapType.Metallic => "Metal",
                MapType.Displacement => "Displacement",
                MapType.Bump => "Bump",
                MapType.Specular => "Spec",
                MapType.Alpha => "Alpha",
                MapType.Emission => "Emission",
                _ => type.ToString(),
            };
        }

        private static void CollectFileVariants(
            object node,
            List<string> path,
            List<FileVariant> output
        )
        {
            if (node is Dictionary<string, object> dict)
            {
                if (dict.TryGetValue("url", out var urlObj) && urlObj is string url)
                {
                    var combined = string.Join("/", path) + "/" + GetFileNameFromUrl(url);
                    var mapType = DetectMapType(combined);
                    var resolution = DetectResolution(combined);
                    var format = DetectFormat(url);

                    if (
                        mapType != MapType.Unknown
                        && !string.IsNullOrEmpty(resolution)
                        && !string.IsNullOrEmpty(format)
                    )
                    {
                        output.Add(
                            new FileVariant
                            {
                                MapType = mapType,
                                Resolution = resolution,
                                Format = format,
                                Url = url,
                            }
                        );
                    }

                    return;
                }

                foreach (var pair in dict)
                {
                    path.Add(pair.Key);
                    CollectFileVariants(pair.Value, path, output);
                    path.RemoveAt(path.Count - 1);
                }
            }
            else if (node is List<object> list)
            {
                for (var i = 0; i < list.Count; i++)
                {
                    path.Add(i.ToString(CultureInfo.InvariantCulture));
                    CollectFileVariants(list[i], path, output);
                    path.RemoveAt(path.Count - 1);
                }
            }
        }

        private static MapType DetectMapType(string combinedPath)
        {
            var s = (combinedPath ?? string.Empty).ToLowerInvariant();

            // Most specific first.
            if (ContainsToken(s, "nor_gl") || s.Contains("normal (gl)"))
                return MapType.NormalGl;
            if (ContainsToken(s, "nor_dx") || s.Contains("normal (dx)"))
                return MapType.NormalDx;
            if (
                ContainsToken(s, "arm")
                || s.Contains("ao/rough/metal")
                || s.Contains("ao_rough_metal")
            )
                return MapType.Arm;
            if (ContainsToken(s, "diff") || s.Contains("diffuse"))
                return MapType.Diffuse;
            if (ContainsToken(s, "rough") || s.Contains("roughness"))
                return MapType.Roughness;
            if (ContainsToken(s, "metal") || s.Contains("metallic") || s.Contains("metalness"))
                return MapType.Metallic;
            if (
                ContainsToken(s, "disp")
                || s.Contains("displacement")
                || ContainsToken(s, "height")
            )
                return MapType.Displacement;
            if (
                ContainsToken(s, "ao")
                || s.Contains("ambient_occlusion")
                || s.Contains("ambient occlusion")
            )
                return MapType.AmbientOcclusion;
            if (ContainsToken(s, "bump"))
                return MapType.Bump;
            if (ContainsToken(s, "spec") || s.Contains("specular"))
                return MapType.Specular;
            if (ContainsToken(s, "alpha") || s.Contains("opacity"))
                return MapType.Alpha;
            if (ContainsToken(s, "emission") || s.Contains("emissive"))
                return MapType.Emission;

            return MapType.Unknown;
        }

        private static bool ContainsToken(string value, string token)
        {
            return Regex.IsMatch(
                value,
                $@"(^|[^a-z0-9]){Regex.Escape(token)}([^a-z0-9]|$)",
                RegexOptions.IgnoreCase
            );
        }

        private static string DetectResolution(string combinedPath)
        {
            var match = Regex.Match(
                combinedPath ?? string.Empty,
                @"(^|[^a-z0-9])(\d+k)([^a-z0-9]|$)",
                RegexOptions.IgnoreCase
            );
            return match.Success ? match.Groups[2].Value.ToLowerInvariant() : null;
        }

        private static string DetectFormat(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return null;

            var ext = Path.GetExtension(uri.LocalPath)?.TrimStart('.').ToLowerInvariant();
            return ext is "jpeg" ? "jpg" : ext;
        }

        private static bool TryGetLong(Dictionary<string, object> dict, string key, out long value)
        {
            value = 0;
            if (!dict.TryGetValue(key, out var raw) || raw == null)
                return false;

            switch (raw)
            {
                case long l:
                    value = l;
                    return true;
                case int i:
                    value = i;
                    return true;
                case double d:
                    value = (long)d;
                    return true;
                default:
                    return long.TryParse(
                        raw.ToString(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out value
                    );
            }
        }

        private sealed class FileVariant
        {
            public MapType MapType;
            public string Resolution;
            public string Format;
            public string Url;
        }

        private enum MapType
        {
            Unknown,
            Diffuse,
            NormalGl,
            NormalDx,
            Arm,
            AmbientOcclusion,
            Roughness,
            Metallic,
            Displacement,
            Bump,
            Specular,
            Alpha,
            Emission,
        }

        /// <summary>
        /// Small dependency-free JSON parser for Editor tooling.
        /// It supports the JSON types returned by the Poly Haven API:
        /// objects, arrays, strings, numbers, booleans and null.
        /// </summary>
        private static class MiniJson
        {
            public static object Parse(string json)
            {
                if (json == null)
                    return null;

                var parser = new Parser(json);
                return parser.ParseValue();
            }

            private sealed class Parser
            {
                private readonly string _json;
                private int _index;

                public Parser(string json)
                {
                    _json = json;
                }

                public object ParseValue()
                {
                    SkipWhitespace();
                    if (_index >= _json.Length)
                        return null;

                    return _json[_index] switch
                    {
                        '{' => ParseObject(),
                        '[' => ParseArray(),
                        '"' => ParseString(),
                        't' => ParseLiteral("true", true),
                        'f' => ParseLiteral("false", false),
                        'n' => ParseLiteral("null", null),
                        _ => ParseNumber(),
                    };
                }

                private Dictionary<string, object> ParseObject()
                {
                    var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    _index++; // {
                    SkipWhitespace();

                    if (TryConsume('}'))
                        return result;

                    while (_index < _json.Length)
                    {
                        SkipWhitespace();
                        var key = ParseString();
                        SkipWhitespace();
                        Expect(':');
                        var value = ParseValue();
                        result[key] = value;
                        SkipWhitespace();

                        if (TryConsume('}'))
                            return result;

                        Expect(',');
                    }

                    throw new FormatException("Unexpected end of JSON object.");
                }

                private List<object> ParseArray()
                {
                    var result = new List<object>();
                    _index++; // [
                    SkipWhitespace();

                    if (TryConsume(']'))
                        return result;

                    while (_index < _json.Length)
                    {
                        result.Add(ParseValue());
                        SkipWhitespace();

                        if (TryConsume(']'))
                            return result;

                        Expect(',');
                    }

                    throw new FormatException("Unexpected end of JSON array.");
                }

                private string ParseString()
                {
                    SkipWhitespace();
                    Expect('"');
                    var sb = new StringBuilder();

                    while (_index < _json.Length)
                    {
                        var c = _json[_index++];
                        if (c == '"')
                            return sb.ToString();

                        if (c != '\\')
                        {
                            sb.Append(c);
                            continue;
                        }

                        if (_index >= _json.Length)
                            throw new FormatException("Invalid JSON escape sequence.");

                        var esc = _json[_index++];
                        switch (esc)
                        {
                            case '"':
                                sb.Append('"');
                                break;
                            case '\\':
                                sb.Append('\\');
                                break;
                            case '/':
                                sb.Append('/');
                                break;
                            case 'b':
                                sb.Append('\b');
                                break;
                            case 'f':
                                sb.Append('\f');
                                break;
                            case 'n':
                                sb.Append('\n');
                                break;
                            case 'r':
                                sb.Append('\r');
                                break;
                            case 't':
                                sb.Append('\t');
                                break;
                            case 'u':
                                if (_index + 4 > _json.Length)
                                    throw new FormatException("Invalid JSON unicode escape.");
                                var hex = _json.Substring(_index, 4);
                                if (
                                    !ushort.TryParse(
                                        hex,
                                        NumberStyles.HexNumber,
                                        CultureInfo.InvariantCulture,
                                        out var code
                                    )
                                )
                                    throw new FormatException("Invalid JSON unicode escape.");
                                sb.Append((char)code);
                                _index += 4;
                                break;
                            default:
                                throw new FormatException($"Unsupported JSON escape: \\{esc}");
                        }
                    }

                    throw new FormatException("Unexpected end of JSON string.");
                }

                private object ParseNumber()
                {
                    SkipWhitespace();
                    var start = _index;

                    while (_index < _json.Length)
                    {
                        var c = _json[_index];
                        if (char.IsDigit(c) || c is '-' or '+' or '.' or 'e' or 'E')
                            _index++;
                        else
                            break;
                    }

                    if (start == _index)
                        throw new FormatException($"Unexpected JSON token at index {_index}.");

                    var token = _json.Substring(start, _index - start);
                    if (token.IndexOfAny(new[] { '.', 'e', 'E' }) >= 0)
                    {
                        if (
                            double.TryParse(
                                token,
                                NumberStyles.Float,
                                CultureInfo.InvariantCulture,
                                out var d
                            )
                        )
                            return d;
                    }
                    else if (
                        long.TryParse(
                            token,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out var l
                        )
                    )
                    {
                        return l;
                    }

                    throw new FormatException($"Invalid JSON number: {token}");
                }

                private object ParseLiteral(string literal, object value)
                {
                    if (
                        _index + literal.Length > _json.Length
                        || !string.Equals(
                            _json.Substring(_index, literal.Length),
                            literal,
                            StringComparison.Ordinal
                        )
                    )
                    {
                        throw new FormatException($"Invalid JSON literal at index {_index}.");
                    }

                    _index += literal.Length;
                    return value;
                }

                private void SkipWhitespace()
                {
                    while (_index < _json.Length && char.IsWhiteSpace(_json[_index]))
                        _index++;
                }

                private bool TryConsume(char expected)
                {
                    SkipWhitespace();
                    if (_index < _json.Length && _json[_index] == expected)
                    {
                        _index++;
                        return true;
                    }

                    return false;
                }

                private void Expect(char expected)
                {
                    SkipWhitespace();
                    if (_index >= _json.Length || _json[_index] != expected)
                        throw new FormatException($"Expected '{expected}' at JSON index {_index}.");
                    _index++;
                }
            }
        }
    }
}
#endif
