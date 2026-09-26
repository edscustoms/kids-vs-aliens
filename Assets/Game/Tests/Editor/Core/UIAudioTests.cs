using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

[TestFixture, Category("Core")]
public sealed class UIAudioTests
{
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Clear(AudioService service) { foreach (var source in service.GetComponentsInChildren<AudioSource>()) source.Stop(); }
    private static AudioService Service(AudioLibrary library, SoundEvent click, SoundEvent confirm)
    {
        var root = new GameObject("UI Audio Test"); root.SetActive(false);
        var service = root.AddComponent<AudioService>(); Set(service, "library", library);
        var feedback = root.AddComponent<UIAudioFeedback>(); Set(feedback, "clickSound", click); Set(feedback, "playConfirmSound", confirm);
        root.SetActive(true); return service;
    }

    [TestCase("Assets/Game/Scenes/GamePoc.unity")]
    [TestCase("Assets/Game/Scenes/ConstructionSite.unity")]
    public void AuthoredHud_ActionControlsAreExcluded_NavigationStillRegisters(string path)
    {
        var scene = EditorSceneManager.OpenPreviewScene(path);
        try
        {
            var buttons = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Button>(true)).ToArray();
            var actions = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<UIVirtualButton>(true)).Select(c => c.GetComponent<Button>())
                .Concat(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BeamHoistButton>(true)).Select(c => c.GetComponent<Button>())).ToArray();
            Assert.That(actions.Length, Is.GreaterThanOrEqualTo(3), "Authored jump, fire and sprint controls must be covered.");
            foreach (var button in buttons) UIAudioButton.Ensure(button);
            foreach (var action in actions) Assert.That(action.GetComponent<UIAudioButton>(), Is.Null, action.name);
            Assert.That(buttons.Except(actions).Count(b => b.GetComponent<UIAudioButton>() != null), Is.GreaterThan(0), "HUD navigation still registers.");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [UnityTest]
    public IEnumerator ClickOutcome_IsSingleAndSurvivesSceneUnload()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        // Allocate the callback closure AFTER the domain reload, not before the yield.
        {
        var clickClip = AudioClip.Create("click test", 88200, 1, 44100, false);
        var confirmClip = AudioClip.Create("confirm test", 88200, 1, 44100, false);
        var click = ScriptableObject.CreateInstance<SoundEvent>(); click.variants = new[] { clickClip }; click.playDuringPause = true;
        var confirm = ScriptableObject.CreateInstance<SoundEvent>(); confirm.variants = new[] { confirmClip }; confirm.playDuringPause = true;
        var library = ScriptableObject.CreateInstance<AudioLibrary>(); library.events.Add(click); library.events.Add(confirm);
        var service = Service(library, click, confirm);
        var eventSystem = new GameObject("EventSystem").AddComponent<EventSystem>();
        var listener = new GameObject("Listener").AddComponent<AudioListener>();
        var button = new GameObject("Ordinary", typeof(RectTransform)).AddComponent<Button>(); UIAudioButton.Ensure(button);
        var hud = new GameObject("HUD test", typeof(RectTransform));
        AudioService next = null;
        try
        {
            yield return EditorTestFrame.Next();
            var pointer = new PointerEventData(eventSystem) { button = PointerEventData.InputButton.Left };
            foreach (var role in new[] { typeof(UIVirtualButton), typeof(UIVirtualJoystick), typeof(UIVirtualTouchZone), typeof(BeamHoistButton) })
            {
                var action = new GameObject("Action", typeof(RectTransform)); action.transform.SetParent(hud.transform);
                var input = (Behaviour)action.AddComponent(role); input.enabled = false;
                var target = new GameObject("Nested hit area", typeof(RectTransform)).AddComponent<Button>(); target.transform.SetParent(action.transform);
                UIAudioButton.Ensure(target);
                Assert.That(target.GetComponent<UIAudioButton>(), Is.Null, role.Name);
                // Simulate a hook already present on an older prefab or registered before its input role.
                target.gameObject.AddComponent<UIAudioButton>();
                ExecuteEvents.Execute(target.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                yield return EditorTestFrame.Next(); yield return EditorTestFrame.Next();
                Assert.That(service.GetComponentsInChildren<AudioSource>().Any(s => s.clip != null), Is.False, role.Name + " remains silent even with an existing hook.");
            }
            var dynamicButton = new GameObject("Dynamic action", typeof(RectTransform)).AddComponent<Button>(); dynamicButton.transform.SetParent(hud.transform);
            UIAudioButton.Ensure(dynamicButton);
            var virtualInput = dynamicButton.gameObject.AddComponent<UIVirtualButton>();
            int presses = 0, releases = 0, clicks = 0;
            virtualInput.buttonStateOutputEvent.AddListener(held => { if (held) presses++; else releases++; });
            virtualInput.buttonClickOutputEvent.AddListener(() => clicks++);
            ExecuteEvents.Execute(dynamicButton.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(dynamicButton.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(dynamicButton.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            yield return EditorTestFrame.Next(); yield return EditorTestFrame.Next();
            Assert.That((presses, releases, clicks), Is.EqualTo((1, 1, 1)), "Gameplay callbacks remain intact.");
            Assert.That(service.GetComponentsInChildren<AudioSource>().Any(s => s.clip != null), Is.False, "Late-added gameplay input remains silent.");

            var inventory = hud.AddComponent<PlayerInventory>(); inventory.enabled = false;
            var slot = new GameObject("Quick slot").AddComponent<InventorySlotUI>(); slot.transform.SetParent(hud.transform); slot.Setup(inventory, 0);
            ExecuteEvents.Execute(slot.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            yield return EditorTestFrame.Next(); yield return EditorTestFrame.Next();
            Assert.That(service.GetComponentsInChildren<AudioSource>().Count(s => s.clip == clickClip), Is.EqualTo(1), "Actual quick-slot selection still clicks.");
            Clear(service); yield return EditorTestFrame.Next();
            var inventoryView = hud.AddComponent<InventoryManagementView>(); Set(inventoryView, "inventory", inventory);
            inventoryView.Select(0, false);
            yield return EditorTestFrame.Next(); yield return EditorTestFrame.Next();
            Assert.That(service.GetComponentsInChildren<AudioSource>().Count(s => s.clip == clickClip), Is.EqualTo(1), "Inventory selection still clicks.");
            Clear(service); yield return EditorTestFrame.Next();
            Time.timeScale = 0; button.onClick.Invoke();
            yield return EditorTestFrame.Next(); yield return EditorTestFrame.Next();
            Assert.That(service.GetComponentsInChildren<AudioSource>().Count(s => s.clip == clickClip), Is.EqualTo(1), "Ordinary click works while paused.");
            Clear(service); yield return EditorTestFrame.Next();
            // Generic audio listener runs BEFORE the semantic action.
            button.onClick.AddListener(() => UIAudioFeedback.ConfirmGameplay());
            button.onClick.Invoke(); yield return EditorTestFrame.Next(); yield return EditorTestFrame.Next();
            Assert.That(service.GetComponentsInChildren<AudioSource>().Count(s => s.clip == confirmClip), Is.EqualTo(1));
            Assert.That(service.GetComponentsInChildren<AudioSource>().Any(s => s.clip == clickClip), Is.False, "No ordinary click on final action.");
            Clear(service); yield return EditorTestFrame.Next();
            button.interactable = false;
            ExecuteEvents.Execute(button.gameObject, new PointerEventData(eventSystem) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
            yield return EditorTestFrame.Next(); yield return EditorTestFrame.Next();
            Assert.That(service.GetComponentsInChildren<AudioSource>().Any(s => s.clip != null), Is.False, "Disabled buttons remain silent.");
            Object.Destroy(button.gameObject);
            // Semantic listener runs BEFORE the generic hook; hiding the button must not add a click.
            button = new GameObject("Final", typeof(RectTransform)).AddComponent<Button>();
            button.onClick.AddListener(() => { UIAudioFeedback.ConfirmGameplay(true); button.gameObject.SetActive(false); });
            UIAudioButton.Ensure(button); yield return EditorTestFrame.Next();
            var oldScene = service.gameObject.scene;
            button.onClick.Invoke();
            Assert.That(AudioService.Instance, Is.Null, "Old service relinquishes ownership for next scene.");
            Assert.That(service.gameObject.scene.name, Is.EqualTo("DontDestroyOnLoad"));
            var newScene = SceneManager.CreateScene("UI Audio Destination"); SceneManager.SetActiveScene(newScene);
            next = Service(library, click, confirm);
            yield return SceneManager.UnloadSceneAsync(oldScene);
            Assert.That(AudioService.Instance, Is.SameAs(next));
            Assert.That(service.GetComponentsInChildren<AudioSource>().Count(s => s.clip == confirmClip && s.isPlaying), Is.EqualTo(1), "Confirmation continues through scene unload.");
            Assert.That(service.GetComponentsInChildren<AudioSource>().Any(s => s.clip == clickClip), Is.False);
            Clear(service); yield return EditorTestFrame.Next(); yield return EditorTestFrame.Next();
            Assert.That(service == null, Is.True, "Retired pool cleans itself up after its UI tail finishes.");
            // Quit/reset-to-menu keep the ordinary click, not the melodic confirmation.
            UIAudioFeedback.Click(true); UIAudioFeedback.Click();
            yield return EditorTestFrame.Next(); yield return EditorTestFrame.Next();
            Assert.That(AudioService.Instance, Is.Null);
            Assert.That(next.GetComponentsInChildren<AudioSource>().Count(s => s.clip == clickClip && s.isPlaying), Is.EqualTo(1));
            Assert.That(next.GetComponentsInChildren<AudioSource>().Any(s => s.clip == confirmClip), Is.False);
        }
        finally
        {
            Time.timeScale = 1;
            if (service != null) Object.Destroy(service.gameObject);
            if (next != null) Object.Destroy(next.gameObject);
            if (button != null) Object.Destroy(button.gameObject);
            if (hud != null) Object.Destroy(hud);
            if (listener != null) Object.Destroy(listener.gameObject);
            if (eventSystem != null) Object.Destroy(eventSystem.gameObject);
            Object.Destroy(library); Object.Destroy(click); Object.Destroy(confirm); Object.Destroy(clickClip); Object.Destroy(confirmClip);
        }
        }
        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator ExitAfterFailure()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    [Test]
    public void StarterPack_LockedAssignmentsAndImportsAreValid()
    {
        var library = AssetDatabase.LoadAssetAtPath<AudioLibrary>(AudioAssets.LibraryPath);
        foreach (var pair in new[] { (AudioAssets.UiClickPath, "cool-interface-click-tone-2568"), (AudioAssets.UiPlayPath, "click-melodic-tone-1129") })
        {
            var sound = AssetDatabase.LoadAssetAtPath<SoundEvent>(pair.Item1);
            Assert.That(sound.variants.Single().name, Is.EqualTo(pair.Item2));
            Assert.That(sound.status, Is.EqualTo(SoundStatus.Placeholder));
            Assert.That(sound.playDuringPause && !sound.spatial);
            Assert.That(sound.mixerGroup.name, Is.EqualTo("UI"));
        }
        foreach (var sound in library.events)
        {
            var issues = AudioLibraryValidation.Issues(sound);
            if (sound.name == "Beam_Loop") { Assert.That(issues.Count, Is.EqualTo(1)); Assert.That(sound.status, Is.EqualTo(SoundStatus.Missing)); }
            else { Assert.That(issues, Is.Empty, sound.name); Assert.That(sound.status, Is.Not.EqualTo(SoundStatus.Final)); }
        }
    }
}
