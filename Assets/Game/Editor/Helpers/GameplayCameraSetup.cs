using System.Linq;
using Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GameplayCameraSetup
{
    public const string ProfilePath = "Assets/Game/Data/Camera/GameplayCameraProfile.asset";

    public static GameplayCameraController ConfigureScene(PlayerCharacter player)
    {
        if (player == null) return null;
        Scene scene = player.gameObject.scene;
        var cameras = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<CinemachineVirtualCamera>(true))
            .Where(camera => camera.Follow != null
                && (camera.Follow == player.transform || camera.Follow.IsChildOf(player.transform))
                && camera.GetCinemachineComponent<CinemachineTransposer>() != null).ToArray();
        if (cameras.Length != 1)
        {
            Debug.LogWarning($"Camera settings require one player-following Transposer camera in '{scene.name}'; found {cameras.Length}.", player);
            return null;
        }
        var controller = cameras[0].GetComponent<GameplayCameraController>();
        if (controller == null) controller = Undo.AddComponent<GameplayCameraController>(cameras[0].gameObject);
        var serialized = new SerializedObject(controller);
        if (serialized.FindProperty("profile").objectReferenceValue == null)
        {
            serialized.FindProperty("profile").objectReferenceValue = EnsureProfile();
            serialized.ApplyModifiedProperties();
        }
        return controller;
    }

    public static GameplayCameraProfile EnsureProfile()
    {
        var profile = AssetDatabase.LoadAssetAtPath<GameplayCameraProfile>(ProfilePath);
        if (profile != null) return profile;
        MenuUISetup.EnsureFolder("Assets/Game/Data/Camera");
        profile = ScriptableObject.CreateInstance<GameplayCameraProfile>();
        AssetDatabase.CreateAsset(profile, ProfilePath);
        return profile;
    }
}
