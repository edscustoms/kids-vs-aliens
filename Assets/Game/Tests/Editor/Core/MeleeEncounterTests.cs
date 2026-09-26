using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using StarterAssets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

// Disposable Unity encounter lab. Production scenes, assets and user saves are never saved.
public sealed class MeleeEncounterTests
{
    const string Key="MeleeEncounterTests";
    static readonly Vector3 Home=new Vector3(1000,.05f,1000);
    PlayerMeleeController player; PlayerHealth health; StarterAssetsInputs input;
    CharacterController capsule; Animator animator; Camera camera;
    UnarmedCombatItemData item; NavMeshDataInstance navigation;
    readonly List<string> rows=new List<string>();
    readonly List<GameObject> enemies=new List<GameObject>();
    string directory, label; float began; bool marker; int requested, impacts;
    CharacterActionId action;
    float attributedDamage, observedDamage, previousHealth, playerDamage;
    readonly List<string> contactViolations=new List<string>();
    static readonly HumanBodyBones[] Limbs={HumanBodyBones.LeftHand,HumanBodyBones.RightHand,HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot,HumanBodyBones.LeftMiddleProximal,HumanBodyBones.RightMiddleProximal,HumanBodyBones.RightToes};

    [UnityTearDown] public IEnumerator Teardown()
    {
        if (navigation.valid) navigation.Remove();
        if (item != null && !AssetDatabase.Contains(item)) Object.DestroyImmediate(item);
        if(EditorApplication.isPlaying) yield return new ExitPlayMode();
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY",SessionState.GetString(Key,"") is string s && s.Length>0?s:null);
        Time.timeScale = SessionState.GetFloat(Key + ".TimeScale", 1);
        SessionState.EraseFloat(Key + ".TimeScale");
    }
    [UnityTest, Timeout(600000)] public IEnumerator Baseline() { return Run(false); }
    [UnityTest, Timeout(600000)] public IEnumerator Refined() { return Run(true); }
    IEnumerator Run(bool refined)
    {
        directory="Logs/CombatPrecision/"+(refined?"refined":"baseline"); Directory.CreateDirectory(directory);
        SessionState.SetString(Key,Environment.GetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY")??"");
        SessionState.SetFloat(Key + ".TimeScale", Time.timeScale);
        Time.timeScale = 1; // The encounter owns its clock, not the open Editor's pause state.
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY",Path.GetFullPath(directory+"/Save-"+Guid.NewGuid().ToString("N")));
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        yield return new EnterPlayMode(); Application.runInBackground=true;
        directory="Logs/CombatPrecision/"+(refined?"refined":"baseline");
        EditorSceneManager.LoadSceneInPlayMode("Assets/Game/Scenes/ConstructionSite.unity",new LoadSceneParameters(LoadSceneMode.Single));
        yield return EditorTestFrame.Next(); yield return EditorTestFrame.Next();
        Object.FindAnyObjectByType<BeamTransportController>().CancelTransport();
        var run=ActiveRunController.Instance; run.SendMessage("OnApplicationPause",false); run.SendMessage("OnApplicationFocus",true);
        Object.FindAnyObjectByType<InGameMenuController>().ResumeGame();
        player=Object.FindAnyObjectByType<PlayerMeleeController>();
        playerDamage = new SerializedObject(player).FindProperty("damage").floatValue;
        health=player.GetComponent<PlayerHealth>(); input=player.GetComponent<StarterAssetsInputs>(); capsule=player.GetComponent<CharacterController>();
        previousHealth=health.CurrentHealth+health.CurrentArmor;
        health.OnHealthChanged+=()=>{float now=health.CurrentHealth+health.CurrentArmor;observedDamage+=Mathf.Max(0,previousHealth-now);previousHealth=now;};
        Set(run,"ready",false); // No snapshots of test-only items/positions.
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name="Disposable melee lab";
        floor.transform.position=Home+Vector3.down*.55f; floor.transform.localScale=new Vector3(40,1,40);
        var settings=NavMesh.GetSettingsByIndex(0);
        var sources=new List<NavMeshBuildSource>{new NavMeshBuildSource{shape=NavMeshBuildSourceShape.Box,transform=floor.transform.localToWorldMatrix,size=Vector3.one,area=0}};
        var data=NavMeshBuilder.BuildNavMeshData(settings,sources,new Bounds(Vector3.zero,new Vector3(40,5,40)),Home,Quaternion.identity);
        Assert.That(data,Is.Not.Null); navigation=NavMesh.AddNavMeshData(data);
        item=Object.Instantiate(AssetDatabase.LoadAssetAtPath<UnarmedCombatItemData>(UnarmedCombatSetup.ItemPath));
        player.GetComponent<PlayerSkillState>().UnlockSkill(item.requiredSkill);
        yield return EditorTestFrame.Next();
        var tutorial=Object.FindAnyObjectByType<KnowledgeAcquiredPresenter>(); if(tutorial.CurrentSkill!=null)tutorial.Close();
        yield return EditorTestFrame.Next(); yield return EditorTestFrame.Next();
        player.GetComponent<PlayerEquipment>().UnequipWeapon(); PlacePlayer(Home);
        animator=player.GetComponent<PlayerCharacter>().ActiveVisual.Animator;
        animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        camera=new GameObject("Melee review camera").AddComponent<Camera>(); camera.enabled=false;
        camera.transform.position=Home+new Vector3(3,2.3f,-3); camera.transform.LookAt(Home+new Vector3(0,.8f,.45f));
        camera.fieldOfView=40; camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.07f,.09f,.13f);
        var keyLight=new GameObject("Review light").AddComponent<Light>();keyLight.type=LightType.Directional;keyLight.intensity=2;keyLight.transform.rotation=Quaternion.Euler(45,-35,0);
        player.AttackRequested+=a=>{requested++;action=a;};
        player.GetComponent<PlayerAnimation>().AnimationEventReceived+=m=>{if(m==CharacterAnimationEventId.MeleeImpact){marker=true;impacts++;}};
        rows.Add("case,time,kind,action,damage,rootDistance,leftHandGap,rightHandGap,leftFootGap,rightFootGap,leftKnuckleGap,rightKnuckleGap,rightToesGap,enemyState,enemyId");
        // Freeze only automatic facing in the controlled matrix. Native movement and attack animation remain active.
        player.GetComponent<PlayerAim>().enabled=false;
        var chain=(CharacterActionId[])item.attackChain.Clone();
        foreach(var strike in chain)
        foreach(string scenario in new[]{"hit","close","miss","withdraw","edge","turn","strafe","behind","wall","cancel","targetdeath"})
        {
            ClearEnemies(); PlacePlayer(Home); input.MoveInput(Vector2.zero); player.CancelCombat();
            item.attackChain=new[]{strike}; player.SelectCombatItem(item);
            var enemy=Spawn(false,Home+Vector3.forward*(scenario=="miss"?2.3f:scenario=="edge"?1.45f:scenario=="close"?.7f:.8f));
            if(scenario=="behind")enemy.transform.position=Home-Vector3.forward*.8f;
            var eh=enemy.GetComponent<EnemyHealth>();
            int damageEvents=0;eh.OnDamaged+=()=>damageEvents++;
            GameObject wall=null;
            if(scenario=="wall"){wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=Home+new Vector3(0,.9f,.4f);wall.transform.localScale=new Vector3(2,2,.08f);}
            yield return Seconds(.5f);
            label="player-"+strike+"-"+scenario; began=Time.time; marker=false;
            float before=eh.CurrentHealth; Press(); bool changed=false; int shot=0;
            while(Time.time-began<1.25f)
            {
                float t=Time.time-began;
                if(!changed&&t>.10f){changed=true;if(scenario=="withdraw")enemy.transform.position+=Vector3.forward*2;
                    if(scenario=="turn")player.transform.rotation=Quaternion.Euler(0,100,0);
                    if(scenario=="strafe")input.MoveInput(Vector2.left);
                    if(scenario=="cancel")player.CancelCombat();
                    if(scenario=="targetdeath")eh.TakeDamage(10000);}
                if(marker){Record("marker",before-eh.CurrentHealth,enemy,animator,enemy.GetComponent<Collider>());if(scenario=="hit"||scenario=="edge"||scenario=="close")Capture(label+"-contact");marker=false;}
                Record("sample",before-eh.CurrentHealth,enemy,animator,enemy.GetComponent<Collider>());
                if(scenario=="hit" && t>=.12f+shot*.15f && shot<6){Capture(label+"-"+shot++);Record("pose",before-eh.CurrentHealth,enemy,animator,enemy.GetComponent<Collider>());}
                yield return NextCombatFrame();
            }
            Record("result",before-eh.CurrentHealth,enemy,animator,enemy.GetComponent<Collider>());
            if(refined && (scenario=="miss"||scenario=="withdraw"||scenario=="behind"||scenario=="turn"))Assert.That(eh.CurrentHealth,Is.EqualTo(before),label);
            if(refined && scenario=="edge")Assert.That(eh.CurrentHealth,Is.EqualTo(before),label);
            if(refined && scenario=="close")Assert.That(eh.CurrentHealth,Is.EqualTo(before-playerDamage),label);
            Assert.That(damageEvents,Is.LessThanOrEqualTo(1),"One strike cannot hit twice");
            if(refined && (scenario=="wall"||scenario=="cancel"||scenario=="targetdeath"))Assert.That(damageEvents,Is.Zero,label);
            if(wall!=null)Object.DestroyImmediate(wall);
            Flush();
        }
        input.MoveInput(Vector2.zero);player.CancelCombat();
        foreach(string scenario in new[]{"hit","withdraw","slight","misaligned","air","interrupt","reaction","turn","stun","disable","death","wall"})
        for(int repeat=0;repeat<3;repeat++)
        {
            ClearEnemies();PlacePlayer(Home);health.RestoreRunHealth(100,50);
            var enemy=Spawn(false,Home+Vector3.forward*(scenario=="air"?2:.75f));
            enemy.transform.rotation=Quaternion.Euler(0,scenario=="misaligned"?90:scenario=="slight"?160:180,0);
            yield return Seconds(.4f);label="enemy-"+scenario+"-"+repeat;began=Time.time;
            var attack=enemy.GetComponent<EnemyMeleeAttack>();var ea=enemy.GetComponentInChildren<Animator>();
            GameObject wall=null;
            if(scenario=="wall"){wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=Home+new Vector3(0,.9f,.4f);wall.transform.localScale=new Vector3(2,2,.08f);}
            if(scenario=="reaction")React(enemy);
            float before=health.CurrentHealth+health.CurrentArmor;
            bool accepted=attack.TryAttack(player.transform);
            Record(accepted?"request-accepted":"request-rejected",before-health.CurrentHealth-health.CurrentArmor,enemy,ea,capsule);
            bool changed=false;int shot=0;
            while(Time.time-began<1.3f){
                float t=Time.time-began;
                if(!changed&&t>.06f){changed=true;if(scenario=="withdraw")PlacePlayer(Home-Vector3.forward*2);if(scenario=="interrupt")React(enemy);
                    if(scenario=="turn")enemy.transform.rotation=Quaternion.identity;
                    if(scenario=="stun")enemy.GetComponent<EnemyStunReceiver>().ApplyStun(1,player.gameObject);
                    if(scenario=="disable")attack.enabled=false;
                    if(scenario=="death")enemy.GetComponent<EnemyHealth>().TakeDamage(10000);}
                Record("sample",before-health.CurrentHealth-health.CurrentArmor,enemy,ea,capsule);
                if(repeat==0&&scenario=="hit"&&t>=shot*.10f&&shot<12)Capture(label+"-"+shot++);
                yield return NextCombatFrame();
            }
            if(refined&&scenario!="hit"&&scenario!="slight")Assert.That(health.CurrentHealth+health.CurrentArmor,Is.EqualTo(before),label);
            if(refined&&scenario=="hit")Assert.That(health.CurrentHealth+health.CurrentArmor,Is.LessThan(before),label);
            if(wall!=null)Object.DestroyImmediate(wall);
            if(refined&&scenario=="interrupt")
            {
                Assert.That(attack.TryAttack(player.transform),Is.True,"Fresh attack must recover after Hit");
                yield return Seconds(1.3f);
                Assert.That(health.CurrentHealth+health.CurrentArmor,Is.EqualTo(before-10),"Only the NEW attack can damage");
            }
            Flush();
        }
        item.attackChain=chain;
        // Swap living targets across the strike line, then kill one during the same chain.
        ClearEnemies();PlacePlayer(Home);player.CancelCombat();player.SelectCombatItem(item);
        var first=Spawn(false,Home+Vector3.forward*.7f);var second=Spawn(false,Home+Vector3.right*1.5f);
        yield return Seconds(.5f);label="combo-target-switch";began=Time.time;int comboStart=requested,comboMarkers=impacts;
        bool switched=false,killed=false;float pressAt=0;
        while(Time.time-began<3.5f)
        {
            float t=Time.time-began;
            if(t>=pressAt&&requested-comboStart<5){Press();pressAt=t+.2f;}
            if(!switched&&requested-comboStart>=2){switched=true;first.transform.position=Home+Vector3.left*1.5f;second.transform.position=Home+Vector3.forward*.7f;Physics.SyncTransforms();}
            if(!killed&&requested-comboStart>=3){killed=true;first.GetComponent<EnemyHealth>().TakeDamage(10000);}
            yield return NextCombatFrame();
        }
        Assert.That(requested-comboStart,Is.EqualTo(5));Assert.That(impacts-comboMarkers,Is.EqualTo(5),"Target change/death must not corrupt the five-step chain");
        Flush();player.GetComponent<PlayerAim>().enabled=true;
        for(int count=1;count<=3;count++)for(int repeat=0;repeat<3;repeat++)
        {
            ClearEnemies();PlacePlayer(Home);health.RestoreRunHealth(100,50);player.SelectCombatItem(item);
            for(int i=0;i<count;i++)Spawn(true,Home+Quaternion.Euler(0,(i-(count-1)*.5f)*55,0)*Vector3.forward*2);
            label="encounter-"+count+"-"+repeat;began=Time.time;float nextPress=0,nextShot=0;int firstRequests=requested,firstImpacts=impacts;
            while(Time.time-began<10){float t=Time.time-began;
                if(health.CurrentHealth<50)health.RestoreRunHealth(100,50);
                input.MoveInput(t<4?Vector2.zero:t<6?Vector2.left:t<7?Vector2.down:Vector2.up);
                if(t>=nextPress){Press();nextPress=t+.25f;}
                foreach(var e in enemies)Record("fight",health.CurrentHealth+health.CurrentArmor,e,e.GetComponentInChildren<Animator>(),capsule);
                if(t>=nextShot){camera.transform.position=player.transform.position+new Vector3(3,2.3f,-3);camera.transform.LookAt(player.transform.position+Vector3.up*.8f);Capture(label+"-"+((int)(nextShot*2)));nextShot+=.5f;}
                yield return NextCombatFrame();
            }
            Debug.Log($"MELEE ENCOUNTER {count}/{repeat}: {requested-firstRequests} requests, {impacts-firstImpacts} markers");Flush();
        }
        input.MoveInput(Vector2.zero);navigation.Remove();Flush();
        if(refined){Assert.That(contactViolations,Is.Empty);Assert.That(observedDamage,Is.EqualTo(attributedDamage),"Every point of enemy damage must have an identifiable valid contact");}
    }
    GameObject Spawn(bool live,Vector3 position)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Enemies/PF_Enemy_Melee_POC_V1.prefab");
        var e=Object.Instantiate(prefab,position,Quaternion.Euler(0,180,0));enemies.Add(e);
        e.GetComponentInChildren<Animator>().cullingMode=AnimatorCullingMode.AlwaysAnimate;
        e.GetComponent<EnemyMeleeAttack>().ContactResolved+=hit=>
        {
            attributedDamage+=hit.Damage;Record("enemy-contact",hit.Damage,e,e.GetComponentInChildren<Animator>(),capsule);
            var knuckle=e.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.RightMiddleProximal);
            if(Vector3.Distance(knuckle.position,capsule.ClosestPoint(knuckle.position))>.081f)contactViolations.Add(label+": contact outside fist");
            if((e.GetComponent<EnemyMotor>().MovementLocks&~EnemyMovementLockReason.MeleeAttack)!=0)contactViolations.Add(label+": damage during reaction/stun");
        };
        e.GetComponent<EnemyBrain>().enabled=live;
        if(!live)e.GetComponent<NavMeshAgent>().enabled=false;
        Set(e.GetComponent<EnemyHealth>(),"maxHealth",1000f);e.GetComponent<EnemyHealth>().RestoreRunHealth(1000);
        e.GetComponent<EnemyHealth>().OnDamaged+=()=>
        {
            Record("player-contact",playerDamage,e,animator,e.GetComponent<Collider>());
            var visual=player.GetComponent<PlayerCharacter>().ActiveVisual;
            if(!visual.AnimationActions.TryGetBinding(action,out var binding)
                || !binding.meleeContact.TryGetCenter(animator,out var center)
                || !binding.meleeContact.Touches(e.GetComponent<Collider>(),center,out _))contactViolations.Add(label+": player damage without limb contact");
        };
        return e;
    }
    void ClearEnemies(){foreach(var e in enemies)Object.DestroyImmediate(e);enemies.Clear();}
    void PlacePlayer(Vector3 p){capsule.enabled=false;player.transform.SetPositionAndRotation(p,Quaternion.identity);capsule.enabled=true;Physics.SyncTransforms();}
    void Press(){input.ShootInput(false);input.ShootInput(true);input.ShootInput(false);}
    static void React(GameObject e)=>e.GetComponent<EnemyHitReaction>().ReceiveHit(new HitInfo(1,e.transform.position,Vector3.back,Vector3.forward,null));
    static void Set(object target,string field,object value)=>target.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(target,value);
    static IEnumerator Seconds(float time){float until=Time.time+time;while(Time.time<until)yield return NextCombatFrame();}
    static IEnumerator NextCombatFrame()
    {
        // A scaled-time loop must fail and run teardown if a modal/focus loss pauses
        // the lab; NUnit's overall timeout is not a safe coroutine cancellation path.
        Assert.That(EditorApplication.isPaused, Is.False, "Encounter fixture was paused in the Editor.");
        Assert.That(Time.timeScale, Is.GreaterThan(0), "Encounter fixture acquired an unexpected pause; refusing an unbounded scaled-time wait.");
        yield return EditorTestFrame.Next();
    }
    void Record(string kind,float damage,GameObject enemy,Animator source,Collider target)
    {
        string distances="";foreach(var bone in Limbs){var b=source.GetBoneTransform(bone);distances+=","+(b==null?-1:Vector3.Distance(b.position,target.ClosestPoint(b.position))).ToString("F3",System.Globalization.CultureInfo.InvariantCulture);}
        var state=enemy.GetComponentInChildren<Animator>().GetCurrentAnimatorStateInfo(0);
        rows.Add($"{label},{(Time.time-began).ToString("F3",System.Globalization.CultureInfo.InvariantCulture)},{kind},{action},{damage},{Vector3.Distance(player.transform.position,enemy.transform.position):F3}{distances},{state.shortNameHash}:{state.normalizedTime:F3},{enemy.GetEntityId()}");
    }
    void Flush()=>File.WriteAllLines(directory+"/trace.csv",rows);
    void Capture(string name)
    {
        var rt=new RenderTexture(480,360,24);rt.Create();var old=RenderTexture.active;
        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;
        var tex=new Texture2D(480,360,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,480,360),0,0);tex.Apply();
        File.WriteAllBytes(directory+"/"+name+".png",tex.EncodeToPNG());RenderTexture.active=old;Object.DestroyImmediate(tex);rt.Release();Object.DestroyImmediate(rt);
    }
}

