using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class GameplayActionsTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void AssignedActionRunsSynchronouslyAndReturnsItsResult(bool accepted)
    {
        var go = new GameObject("Action fixture");
        try
        {
            var target = go.AddComponent<GameplayActionTestTarget>();
            target.accepted = accepted;
            var actions = new GameplayActions { actionTarget = target };
            Assert.That(actions.TryExecute(null), Is.EqualTo(accepted));
            Assert.That(target.calls, Is.EqualTo(1));
            Object.DestroyImmediate(target);
            Assert.That(actions.TryExecute(null), Is.True, "Destroyed Unity targets behave like an empty optional slot");
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test] public void EmptyActionIsOptionalAndInvalidAssignmentIsRejected()
    {
        Assert.That(new GameplayActions().TryExecute(null), Is.True);
        var go = new GameObject("Invalid action fixture");
        try
        {
            var target = go.AddComponent<GameplayActionInvalidTestTarget>();
            LogAssert.Expect(LogType.Error, "Gameplay Action Target must implement IGameplayAction.");
            Assert.That(new GameplayActions { actionTarget = target }.TryExecute(null), Is.False);
        }
        finally { Object.DestroyImmediate(go); }
    }
}

public sealed class GameplayActionTestTarget : MonoBehaviour, IGameplayAction
{
    public bool accepted;
    public int calls;
    public bool TryExecute(PlayerCharacter player) { calls++; return accepted; }
}
public sealed class GameplayActionInvalidTestTarget : MonoBehaviour { }
