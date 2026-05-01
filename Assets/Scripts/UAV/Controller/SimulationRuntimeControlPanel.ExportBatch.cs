using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class SimulationRuntimeControlPanel
{
    private void ExportCurrentResultToCsv()
    {
        if (resultExporter == null)
        {
            transientMessage = "未找到结果导出器";
            RefreshAllLabels();
            return;
        }

        bool exported = resultExporter.ExportCurrentResult();
        transientMessage = exported
            ? resultExporter.LastExportMessage
            : resultExporter.LastExportMessage;
        RefreshAllLabels();
    }

    private void ExportCurrentResultToJson()
    {
        if (resultExporter == null)
        {
            transientMessage = "未找到结果导出器";
            RefreshAllLabels();
            return;
        }

        bool exported = resultExporter.ExportCurrentResultAsJson();
        transientMessage = exported
            ? resultExporter.LastExportMessage
            : resultExporter.LastExportMessage;
        RefreshAllLabels();
    }

    private void ApplyCustomExportDirectory()
    {
        if (resultExporter == null)
        {
            transientMessage = "未找到结果导出器";
            RefreshAllLabels();
            return;
        }

        if (exportDirectoryInputField == null)
        {
            transientMessage = "目录输入框未初始化";
            RefreshAllLabels();
            return;
        }

        bool success = resultExporter.SetCustomExportDirectory(exportDirectoryInputField.text, out string message);
        transientMessage = message;
        RefreshExportDirectoryUi(success);
        RefreshAllLabels();
    }

    private void BrowseCustomExportDirectory()
    {
        if (resultExporter == null)
        {
            transientMessage = "未找到结果导出器";
            RefreshAllLabels();
            return;
        }

        string initialDirectory = exportDirectoryInputField != null && !string.IsNullOrWhiteSpace(exportDirectoryInputField.text)
            ? exportDirectoryInputField.text
            : resultExporter.GetExportDirectoryPath();

        if (!TryOpenFolderPicker(initialDirectory, out string selectedDirectory))
        {
            transientMessage = "当前环境未能打开目录选择器，可手动输入路径";
            RefreshAllLabels();
            return;
        }

        if (string.IsNullOrWhiteSpace(selectedDirectory))
        {
            transientMessage = "已取消目录选择";
            RefreshAllLabels();
            return;
        }

        if (exportDirectoryInputField != null)
        {
            exportDirectoryInputField.SetTextWithoutNotify(selectedDirectory);
        }

        bool success = resultExporter.SetCustomExportDirectory(selectedDirectory, out string message);
        transientMessage = message;
        RefreshExportDirectoryUi(success);
        RefreshAllLabels();
    }

    private void ResetExportDirectoryToDefault()
    {
        if (resultExporter == null)
        {
            transientMessage = "未找到结果导出器";
            RefreshAllLabels();
            return;
        }

        resultExporter.ResetExportDirectoryToDefault();
        transientMessage = resultExporter.LastExportMessage;
        RefreshExportDirectoryUi(true);
        RefreshAllLabels();
    }

    private void StartNewExportSession()
    {
        if (resultExporter == null)
        {
            transientMessage = "未找到结果导出器";
            RefreshAllLabels();
            return;
        }

        string sessionFolderName = resultExporter.BeginNewArchiveSession();
        transientMessage = $"已切换到新会话：{sessionFolderName}";
        RefreshExportDirectoryUi(false);
        RefreshAllLabels();
    }

    private void OnDecreaseBatchRunCountClicked()
    {
        configuredBatchRunCount = Mathf.Clamp(configuredBatchRunCount - 1, MinBatchRunCount, MaxBatchRunCount);
        if (batchExperimentRunner != null)
        {
            batchExperimentRunner.SetBatchRunCount(configuredBatchRunCount);
        }

        transientMessage = $"批量实验轮数 {configuredBatchRunCount}";
        RefreshAllLabels();
    }

    private void OnIncreaseBatchRunCountClicked()
    {
        configuredBatchRunCount = Mathf.Clamp(configuredBatchRunCount + 1, MinBatchRunCount, MaxBatchRunCount);
        if (batchExperimentRunner != null)
        {
            batchExperimentRunner.SetBatchRunCount(configuredBatchRunCount);
        }

        transientMessage = $"批量实验轮数 {configuredBatchRunCount}";
        RefreshAllLabels();
    }

    private void StartBatchExperiments()
    {
        if (batchExperimentRunner == null)
        {
            transientMessage = "未找到批量实验执行器";
            RefreshAllLabels();
            return;
        }

        batchExperimentRunner.UseCurrentRuntimeConfiguration();
        batchExperimentRunner.SetBatchRunCount(configuredBatchRunCount);
        batchExperimentRunner.StartBatch();
        transientMessage = batchExperimentRunner.LastBatchMessage;
        RefreshBatchStatus();
        RefreshAllLabels();
    }

    private void StopBatchExperiments()
    {
        if (batchExperimentRunner == null)
        {
            transientMessage = "未找到批量实验执行器";
            RefreshAllLabels();
            return;
        }

        batchExperimentRunner.StopBatch();
        transientMessage = batchExperimentRunner.LastBatchMessage;
        RefreshBatchStatus();
        RefreshAllLabels();
    }

    private bool TryOpenFolderPicker(string initialDirectory, out string selectedDirectory)
    {
        selectedDirectory = string.Empty;

        if (TryOpenEditorFolderPanel(initialDirectory, out selectedDirectory))
        {
            return true;
        }

        return TryOpenWindowsFolderBrowser(initialDirectory, out selectedDirectory);
    }

    private bool TryOpenEditorFolderPanel(string initialDirectory, out string selectedDirectory)
    {
        selectedDirectory = string.Empty;

        try
        {
            Type editorUtilityType = Type.GetType("UnityEditor.EditorUtility, UnityEditor");
            if (editorUtilityType == null)
            {
                return false;
            }

            MethodInfo openFolderPanelMethod = editorUtilityType.GetMethod(
                "OpenFolderPanel",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(string), typeof(string), typeof(string) },
                null);

            if (openFolderPanelMethod == null)
            {
                return false;
            }

            object result = openFolderPanelMethod.Invoke(
                null,
                new object[] { "选择导出目录", initialDirectory ?? string.Empty, string.Empty });

            selectedDirectory = result as string ?? string.Empty;
            return true;
        }
        catch
        {
            selectedDirectory = string.Empty;
            return false;
        }
    }

    private bool TryOpenWindowsFolderBrowser(string initialDirectory, out string selectedDirectory)
    {
        selectedDirectory = string.Empty;

        try
        {
            string dialogResultName = null;
            string resolvedSelectedDirectory = string.Empty;
            Exception threadException = null;
            Thread pickerThread = new Thread(() =>
            {
                try
                {
                    Assembly winFormsAssembly = Assembly.Load("System.Windows.Forms");
                    Type folderDialogType = winFormsAssembly.GetType("System.Windows.Forms.FolderBrowserDialog");
                    Type dialogResultType = winFormsAssembly.GetType("System.Windows.Forms.DialogResult");
                    if (folderDialogType == null || dialogResultType == null)
                    {
                        return;
                    }

                    using (IDisposable dialog = Activator.CreateInstance(folderDialogType) as IDisposable)
                    {
                        if (dialog == null)
                        {
                            return;
                        }

                        folderDialogType.GetProperty("Description")?.SetValue(dialog, "选择导出目录");
                        folderDialogType.GetProperty("ShowNewFolderButton")?.SetValue(dialog, true);

                        if (!string.IsNullOrWhiteSpace(initialDirectory))
                        {
                            folderDialogType.GetProperty("SelectedPath")?.SetValue(dialog, initialDirectory);
                        }

                        object result = folderDialogType.GetMethod("ShowDialog", Type.EmptyTypes)?.Invoke(dialog, null);
                        if (result == null)
                        {
                            return;
                        }

                        dialogResultName = Enum.GetName(dialogResultType, result);
                        if (string.Equals(dialogResultName, "OK", StringComparison.OrdinalIgnoreCase))
                        {
                            resolvedSelectedDirectory = folderDialogType.GetProperty("SelectedPath")?.GetValue(dialog) as string ?? string.Empty;
                        }
                    }
                }
                catch (Exception exception)
                {
                    threadException = exception;
                }
            });

            pickerThread.SetApartmentState(ApartmentState.STA);
            pickerThread.Start();
            pickerThread.Join();

            if (threadException != null)
            {
                return false;
            }

            selectedDirectory = resolvedSelectedDirectory;
            return true;
        }
        catch
        {
            selectedDirectory = string.Empty;
            return false;
        }
    }
}
