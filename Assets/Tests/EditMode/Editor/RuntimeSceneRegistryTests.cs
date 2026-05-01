using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class RuntimeSceneRegistryTests
{
    private readonly List<GameObject> createdObjects = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject createdObject in createdObjects)
        {
            if (createdObject != null)
            {
                Object.DestroyImmediate(createdObject);
            }
        }

        createdObjects.Clear();
    }

    [Test]
    public void Get_PrefersLocalComponentBeforeCachedSceneObject()
    {
        TaskPointSpawner cachedSpawner = CreateSpawner("CachedSpawner");
        RuntimeSceneRegistry.Register(cachedSpawner);

        TaskPointSpawner localSpawner = CreateSpawner("LocalSpawner");

        TaskPointSpawner resolved = RuntimeSceneRegistry.Get<TaskPointSpawner>(localSpawner.transform);

        Assert.AreSame(localSpawner, resolved);
    }

    [Test]
    public void Get_ReplacesDestroyedCachedComponent()
    {
        TaskPointSpawner cachedSpawner = CreateSpawner("DestroyedSpawner");
        RuntimeSceneRegistry.Register(cachedSpawner);
        Object.DestroyImmediate(cachedSpawner.gameObject);

        TaskPointSpawner replacementSpawner = CreateSpawner("ReplacementSpawner");

        TaskPointSpawner resolved = RuntimeSceneRegistry.Get<TaskPointSpawner>();

        Assert.AreSame(replacementSpawner, resolved);
    }

    private TaskPointSpawner CreateSpawner(string name)
    {
        GameObject gameObject = new GameObject(name);
        createdObjects.Add(gameObject);
        return gameObject.AddComponent<TaskPointSpawner>();
    }
}
