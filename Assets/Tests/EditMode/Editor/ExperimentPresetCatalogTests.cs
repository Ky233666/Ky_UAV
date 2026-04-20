using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class ExperimentPresetCatalogTests
{
    private readonly List<ExperimentPreset> createdPresets = new List<ExperimentPreset>();

    [TearDown]
    public void TearDown()
    {
        foreach (ExperimentPreset preset in createdPresets)
        {
            if (preset != null)
            {
                Object.DestroyImmediate(preset);
            }
        }

        createdPresets.Clear();
    }

    [Test]
    public void Build_SortsPreferredGroupsAndPresetNames()
    {
        List<ExperimentPreset> presets = new List<ExperimentPreset>
        {
            CreatePreset("Density Dense", "density"),
            CreatePreset("Planning RRT", "planning"),
            CreatePreset("Scheduling PriorityGreedy", "scheduling"),
            CreatePreset("Scaling 8 UAV", "scaling"),
            CreatePreset("Scheduling EvenSplit", "scheduling"),
            CreatePreset("AdHoc Demo", "adhoc")
        };

        List<ExperimentPresetGroupInfo> groups = ExperimentPresetCatalog.Build(presets);

        Assert.AreEqual(5, groups.Count);
        Assert.AreEqual("scheduling", groups[0].GroupKey);
        Assert.AreEqual("planning", groups[1].GroupKey);
        Assert.AreEqual("scaling", groups[2].GroupKey);
        Assert.AreEqual("density", groups[3].GroupKey);
        Assert.AreEqual("adhoc", groups[4].GroupKey);
        Assert.AreEqual("Scheduling EvenSplit", ExperimentPresetCatalog.GetPresetDisplayName(groups[0].Presets[0]));
        Assert.AreEqual("Scheduling PriorityGreedy", ExperimentPresetCatalog.GetPresetDisplayName(groups[0].Presets[1]));
    }

    [Test]
    public void GetPresetShortLabel_StripsKnownGroupPrefix()
    {
        ExperimentPreset preset = CreatePreset("Planning AStar", "planning");

        string shortLabel = ExperimentPresetCatalog.GetPresetShortLabel(preset);

        Assert.AreEqual("AStar", shortLabel);
    }

    private ExperimentPreset CreatePreset(string presetName, string groupName)
    {
        ExperimentPreset preset = ScriptableObject.CreateInstance<ExperimentPreset>();
        preset.name = presetName.Replace(' ', '_');
        preset.presetName = presetName;
        preset.groupName = groupName;
        createdPresets.Add(preset);
        return preset;
    }
}
