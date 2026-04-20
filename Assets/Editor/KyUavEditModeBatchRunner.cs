using System;
using System.IO;
using System.Xml;
using NUnit.Framework.Interfaces;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

public static class KyUavEditModeBatchRunner
{
    private const string ResultsPath = "Library/Logs/editmode-results.xml";

    public static void RunEditModeTests()
    {
        BatchRunCallbacks callbacks = new BatchRunCallbacks();
        TestRunnerApi api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(callbacks);

        try
        {
            Filter filter = new Filter
            {
                testMode = TestMode.EditMode,
                assemblyNames = new[] { "KyUAV.EditMode.Tests" }
            };

            ExecutionSettings settings = new ExecutionSettings(filter)
            {
                runSynchronously = true
            };

            api.Execute(settings);

            if (callbacks.Result == null)
            {
                Debug.LogError("EditMode tests did not produce a result.");
                EditorApplication.Exit(3);
                return;
            }

            WriteResultToFile(callbacks.Result, ResultsPath);
            Debug.Log(
                $"EditMode tests finished. Total={callbacks.Result.PassCount + callbacks.Result.FailCount + callbacks.Result.SkipCount + callbacks.Result.InconclusiveCount}, " +
                $"Passed={callbacks.Result.PassCount}, Failed={callbacks.Result.FailCount}, Skipped={callbacks.Result.SkipCount}, Inconclusive={callbacks.Result.InconclusiveCount}");

            EditorApplication.Exit(callbacks.Result.FailCount > 0 ? 2 : 0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(4);
        }
        finally
        {
            api.UnregisterCallbacks(callbacks);
            UnityEngine.Object.DestroyImmediate(api);
        }
    }

    private static void WriteResultToFile(ITestResultAdaptor result, string relativePath)
    {
        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), relativePath);
        string directoryPath = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        XmlWriterSettings settings = new XmlWriterSettings
        {
            Indent = true,
            NewLineOnAttributes = false
        };

        using (StreamWriter streamWriter = File.CreateText(fullPath))
        using (XmlWriter xmlWriter = XmlWriter.Create(streamWriter, settings))
        {
            TNode testRunNode = new TNode("test-run");
            int total = result.PassCount + result.FailCount + result.SkipCount + result.InconclusiveCount;
            testRunNode.AddAttribute("id", "2");
            testRunNode.AddAttribute("testcasecount", total.ToString());
            testRunNode.AddAttribute("result", result.ResultState);
            testRunNode.AddAttribute("total", total.ToString());
            testRunNode.AddAttribute("passed", result.PassCount.ToString());
            testRunNode.AddAttribute("failed", result.FailCount.ToString());
            testRunNode.AddAttribute("inconclusive", result.InconclusiveCount.ToString());
            testRunNode.AddAttribute("skipped", result.SkipCount.ToString());
            testRunNode.AddAttribute("asserts", result.AssertCount.ToString());
            testRunNode.AddAttribute("engine-version", "3.5.0.0");
            testRunNode.AddAttribute("clr-version", Environment.Version.ToString());
            testRunNode.AddAttribute("start-time", result.StartTime.ToString("u"));
            testRunNode.AddAttribute("end-time", result.EndTime.ToString("u"));
            testRunNode.AddAttribute("duration", result.Duration.ToString());
            testRunNode.ChildNodes.Add(result.ToXml());
            testRunNode.WriteTo(xmlWriter);
        }
    }

    private sealed class BatchRunCallbacks : ICallbacks
    {
        public ITestResultAdaptor Result { get; private set; }

        public void RunStarted(ITestAdaptor testsToRun)
        {
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            Result = result;
        }

        public void TestStarted(ITestAdaptor test)
        {
        }

        public void TestFinished(ITestResultAdaptor result)
        {
        }
    }
}
