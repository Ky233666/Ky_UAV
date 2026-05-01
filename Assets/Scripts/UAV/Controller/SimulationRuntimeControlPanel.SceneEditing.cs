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
    private void ToggleSpawnPointPlacement()
    {
        if (spawnPointManager == null)
        {
            transientMessage = "未找到起飞点管理器";
            RefreshAllLabels();
            return;
        }

        spawnPointManager.TogglePlacementMode();
        transientMessage = spawnPointManager.IsPlacementMode
            ? "已进入起飞点放置模式，点击地面放置"
            : "已取消起飞点放置模式";
        RefreshAllLabels();
        RefreshSummary();
    }

    private void ToggleSpawnPointDeletion()
    {
        if (spawnPointManager == null)
        {
            transientMessage = "未找到起飞点管理器";
            RefreshAllLabels();
            return;
        }

        spawnPointManager.ToggleDeleteMode();
        transientMessage = spawnPointManager.IsDeleteMode
            ? "已进入起飞点删除模式，点击已有起点删除"
            : "已取消起飞点删除模式";
        RefreshAllLabels();
        RefreshSummary();
    }

    private void ToggleSpawnPointMove()
    {
        if (spawnPointManager == null)
        {
            transientMessage = "未找到起飞点管理器";
            RefreshAllLabels();
            return;
        }

        spawnPointManager.ToggleMoveMode();
        transientMessage = spawnPointManager.IsMoveMode
            ? "已进入起飞点移动模式，先点起点再点新位置"
            : "已取消起飞点移动模式";
        RefreshAllLabels();
        RefreshSummary();
    }

    private void ClearSpawnPoints()
    {
        if (spawnPointManager == null)
        {
            transientMessage = "未找到起飞点管理器";
            RefreshAllLabels();
            return;
        }

        spawnPointManager.ClearSpawnPoints();
        transientMessage = "已清空手动起飞点";
        RefreshAllLabels();
        RefreshSummary();
    }

    private void ToggleObstacleCreateMode()
    {
        if (obstacleEditor == null)
        {
            transientMessage = "未找到障碍物编辑器";
            RefreshAllLabels();
            return;
        }

        if (simulationManager != null && simulationManager.currentState != SimulationState.Idle)
        {
            transientMessage = "障碍物仅可在 Idle 状态编辑";
            RefreshAllLabels();
            return;
        }

        obstacleEditor.ToggleCreateMode();
        transientMessage = obstacleEditor.IsCreateMode
            ? "已进入障碍物绘制模式，拖拽地面生成建筑"
            : "已退出障碍物绘制模式";
        RefreshAllLabels();
        RefreshSummary();
    }

    private void ToggleObstacleDeleteMode()
    {
        if (obstacleEditor == null)
        {
            transientMessage = "未找到障碍物编辑器";
            RefreshAllLabels();
            return;
        }

        if (simulationManager != null && simulationManager.currentState != SimulationState.Idle)
        {
            transientMessage = "障碍物仅可在 Idle 状态编辑";
            RefreshAllLabels();
            return;
        }

        obstacleEditor.ToggleDeleteMode();
        transientMessage = obstacleEditor.IsDeleteMode
            ? "已进入障碍物删除模式，点击自定义建筑删除"
            : "已退出障碍物删除模式";
        RefreshAllLabels();
        RefreshSummary();
    }

    private void ClearCustomObstacles()
    {
        if (obstacleEditor == null)
        {
            transientMessage = "未找到障碍物编辑器";
            RefreshAllLabels();
            return;
        }

        if (simulationManager != null && simulationManager.currentState != SimulationState.Idle)
        {
            transientMessage = "障碍物仅可在 Idle 状态编辑";
            RefreshAllLabels();
            return;
        }

        obstacleEditor.ClearCustomObstacles();
        transientMessage = "已清空自定义障碍物";
        RefreshAllLabels();
        RefreshSummary();
    }

    private void OnDecreaseObstacleHeightClicked()
    {
        configuredObstacleHeight = Mathf.Clamp(configuredObstacleHeight - ObstacleHeightStep, MinObstacleHeight, MaxObstacleHeight);
        obstacleEditor?.SetDefaultObstacleHeight(configuredObstacleHeight);
        transientMessage = $"默认障碍高度 {configuredObstacleHeight:0.0}m";
        RefreshAllLabels();
    }

    private void OnIncreaseObstacleHeightClicked()
    {
        configuredObstacleHeight = Mathf.Clamp(configuredObstacleHeight + ObstacleHeightStep, MinObstacleHeight, MaxObstacleHeight);
        obstacleEditor?.SetDefaultObstacleHeight(configuredObstacleHeight);
        transientMessage = $"默认障碍高度 {configuredObstacleHeight:0.0}m";
        RefreshAllLabels();
    }

    private void OnDecreaseObstacleScaleClicked()
    {
        configuredObstacleScale = Mathf.Clamp(configuredObstacleScale - ObstacleScaleStep, MinObstacleScale, MaxObstacleScale);
        obstacleEditor?.SetDefaultObstacleScaleMultiplier(configuredObstacleScale);
        transientMessage = $"默认障碍缩放 {configuredObstacleScale:0.00}x";
        RefreshAllLabels();
    }

    private void OnIncreaseObstacleScaleClicked()
    {
        configuredObstacleScale = Mathf.Clamp(configuredObstacleScale + ObstacleScaleStep, MinObstacleScale, MaxObstacleScale);
        obstacleEditor?.SetDefaultObstacleScaleMultiplier(configuredObstacleScale);
        transientMessage = $"默认障碍缩放 {configuredObstacleScale:0.00}x";
        RefreshAllLabels();
    }

    private void OnPreviousObstacleTemplateClicked()
    {
        if (obstacleEditor == null)
        {
            transientMessage = "未找到障碍物编辑器";
            RefreshAllLabels();
            return;
        }

        if (simulationManager != null && simulationManager.currentState != SimulationState.Idle)
        {
            transientMessage = "障碍物仅可在 Idle 状态编辑";
            RefreshAllLabels();
            return;
        }

        obstacleEditor.SelectPreviousTemplate();
        configuredObstacleTemplateName = obstacleEditor.GetCurrentTemplateDisplayName();
        transientMessage = $"障碍样式 {configuredObstacleTemplateName}";
        RefreshAllLabels();
    }

    private void OnNextObstacleTemplateClicked()
    {
        if (obstacleEditor == null)
        {
            transientMessage = "未找到障碍物编辑器";
            RefreshAllLabels();
            return;
        }

        if (simulationManager != null && simulationManager.currentState != SimulationState.Idle)
        {
            transientMessage = "障碍物仅可在 Idle 状态编辑";
            RefreshAllLabels();
            return;
        }

        obstacleEditor.SelectNextTemplate();
        configuredObstacleTemplateName = obstacleEditor.GetCurrentTemplateDisplayName();
        transientMessage = $"障碍样式 {configuredObstacleTemplateName}";
        RefreshAllLabels();
    }
}
