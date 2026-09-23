using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

// An isolated, real-controller audition. No scene objects, gameplay input or saves are changed.
public sealed class FightingAnimationReview : EditorWindow
{
    private static readonly string[] MovementNames = { "Standing", "Forward", "Backward", "Strafe left", "Strafe right", "Diagonal / direction changes" };
    private static readonly Vector2[] Movements = { Vector2.zero, Vector2.up, Vector2.down, Vector2.left, Vector2.right, new Vector2(.707f,.707f) };
    private CharacterVisual character;
    private AnimationClip source;
    private Session session;
    private int movement;
    private bool playing = true, rebuild = true;
    private double previous;

    [MenuItem("Tools/Animation/Fighting Review")]
    public static void Open() => GetWindow<FightingAnimationReview>("Fighting Review");
    private void OnEnable()
    {
        character ??= AssetDatabase.LoadAssetAtPath<CharacterVisual>("Assets/Game/Prefabs/Player/Characters/Amy.prefab");
        previous = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
        rebuild = true;
    }
    private void OnDisable() { EditorApplication.update -= Tick; session?.Dispose(); session = null; }
    private void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        if (rebuild)
        {
            session?.Dispose();
            session = character != null ? new Session(character, Movements[movement], movement == 5, source) : null;
            rebuild = false;
        }
        if (playing && session != null)
        {
            float remaining = Mathf.Min((float)(now - previous), .1f);
            while (remaining > 0) { float dt = Mathf.Min(remaining, 1f/60); session.Step(dt); remaining -= dt; }
            Repaint();
        }
        previous = now;
    }
    private void OnGUI()
    {
        EditorGUI.BeginChangeCheck();
        character = (CharacterVisual)EditorGUILayout.ObjectField("Character", character, typeof(CharacterVisual), false);
        movement = EditorGUILayout.Popup("Sequence movement", movement, MovementNames);
        source = (AnimationClip)EditorGUILayout.ObjectField("Single clip (optional)", source, typeof(AnimationClip), false);
        if (EditorGUI.EndChangeCheck()) rebuild = true;
        using (new EditorGUILayout.HorizontalScope())
        {
            playing = GUILayout.Toggle(playing, "Play at 1×", "Button");
            if (GUILayout.Button("Restart")) rebuild = true;
            if (GUILayout.Button("Step 1/60 s")) { playing = false; session?.Step(1f/60); Repaint(); }
            if (GUILayout.Button("Source pack")) Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(CombatV2Audition.Folder);
        }
        EditorGUILayout.LabelField(session?.Phase ?? "Preparing preview");
        EditorGUILayout.HelpBox("In-place Animator review: exploration → guard → five deliberate attacks → guard → exploration. Drop any source clip above to audition it separately. Gameplay movement and impact handling are tested in Play Mode.", MessageType.Info);
        Rect area = GUILayoutUtility.GetRect(200, 200, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        if (Event.current.type == EventType.Repaint && session != null && area.width > 0 && area.height > 0)
            GUI.DrawTexture(area, session.Render(area), ScaleMode.ScaleToFit, false);
    }

    // Same session used by the interactive window and reproducible contact-sheet captures.
    internal sealed class Session : IDisposable
    {
        private readonly PreviewRenderUtility preview = new();
        private readonly CharacterVisual actor;
        private readonly CharacterAnimatorDriver driver;
        private readonly Vector2 movement;
        private readonly bool changes;
        private readonly SkinnedMeshRenderer[] skins;
        private readonly List<Mesh> meshes = new();
        private readonly CharacterActionId[] chain;
        private PlayableGraph graph;
        private readonly AnimationClip source;
        private float time, endTime = -1;
        private int step = -1;
        private bool entered;
        public int Cycles { get; private set; }
        public string Phase { get; private set; }
        public Animator Animator => actor.Animator;

        public Session(CharacterVisual prefab, Vector2 movement, bool changes = false, AnimationClip source = null)
        {
            this.movement = movement; this.changes = changes; this.source = source;
            actor = Object.Instantiate(prefab); preview.AddSingleGO(actor.gameObject);
            var animator = actor.Animator;
            animator.applyRootMotion = false; animator.fireEvents = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            driver = new CharacterAnimatorDriver(animator, actor.AnimationActions);
            chain = AssetDatabase.LoadAssetAtPath<UnarmedCombatItemData>(UnarmedCombatSetup.ItemPath).attackChain;
            if (source != null)
            {
                animator.runtimeAnimatorController = null;
                graph = PlayableGraph.Create("Fighting clip audition"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                AnimationPlayableOutput.Create(graph, "Pose", animator).SetSourcePlayable(AnimationClipPlayable.Create(graph, source));
                graph.Play();
            }
            skins = actor.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var skin in skins)
            {
                var mesh = new Mesh(); meshes.Add(mesh);
                var rendered = new GameObject("Preview skin", typeof(MeshFilter), typeof(MeshRenderer));
                rendered.transform.SetParent(skin.transform, false);
                rendered.GetComponent<MeshFilter>().sharedMesh = mesh;
                rendered.GetComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
                skin.enabled = false;
            }
            preview.camera.orthographic = true; preview.camera.orthographicSize = 1.4f;
            preview.camera.nearClipPlane = .01f; preview.camera.farClipPlane = 20;
            preview.camera.clearFlags = CameraClearFlags.SolidColor; preview.camera.backgroundColor = new Color(.07f,.09f,.13f);
            preview.camera.transform.position = new Vector3(3,2.3f,4);
            preview.camera.transform.LookAt(new Vector3(0,1,0));
            preview.lights[0].enabled = preview.lights[1].enabled = true;
            preview.lights[0].intensity = 1.2f; preview.lights[0].transform.rotation = Quaternion.Euler(35,-35,0);
            preview.lights[1].intensity = .8f; preview.ambientColor = Color.gray;
            Reset();
        }
        private void Reset()
        {
            time = 0; step = -1; endTime = -1; entered = false; Phase = "Exploration";
            if (source != null) { graph.GetRootPlayable(0).SetTime(0); graph.Evaluate(0); return; }
            actor.Animator.Rebind(); driver.SetCombatStance(false); actor.Animator.Update(0);
        }
        public void Step(float dt)
        {
            time += dt;
            if (source != null)
            {
                Phase = source.name + "  " + time.ToString("F2") + " s"; graph.Evaluate(dt);
                if (time > source.length + .3f) { Cycles++; Reset(); }
                return;
            }
            bool guard = time >= .5f && (endTime < 0 || time < endTime + .55f);
            driver.SetCombatStance(guard);
            Vector2 direction = changes && (int)(time/.45f)%2 != 0 ? new Vector2(-movement.y,movement.x) : movement;
            if (step >= 0 && actor.AnimationActions.TryGetBinding(chain[step], out var binding)
                && binding.requiresLegMotion && endTime < 0 && !driver.CanChainAction(chain[step])) direction = Vector2.zero;
            driver.SetMovement(direction,dt);
            if (step < 0 && time >= 1)
            { step = 0; driver.TryPlayAction(chain[step]); }
            else if (step >= 0)
            {
                entered |= driver.IsActionPlaying(chain[step]);
                if (step + 1 < chain.Length && driver.CanChainAction(chain[step]))
                { step++; entered = false; driver.TryPlayAction(chain[step]); }
                else if (step == chain.Length-1 && entered && !driver.IsActionPlaying(chain[step]) && endTime < 0) endTime = time;
            }
            Phase = step < 0 ? guard ? "Fighting stance" : "Exploration" : endTime < 0 ? chain[step].ToString() : guard ? "Recovery / guard" : "Return to exploration";
            actor.Animator.Update(dt);
            if (endTime >= 0 && time > endTime + 1.3f) { Cycles++; Reset(); }
        }
        public Texture Render(Rect rect)
        {
            for (int i=0;i<skins.Length;i++) skins[i].BakeMesh(meshes[i]);
            preview.BeginPreview(rect,GUIStyle.none); preview.Render(true); return preview.EndPreview();
        }
        public void Dispose()
        {
            if (graph.IsValid()) graph.Destroy();
            preview.Cleanup(); foreach (var mesh in meshes) Object.DestroyImmediate(mesh);
        }
    }

    public static void CaptureSequences()
    {
        Directory.CreateDirectory("Logs/CombatV2/Sequences");
        foreach (string name in new[] { "Amy", "SportyGranny" })
        foreach (int movement in new[] { 0, 1, 2, 3, 4, 5 })
        {
            using var session = new Session(AssetDatabase.LoadAssetAtPath<CharacterVisual>("Assets/Game/Prefabs/Player/Characters/"+name+".prefab"), Movements[movement], movement==5);
            // Three complete, uninterrupted controller cycles; capture the middle pass.
            var sheet = new Texture2D(2200,1620,TextureFormat.RGB24,false);
            var phases = new List<string>(); int frame=0, captured=0;
            try
            {
                for (int tick=0; session.Cycles<3 && tick<2400; tick++)
                {
                    session.Step(1f/60);
                    if (session.Cycles!=1 || frame++%6!=0 || captured>=60) continue;
                    var rendered=(RenderTexture)session.Render(new Rect(0,0,220,270));
                    var previous=RenderTexture.active; RenderTexture.active=rendered;
                    int x=captured%10*220, y=(5-captured/10)*270;
                    sheet.ReadPixels(new Rect(0,0,220,270),x,y,false); RenderTexture.active=previous;
                    if (!rendered.sRGB && QualitySettings.activeColorSpace==ColorSpace.Linear)
                    {
                        var pixels=sheet.GetPixels(x,y,220,270);
                        for(int i=0;i<pixels.Length;i++) pixels[i]=pixels[i].gamma;
                        sheet.SetPixels(x,y,220,270,pixels);
                    }
                    phases.Add(captured+": "+session.Phase); captured++;
                }
                if (session.Cycles != 3) throw new InvalidOperationException("Fighting review sequence stalled.");
                sheet.Apply(); string path="Logs/CombatV2/Sequences/"+name+"-"+MovementNames[movement].Replace(" / ","-");
                File.WriteAllBytes(path+".png",sheet.EncodeToPNG()); File.WriteAllLines(path+".txt",phases);
            }
            finally {Object.DestroyImmediate(sheet);}
        }
    }
}
