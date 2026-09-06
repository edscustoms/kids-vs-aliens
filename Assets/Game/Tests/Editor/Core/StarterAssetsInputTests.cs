using NUnit.Framework;
using StarterAssets;
using UnityEngine;

[TestFixture]
[Category("Core")]
public class StarterAssetsInputTests
{
    private GameObject inputObject;
    private StarterAssetsInputs input;

    [SetUp]
    public void SetUp()
    {
        inputObject =
            new GameObject("StarterAssetsInputTest");

        input =
            inputObject.AddComponent<StarterAssetsInputs>();
    }

    [TearDown]
    public void TearDown()
    {
        if (inputObject != null)
            Object.DestroyImmediate(inputObject);
    }

    [Test]
    public void ShootInput_PublishesOnlyRealStateChanges()
    {
        int stateEventCount = 0;
        bool lastState = false;

        input.ShootStateChanged +=
            state =>
            {
                stateEventCount++;
                lastState = state;
            };

        input.ShootInput(true);
        input.ShootInput(true);

        Assert.That(stateEventCount, Is.EqualTo(1));
        Assert.That(lastState, Is.True);

        input.ShootInput(false);

        Assert.That(stateEventCount, Is.EqualTo(2));
        Assert.That(lastState, Is.False);
    }

    [Test]
    public void CancelShootInput_DoesNotPublishNormalRelease()
    {
        int stateEventCount = 0;
        int cancelEventCount = 0;

        input.ShootStateChanged +=
            state => stateEventCount++;

        input.ShootCanceled +=
            () => cancelEventCount++;

        input.ShootInput(true);
        input.CancelShootInput();

        Assert.That(input.shoot, Is.False);
        Assert.That(stateEventCount, Is.EqualTo(1));
        Assert.That(cancelEventCount, Is.EqualTo(1));
    }
}
