using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

internal static class EditorTestFrame
{
    // An EditMode coroutine tick is not necessarily a player-loop frame when
    // the Game view is unfocused. Tests of frame guards must explicitly pump it.
    public static IEnumerator Next()
    {
        int previous = Time.frameCount;
        double deadline = EditorApplication.timeSinceStartup + 5;
        do
        {
            EditorApplication.QueuePlayerLoopUpdate();
            yield return null;
        }
        while (Time.frameCount <= previous && EditorApplication.timeSinceStartup < deadline);
        Assert.That(Time.frameCount, Is.GreaterThan(previous), "The Editor did not advance its player loop.");
    }
}
