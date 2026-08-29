using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace KidsVsAliens.EditorTools.ModularLevelKit
{
    public sealed class ModularLevelKitGeneratorWindow : EditorWindow
    {
        private const string DefaultOutputFolder =
            "Assets/Game/Prefabs/Environment/GeneratedModularKit";

        [SerializeField]
        private string outputFolder = DefaultOutputFolder;

        [SerializeField]
        private float wallHeight = 2f;

        [SerializeField]
        private float wallThickness = 0.25f;

        [SerializeField]
        private float elevationHeight = 0.5f;

        [SerializeField]
        private float cornerArmLength = 1f;

        [SerializeField]
        private Material wallMaterial;

        [SerializeField]
        private Material floorMaterial;

        private Material fallbackMaterial;

        [MenuItem("Tools/Kids VS Aliens/Level Tools/Generate Starter Modular Kit")]
        public static void Open()
        {
            GetWindow<ModularLevelKitGeneratorWindow>("Modular Kit");
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Starter Modular Level Kit", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "Generates exact greybox modules with Snap_* connection points. "
                    + "V9 adds exposed-boundary selection snapping for parented chunks, loose multi-selections and 1x1 floors.",
                MessageType.Info
            );

            outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
            wallHeight = EditorGUILayout.FloatField("Wall Height", wallHeight);
            wallThickness = EditorGUILayout.FloatField("Wall Thickness", wallThickness);
            elevationHeight = EditorGUILayout.FloatField("Elevation Height", elevationHeight);
            cornerArmLength = EditorGUILayout.FloatField("Corner Arm Length", cornerArmLength);

            wallMaterial = (Material)
                EditorGUILayout.ObjectField(
                    "Optional Wall Material",
                    wallMaterial,
                    typeof(Material),
                    false
                );

            floorMaterial = (Material)
                EditorGUILayout.ObjectField(
                    "Optional Floor Material",
                    floorMaterial,
                    typeof(Material),
                    false
                );

            EditorGUILayout.Space(8);

            if (GUILayout.Button("Generate / Regenerate Starter Kit", GUILayout.Height(36)))
                Generate();
        }

        private void Generate()
        {
            if (
                wallHeight <= 0f
                || wallThickness <= 0f
                || elevationHeight <= 0f
                || cornerArmLength <= 0f
            )
            {
                EditorUtility.DisplayDialog(
                    "Invalid Modular Kit Settings",
                    "Wall height, wall thickness, elevation height and corner arm length must be greater than zero.",
                    "OK"
                );
                return;
            }

            EnsureFolder(outputFolder);
            fallbackMaterial = GetOrCreateFallbackMaterial();

            Material effectiveWallMaterial = wallMaterial != null ? wallMaterial : fallbackMaterial;
            Material effectiveFloorMaterial =
                floorMaterial != null ? floorMaterial : fallbackMaterial;

            // Straight walls.
            CreateStraightWall("Wall_1m", 1f, effectiveWallMaterial);
            CreateStraightWall("Wall_2m", 2f, effectiveWallMaterial);
            CreateStraightWall("Wall_4m", 4f, effectiveWallMaterial);
            CreateStraightWall("HalfWall_2m", 2f, effectiveWallMaterial, wallHeight * 0.5f);

            // Sharp mitered turns. A wall strip naturally contains both its inner and outer corner.
            // Left / Right only determines turn direction.
            CreateMiteredCorner("Corner_90_Left_1m", 90f, effectiveWallMaterial);
            CreateMiteredCorner("Corner_90_Right_1m", -90f, effectiveWallMaterial);
            CreateMiteredCorner("Corner_45_Left_1m", 45f, effectiveWallMaterial);
            CreateMiteredCorner("Corner_45_Right_1m", -45f, effectiveWallMaterial);

            // Flat floor family.
            CreateFloor("Floor_1x1", 1f, 1f, 0f, effectiveFloorMaterial);
            CreateFloor("Floor_2x2", 2f, 2f, 0f, effectiveFloorMaterial);
            CreateFloor("Floor_3x3", 3f, 3f, 0f, effectiveFloorMaterial);
            CreateFloor("Floor_4x4", 4f, 4f, 0f, effectiveFloorMaterial);

            // 45-degree triangular floor family.
            CreateTriangleFloor45("FloorTri_1x1_45", 1f, 0f, effectiveFloorMaterial);
            CreateTriangleFloor45("FloorTri_2x2_45", 2f, 0f, effectiveFloorMaterial);
            CreateTriangleFloor45("FloorTri_3x3_45", 3f, 0f, effectiveFloorMaterial);
            CreateTriangleFloor45("FloorTri_4x4_45", 4f, 0f, effectiveFloorMaterial);

            // Elevation starter pieces.
            CreateFloor("Platform_2x2_H0.5", 2f, 2f, elevationHeight, effectiveFloorMaterial);
            CreateStairs("Stairs_H0.5", 2f, 2f, elevationHeight, 4, effectiveFloorMaterial);
            CreateRamp("Ramp_H0.5", 2f, 2f, elevationHeight, effectiveFloorMaterial);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Modular Kit V9 Ready",
                $"Generated / updated starter prefabs in:\n{outputFolder}\n\n",
                "Nice"
            );
        }

        private void CreateStraightWall(
            string prefabName,
            float length,
            Material material,
            float? customHeight = null
        )
        {
            float height = customHeight ?? wallHeight;
            GameObject root = new GameObject(prefabName);

            GameObject visual = CreateCube(
                "Visual",
                root.transform,
                new Vector3(0f, height * 0.5f, 0f),
                new Vector3(length, height, wallThickness),
                Quaternion.identity,
                material
            );

            ApplyFadeLayerIfAvailable(visual);

            CreateSocket(
                root.transform,
                "Snap_Wall_A",
                new Vector3(-length * 0.5f, 0f, 0f),
                Quaternion.LookRotation(Vector3.left, Vector3.up)
            );

            CreateSocket(
                root.transform,
                "Snap_Wall_B",
                new Vector3(length * 0.5f, 0f, 0f),
                Quaternion.LookRotation(Vector3.right, Vector3.up)
            );

            SavePrefabAndDestroy(root);
        }

        private void CreateMiteredCorner(string prefabName, float turnDegrees, Material material)
        {
            GameObject root = new GameObject(prefabName);

            Vector2 incomingDirection = Vector2.right;
            float radians = turnDegrees * Mathf.Deg2Rad;
            Vector2 outgoingDirection = new Vector2(
                Mathf.Cos(radians),
                Mathf.Sin(radians)
            ).normalized;

            Vector2 startCenter = -incomingDirection * cornerArmLength;
            Vector2 cornerCenter = Vector2.zero;
            Vector2 endCenter = outgoingDirection * cornerArmLength;

            float half = wallThickness * 0.5f;

            Vector2 incomingLeft = LeftNormal(incomingDirection);
            Vector2 outgoingLeft = LeftNormal(outgoingDirection);

            Vector2 startLeft = startCenter + incomingLeft * half;
            Vector2 startRight = startCenter - incomingLeft * half;
            Vector2 endLeft = endCenter + outgoingLeft * half;
            Vector2 endRight = endCenter - outgoingLeft * half;

            Vector2 miterLeft = IntersectLines(
                cornerCenter + incomingLeft * half,
                incomingDirection,
                cornerCenter + outgoingLeft * half,
                outgoingDirection
            );

            Vector2 miterRight = IntersectLines(
                cornerCenter - incomingLeft * half,
                incomingDirection,
                cornerCenter - outgoingLeft * half,
                outgoingDirection
            );

            // One continuous thick wall footprint.
            // The inside and outside corner are both true sharp miter intersections.
            // No overlapping wall blocks and no separate seam in the middle.
            Vector2[] footprint = EnsureCounterClockwise(
                new[] { startLeft, miterLeft, endLeft, endRight, miterRight, startRight }
            );

            Mesh mesh = BuildExtrudedPolygonMesh(footprint, wallHeight, wallHeight);

            mesh.name = $"{prefabName}_Mesh";
            Mesh meshAsset = SaveOrReplaceMeshAsset(
                mesh,
                $"{outputFolder}/{prefabName}_Mesh.asset"
            );

            GameObject visual = CreateMeshObject(
                "Visual",
                root.transform,
                meshAsset,
                material,
                true
            );

            ApplyFadeLayerIfAvailable(visual);

            Vector3 socketAOut = new Vector3(-incomingDirection.x, 0f, -incomingDirection.y);

            Vector3 socketBOut = new Vector3(outgoingDirection.x, 0f, outgoingDirection.y);

            CreateSocket(
                root.transform,
                "Snap_Wall_A",
                new Vector3(startCenter.x, 0f, startCenter.y),
                Quaternion.LookRotation(socketAOut, Vector3.up)
            );

            CreateSocket(
                root.transform,
                "Snap_Wall_B",
                new Vector3(endCenter.x, 0f, endCenter.y),
                Quaternion.LookRotation(socketBOut, Vector3.up)
            );

            SavePrefabAndDestroy(root);
        }

        private static Vector2 LeftNormal(Vector2 direction)
        {
            return new Vector2(-direction.y, direction.x).normalized;
        }

        private static Vector2 IntersectLines(Vector2 p, Vector2 r, Vector2 q, Vector2 s)
        {
            float cross = Cross(r, s);
            if (Mathf.Abs(cross) < 0.00001f)
                return (p + q) * 0.5f;

            float t = Cross(q - p, s) / cross;
            return p + r * t;
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        private static Vector2[] EnsureCounterClockwise(Vector2[] polygon)
        {
            float area = 0f;
            for (int i = 0; i < polygon.Length; i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[(i + 1) % polygon.Length];
                area += a.x * b.y - b.x * a.y;
            }

            if (area < 0f)
                System.Array.Reverse(polygon);

            return polygon;
        }

        private void CreateFloor(
            string prefabName,
            float width,
            float depth,
            float topHeight,
            Material material
        )
        {
            GameObject root = new GameObject(prefabName);
            const float slabThickness = 0.2f;
            float centerY = topHeight - slabThickness * 0.5f;

            CreateCube(
                "Visual",
                root.transform,
                new Vector3(0f, centerY, 0f),
                new Vector3(width, slabThickness, depth),
                Quaternion.identity,
                material
            );

            CreateGroundEdgeSockets(root.transform, width, depth, topHeight);
            SavePrefabAndDestroy(root);
        }

        private void CreateTriangleFloor45(
            string prefabName,
            float size,
            float topHeight,
            Material material
        )
        {
            GameObject root = new GameObject(prefabName);
            const float slabThickness = 0.2f;

            Mesh mesh = BuildRightTrianglePrismMesh(size, topHeight, slabThickness);
            mesh.name = $"{prefabName}_Mesh";
            Mesh meshAsset = SaveOrReplaceMeshAsset(
                mesh,
                $"{outputFolder}/{prefabName}_Mesh.asset"
            );

            CreateMeshObject("Visual", root.transform, meshAsset, material, true);

            float half = size * 0.5f;

            CreateSocket(
                root.transform,
                "Snap_Ground_South",
                new Vector3(0f, topHeight, -half),
                Quaternion.Euler(0f, 180f, 0f)
            );

            CreateSocket(
                root.transform,
                "Snap_Ground_West",
                new Vector3(-half, topHeight, 0f),
                Quaternion.Euler(0f, -90f, 0f)
            );

            CreateSocket(
                root.transform,
                "Snap_Ground_Diagonal",
                new Vector3(0f, topHeight, 0f),
                Quaternion.Euler(0f, 45f, 0f)
            );

            SavePrefabAndDestroy(root);
        }

        private void CreateStairs(
            string prefabName,
            float width,
            float run,
            float rise,
            int stepCount,
            Material material
        )
        {
            GameObject root = new GameObject(prefabName);
            float stepDepth = run / stepCount;
            float stepRise = rise / stepCount;

            for (int i = 0; i < stepCount; i++)
            {
                float height = stepRise * (i + 1);
                float z = stepDepth * (i + 0.5f);

                CreateCube(
                    $"Step_{i + 1:00}",
                    root.transform,
                    new Vector3(0f, height * 0.5f, z),
                    new Vector3(width, height, stepDepth),
                    Quaternion.identity,
                    material
                );
            }

            CreateSocket(
                root.transform,
                "Snap_Ground_Bottom",
                new Vector3(0f, 0f, 0f),
                Quaternion.Euler(0f, 180f, 0f)
            );

            CreateSocket(
                root.transform,
                "Snap_Ground_Top",
                new Vector3(0f, rise, run),
                Quaternion.identity
            );

            SavePrefabAndDestroy(root);
        }

        private void CreateRamp(
            string prefabName,
            float width,
            float run,
            float rise,
            Material material
        )
        {
            GameObject root = new GameObject(prefabName);

            Mesh mesh = BuildRampMesh(width, run, rise);
            mesh.name = $"{prefabName}_Mesh";
            Mesh meshAsset = SaveOrReplaceMeshAsset(
                mesh,
                $"{outputFolder}/{prefabName}_Mesh.asset"
            );

            CreateMeshObject("Visual", root.transform, meshAsset, material, true);

            CreateSocket(
                root.transform,
                "Snap_Ground_Bottom",
                new Vector3(0f, 0f, 0f),
                Quaternion.Euler(0f, 180f, 0f)
            );

            CreateSocket(
                root.transform,
                "Snap_Ground_Top",
                new Vector3(0f, rise, run),
                Quaternion.identity
            );

            SavePrefabAndDestroy(root);
        }

        private static Mesh BuildRampMesh(float width, float run, float rise)
        {
            float halfWidth = width * 0.5f;

            Vector3 frontLeft = new Vector3(-halfWidth, 0f, 0f);
            Vector3 frontRight = new Vector3(halfWidth, 0f, 0f);
            Vector3 backLeftBottom = new Vector3(-halfWidth, 0f, run);
            Vector3 backRightBottom = new Vector3(halfWidth, 0f, run);
            Vector3 backLeftTop = new Vector3(-halfWidth, rise, run);
            Vector3 backRightTop = new Vector3(halfWidth, rise, run);

            var vertices = new List<Vector3>();
            var triangles = new List<int>();

            // Split vertices per face so RecalculateNormals produces HARD / flat edges.
            AddQuad(vertices, triangles, frontLeft, frontRight, backRightBottom, backLeftBottom); // bottom
            AddQuad(vertices, triangles, frontLeft, backLeftTop, backRightTop, frontRight); // slope/top
            AddQuad(
                vertices,
                triangles,
                backLeftBottom,
                backRightBottom,
                backRightTop,
                backLeftTop
            ); // back

            AddTriangle(vertices, triangles, frontLeft, backLeftBottom, backLeftTop); // left side
            AddTriangle(vertices, triangles, frontRight, backRightTop, backRightBottom); // right side

            Mesh mesh = new Mesh { vertices = vertices.ToArray(), triangles = triangles.ToArray() };

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildRightTrianglePrismMesh(
            float size,
            float topHeight,
            float thickness
        )
        {
            float half = size * 0.5f;

            Vector2[] polygon =
            {
                new Vector2(-half, -half),
                new Vector2(half, -half),
                new Vector2(-half, half),
            };

            return BuildExtrudedPolygonMesh(polygon, topHeight, thickness);
        }

        private static Mesh BuildExtrudedPolygonMesh(
            IReadOnlyList<Vector2> polygon,
            float topY,
            float thickness
        )
        {
            int count = polygon.Count;
            float bottomY = topY - thickness;

            var vertices = new List<Vector3>();
            var triangles = new List<int>();

            // Polygon data is kept counter-clockwise in X/Z space.
            // Mapping 2D (x,y) -> Unity (x,z) flips face winding, so the top face
            // must use reversed triangle order to face +Y.
            List<int> faceTriangles = TriangulatePolygon(polygon);

            var top = new Vector3[count];
            var bottom = new Vector3[count];

            for (int i = 0; i < count; i++)
            {
                top[i] = new Vector3(polygon[i].x, topY, polygon[i].y);
                bottom[i] = new Vector3(polygon[i].x, bottomY, polygon[i].y);
            }

            // Top: reverse each 2D triangle so normals point upward.
            for (int i = 0; i < faceTriangles.Count; i += 3)
            {
                int a = faceTriangles[i];
                int b = faceTriangles[i + 1];
                int c = faceTriangles[i + 2];
                AddTriangle(vertices, triangles, top[a], top[c], top[b]);
            }

            // Bottom: original 2D winding points downward in Unity X/Z.
            for (int i = 0; i < faceTriangles.Count; i += 3)
            {
                int a = faceTriangles[i];
                int b = faceTriangles[i + 1];
                int c = faceTriangles[i + 2];
                AddTriangle(vertices, triangles, bottom[a], bottom[b], bottom[c]);
            }

            // Side faces. Split vertices per face to keep hard construction edges.
            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;

                Vector3 topA = top[i];
                Vector3 topB = top[next];
                Vector3 bottomA = bottom[i];
                Vector3 bottomB = bottom[next];

                // This winding points away from a CCW polygon footprint.
                AddQuad(vertices, triangles, topA, topB, bottomB, bottomA);
            }

            Mesh mesh = new Mesh { vertices = vertices.ToArray(), triangles = triangles.ToArray() };

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static List<int> TriangulatePolygon(IReadOnlyList<Vector2> polygon)
        {
            var result = new List<int>();

            if (polygon == null || polygon.Count < 3)
                return result;

            var remaining = new List<int>(polygon.Count);
            for (int i = 0; i < polygon.Count; i++)
                remaining.Add(i);

            const float epsilon = 0.000001f;
            int guard = 0;
            int maxIterations = polygon.Count * polygon.Count;

            while (remaining.Count > 3 && guard++ < maxIterations)
            {
                bool clippedEar = false;

                for (int i = 0; i < remaining.Count; i++)
                {
                    int previousIndex = remaining[(i - 1 + remaining.Count) % remaining.Count];
                    int currentIndex = remaining[i];
                    int nextIndex = remaining[(i + 1) % remaining.Count];

                    Vector2 a = polygon[previousIndex];
                    Vector2 b = polygon[currentIndex];
                    Vector2 c = polygon[nextIndex];

                    // For a CCW polygon, a valid convex ear must turn left.
                    if (Cross(b - a, c - b) <= epsilon)
                        continue;

                    bool containsOtherPoint = false;

                    for (int j = 0; j < remaining.Count; j++)
                    {
                        int testIndex = remaining[j];

                        if (
                            testIndex == previousIndex
                            || testIndex == currentIndex
                            || testIndex == nextIndex
                        )
                        {
                            continue;
                        }

                        if (PointInsideTriangle(polygon[testIndex], a, b, c, epsilon))
                        {
                            containsOtherPoint = true;
                            break;
                        }
                    }

                    if (containsOtherPoint)
                        continue;

                    result.Add(previousIndex);
                    result.Add(currentIndex);
                    result.Add(nextIndex);

                    remaining.RemoveAt(i);
                    clippedEar = true;
                    break;
                }

                if (!clippedEar)
                {
                    Debug.LogError(
                        "Modular Level Kit: failed to triangulate a generated polygon. "
                            + "The polygon may be self-intersecting."
                    );
                    break;
                }
            }

            if (remaining.Count == 3)
            {
                result.Add(remaining[0]);
                result.Add(remaining[1]);
                result.Add(remaining[2]);
            }

            return result;
        }

        private static bool PointInsideTriangle(
            Vector2 point,
            Vector2 a,
            Vector2 b,
            Vector2 c,
            float epsilon
        )
        {
            float ab = Cross(b - a, point - a);
            float bc = Cross(c - b, point - b);
            float ca = Cross(a - c, point - c);

            return ab >= -epsilon && bc >= -epsilon && ca >= -epsilon;
        }

        private static void AddQuad(
            List<Vector3> vertices,
            List<int> triangles,
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector3 d
        )
        {
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);

            triangles.Add(start + 0);
            triangles.Add(start + 1);
            triangles.Add(start + 2);

            triangles.Add(start + 0);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        private static void AddTriangle(
            List<Vector3> vertices,
            List<int> triangles,
            Vector3 a,
            Vector3 b,
            Vector3 c
        )
        {
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);

            triangles.Add(start + 0);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
        }

        private static GameObject CreateCube(
            string name,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Quaternion localRotation,
            Material material
        )
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPosition;
            cube.transform.localRotation = localRotation;
            cube.transform.localScale = localScale;

            if (material != null)
                cube.GetComponent<MeshRenderer>().sharedMaterial = material;

            return cube;
        }

        private static GameObject CreateMeshObject(
            string name,
            Transform parent,
            Mesh mesh,
            Material material,
            bool addCollider
        )
        {
            GameObject gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);

            var filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            var renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;

            if (addCollider)
            {
                var collider = gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
            }

            return gameObject;
        }

        private Material GetOrCreateFallbackMaterial()
        {
            string materialPath = $"{outputFolder}/M_GreyboxFallback.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (existing != null)
                return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            if (shader == null)
                throw new System.InvalidOperationException(
                    "Could not find a usable Lit shader for the greybox fallback material."
                );

            Material material = new Material(shader) { name = "M_GreyboxFallback" };

            Color grey = new Color(0.55f, 0.56f, 0.58f, 1f);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", grey);
            else if (material.HasProperty("_Color"))
                material.SetColor("_Color", grey);

            AssetDatabase.CreateAsset(material, materialPath);
            return material;
        }

        private static void CreateSocket(
            Transform parent,
            string name,
            Vector3 localPosition,
            Quaternion localRotation
        )
        {
            GameObject socket = new GameObject(name);
            socket.transform.SetParent(parent, false);
            socket.transform.localPosition = localPosition;
            socket.transform.localRotation = localRotation;
            socket.transform.localScale = Vector3.one;
        }

        private static void CreateGroundEdgeSockets(
            Transform root,
            float width,
            float depth,
            float topHeight
        )
        {
            // Primary center sockets stay exactly where they were.
            // Additional target slots are generated every 0.5 m, but only far enough
            // to place the smallest current floor module (2 m) fully on the edge.
            //
            // Example: a 4 m edge gets:
            // -1.0, -0.5, 0, +0.5, +1.0
            //
            // Fast Snap uses ONLY the moving module's primary socket and can target
            // any of these slots. So a 2x2 floor can be roughly placed near the left
            // or right half of a 4x4 floor and then snapped exactly with one click.

            CreateGroundEdgeSocketLine(
                root,
                "North",
                new Vector3(0f, topHeight, depth * 0.5f),
                Vector3.right,
                width,
                Quaternion.identity
            );

            CreateGroundEdgeSocketLine(
                root,
                "South",
                new Vector3(0f, topHeight, -depth * 0.5f),
                Vector3.right,
                width,
                Quaternion.Euler(0f, 180f, 0f)
            );

            CreateGroundEdgeSocketLine(
                root,
                "East",
                new Vector3(width * 0.5f, topHeight, 0f),
                Vector3.forward,
                depth,
                Quaternion.Euler(0f, 90f, 0f)
            );

            CreateGroundEdgeSocketLine(
                root,
                "West",
                new Vector3(-width * 0.5f, topHeight, 0f),
                Vector3.forward,
                depth,
                Quaternion.Euler(0f, -90f, 0f)
            );
        }

        private static void CreateGroundEdgeSocketLine(
            Transform root,
            string edgeName,
            Vector3 center,
            Vector3 localTangent,
            float edgeLength,
            Quaternion localRotation
        )
        {
            const float smallestFloorModule = 1f;
            const float slotStep = 0.5f;

            // Primary moving socket.
            CreateSocket(root, $"Snap_Ground_{edgeName}", center, localRotation);

            float maxOffset = Mathf.Max(0f, (edgeLength - smallestFloorModule) * 0.5f);

            for (float offset = slotStep; offset <= maxOffset + 0.0001f; offset += slotStep)
            {
                CreateSocket(
                    root,
                    $"Snap_Ground_{edgeName}_Slot_P{FormatOffset(offset)}",
                    center + localTangent * offset,
                    localRotation
                );

                CreateSocket(
                    root,
                    $"Snap_Ground_{edgeName}_Slot_M{FormatOffset(offset)}",
                    center - localTangent * offset,
                    localRotation
                );
            }
        }

        private static string FormatOffset(float value)
        {
            return Mathf.RoundToInt(value * 100f).ToString("000");
        }

        private static void ApplyFadeLayerIfAvailable(GameObject gameObject)
        {
            int layer = LayerMask.NameToLayer("FadeWhenBlockingPlayer");
            if (layer >= 0)
            {
                gameObject.layer = layer;
            }
            else
            {
                Debug.LogWarning(
                    "Modular Kit Generator: layer 'FadeWhenBlockingPlayer' was not found. "
                        + "Generated wall visuals were left on Default."
                );
            }
        }

        private Mesh SaveOrReplaceMeshAsset(Mesh generatedMesh, string assetPath)
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);

            if (existing == null)
            {
                AssetDatabase.CreateAsset(generatedMesh, assetPath);
                return generatedMesh;
            }

            EditorUtility.CopySerialized(generatedMesh, existing);
            Object.DestroyImmediate(generatedMesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private void SavePrefabAndDestroy(GameObject root)
        {
            string path = $"{outputFolder}/{root.name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        private static void EnsureFolder(string path)
        {
            string normalized = path.Replace("\\", "/").TrimEnd('/');
            if (AssetDatabase.IsValidFolder(normalized))
                return;

            string[] parts = normalized.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
                throw new IOException("Output folder must be inside Assets.");

            string current = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
