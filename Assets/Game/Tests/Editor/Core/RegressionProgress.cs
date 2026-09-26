using NUnit.Framework.Interfaces;
using UnityEngine;
using UnityEngine.TestRunner;

[assembly: TestRunCallback(typeof(RegressionProgress))]

/// <summary>Leave the active test in the Unity log even if the runner crashes or stalls.</summary>
public sealed class RegressionProgress : ITestRunCallback
{
    public void RunStarted(ITest tests) { }
    public void RunFinished(ITestResult result) => Debug.Log("REGRESSION FINISHED: " + result.ResultState);
    public void TestStarted(ITest test)
    {
        if (!test.IsSuite) Debug.Log("REGRESSION START: " + test.FullName);
    }
    public void TestFinished(ITestResult result)
    {
        if (result.Test.IsSuite) return;
        Debug.Log("REGRESSION RESULT: " + result.ResultState + " " + result.FullName);
        // Manual runs may abort before exporting XML. Keep the actual failure,
        // not just the runner's final Internal_CallUpdateFunctions stack frame.
        if (result.ResultState.Status == TestStatus.Failed)
            Debug.Log("REGRESSION FAILURE: " + result.FullName + "\n" + result.Message + "\n" + result.StackTrace);
    }
}
