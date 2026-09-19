using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using StarterAssets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

[TestFixture, Category("Core")]
public sealed class FightingPlayModeTests
{
    private const string FixtureKey = "KVA.FightingPlayModeTests.SaveDirectory";
    [UnityTearDown]
    public IEnumerator RestoreFixture()
    {
        if (!SessionState.GetBool(FixtureKey + ".Active", false)) yield break;
        // Restore only after scene teardown, including an assertion failure. SessionState
        // survives the domain reload caused by entering/exiting Play Mode.
        if (EditorApplication.isPlaying) yield return new ExitPlayMode();
        string previous = SessionState.GetString(FixtureKey, string.Empty);
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY", string.IsNullOrEmpty(previous) ? null : previous);
        SessionState.EraseBool(FixtureKey + ".Active");
        SessionState.EraseString(FixtureKey);
    }

    [UnityTest]
    public IEnumerator RealSceneInput_CompleteChains_Movement_KickPlant_AndCancellation()
    {
        string previousSaves=Environment.GetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY");
        SessionState.SetString(FixtureKey, previousSaves ?? string.Empty);
        SessionState.SetBool(FixtureKey + ".Active", true);
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY",Path.GetFullPath("Logs/FightingFixture-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        yield return new EnterPlayMode();
        {
            EditorSceneManager.LoadSceneInPlayMode("Assets/Game/Scenes/ConstructionSite.unity",new LoadSceneParameters(LoadSceneMode.Single));
            yield return EditorTestFrame.Next(); yield return EditorTestFrame.Next();
            var melee=Object.FindAnyObjectByType<PlayerMeleeController>(); Assert.That(melee,Is.Not.Null);
            var character=melee.GetComponent<PlayerCharacter>(); var input=melee.GetComponent<StarterAssetsInputs>();
            var capsule=melee.GetComponent<CharacterController>();
            Object.FindAnyObjectByType<BeamTransportController>().CancelTransport();
            var run=Object.FindAnyObjectByType<ActiveRunController>();
            typeof(ActiveRunController).GetMethod("OnApplicationFocus",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(run,new object[]{true});
            Object.FindAnyObjectByType<InGameMenuController>().ResumeGame();
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name="Disposable combat review lane";
            floor.transform.position=new Vector3(1000,-.5f,1000); floor.transform.localScale=new Vector3(100,1,100);
            capsule.enabled=false; melee.transform.position=new Vector3(1000,.1f,1000); capsule.enabled=true;
            var item=AssetDatabase.LoadAssetAtPath<UnarmedCombatItemData>(UnarmedCombatSetup.ItemPath);
            melee.GetComponent<PlayerSkillState>().UnlockSkill(item.requiredSkill);
            yield return EditorTestFrame.Next();
            var presenter=Object.FindAnyObjectByType<KnowledgeAcquiredPresenter>();
            double presentationDeadline=EditorApplication.timeSinceStartup+10;
            while(presenter.CurrentSkill==null && presenter.PendingCount>0 && EditorApplication.timeSinceStartup<presentationDeadline)
                yield return EditorTestFrame.Next();
            presenter.Close();
            yield return EditorTestFrame.Next(); yield return EditorTestFrame.Next();
            var requests=new List<CharacterActionId>(); int impacts=0;
            melee.AttackRequested+=requests.Add;
            melee.GetComponent<PlayerAnimation>().AnimationEventReceived+=m=> {if(m==CharacterAnimationEventId.MeleeImpact)impacts++;};
            Assert.That(Time.timeScale,Is.GreaterThan(0),"Fixture must resume after Knowledge acknowledgement.");
            Debug.Log("Fighting review: native scene ready.");
            var report=new List<string>();
            for(int scenario=0;scenario<8;scenario++)
            {
                if(scenario==0||scenario==4||scenario==6)
                {
                    character.SetCharacter(AssetDatabase.LoadAssetAtPath<CharacterVisual>("Assets/Game/Prefabs/Player/Characters/"+(scenario==4?"SportyGranny":"Amy")+".prefab"));
                    yield return EditorTestFrame.Next();
                }
                if(scenario==6)
                {
                    // The isolated alternate-action item is not production catalog content.
                    // End fixture snapshots before introducing it; persistence is not under test.
                    typeof(ActiveRunController).GetField("ready",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(run,false);
                    item=Object.Instantiate(item); item.attackChain=new[]{CharacterActionId.HeavyKick};
                }
                Debug.Log("Fighting review: scenario "+scenario);
                Vector2 direction=(scenario%4) switch {1=>Vector2.left,2=>Vector2.up,3=>new Vector2(.707f,-.707f),_=>Vector2.zero};
                input.MoveInput(Vector2.zero); yield return EditorTestFrame.Next(); input.MoveInput(direction);
                float elapsed=0;
                while(elapsed<.5f) {elapsed+=Time.deltaTime; yield return EditorTestFrame.Next();}
                Assert.That(melee.SelectCombatItem(item),Is.True);
                elapsed=0; while(elapsed<.4f) {elapsed+=Time.deltaTime; yield return EditorTestFrame.Next();}
                requests.Clear(); impacts=0;
                Press(input); Assert.That(requests.Count,Is.EqualTo(1),"First FIRE must start immediately.");
                for(int step=0;step<item.attackChain.Length;step++)
                {
                    Assert.That(requests[step],Is.EqualTo(item.attackChain[step]));
                    bool queued=false, checkedPlant=false;
                    float began=Time.time, deadline=Time.realtimeSinceStartup+8;
                    while((step+1<item.attackChain.Length ? requests.Count<=step+1 : impacts<=step))
                    {
                        Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline),"Combo stalled.");
                        if(!queued && Time.time-began>.12f && step+1<item.attackChain.Length) {Press(input); queued=true;}
                        if(scenario%4!=0 && !melee.RequiresPlantedFeet)
                            input.MoveInput((int)((Time.time-began)/.35f)%2==0?direction:new Vector2(-direction.y,direction.x));
                        if(melee.RequiresPlantedFeet && Time.time-began>.45f && direction!=Vector2.zero)
                        {
                            Assert.That(new Vector2(capsule.velocity.x,capsule.velocity.z).magnitude,Is.LessThan(.15f),"Supporting foot slides during kick contact.");
                            checkedPlant=true;
                        }
                        yield return EditorTestFrame.Next();
                    }
                    Assert.That(impacts,Is.EqualTo(step+1),"Exactly one authored impact per step.");
                    if(item.RequiresPlantedFeet(item.attackChain[step]) && direction!=Vector2.zero)
                        Assert.That(checkedPlant,Is.True,"Kick planting was not sampled.");
                }
                ProceduralUIReview.Capture("combat-sequence-"+scenario,1280,720);
                elapsed=0; while(elapsed<1.4f) {elapsed+=Time.deltaTime; yield return EditorTestFrame.Next();}
                Assert.That(melee.RequiresPlantedFeet,Is.False);
                if(direction!=Vector2.zero) Assert.That(new Vector2(capsule.velocity.x,capsule.velocity.z).magnitude,Is.GreaterThan(.5f),"Held movement did not resume.");
                Assert.That(requests.Count,Is.EqualTo(item.attackChain.Length));
                Assert.That(character.ActiveVisual.Animator.applyRootMotion,Is.False);
                melee.CancelCombat();
                elapsed=0; while(elapsed<.4f) {elapsed+=Time.deltaTime; yield return EditorTestFrame.Next();}
                Assert.That(melee.IsCombatStance||melee.IsWaitingForImpact||melee.HasBufferedAttack,Is.False);
                report.Add($"PASS {scenario}: {character.ActiveVisual.name}, {requests.Count} deliberate attacks / {impacts} impacts, movement {direction}");
            }
            input.MoveInput(Vector2.zero);
            Object.Destroy(item); // Alternate-action fixture clone; the production asset is untouched.
            File.WriteAllLines("Logs/CombatV2/play-review.txt",report);
        }
    }
    private static void Press(StarterAssetsInputs input) {input.ShootInput(false);input.ShootInput(true);input.ShootInput(false);}
}



