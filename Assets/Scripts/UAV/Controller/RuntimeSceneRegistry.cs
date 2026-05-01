using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Caches scene-level runtime components so systems do not repeatedly scan the scene.
/// </summary>
public static class RuntimeSceneRegistry
{
    private static readonly Dictionary<Type, UnityEngine.Object> cachedObjects =
        new Dictionary<Type, UnityEngine.Object>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        cachedObjects.Clear();
    }

    public static T Register<T>(T instance) where T : Component
    {
        if (instance == null)
        {
            return null;
        }

        cachedObjects[typeof(T)] = instance;
        return instance;
    }

    public static T Resolve<T>(T currentReference, Component localContext = null) where T : Component
    {
        if (currentReference != null)
        {
            return Register(currentReference);
        }

        return Get<T>(localContext);
    }

    public static T Resolve<T>(T currentReference, T preferredReference, Component localContext = null) where T : Component
    {
        if (currentReference != null)
        {
            return Register(currentReference);
        }

        if (preferredReference != null)
        {
            return Register(preferredReference);
        }

        return Get<T>(localContext);
    }

    public static T Get<T>(Component localContext = null) where T : Component
    {
        if (localContext != null)
        {
            T local = localContext.GetComponent<T>();
            if (local != null)
            {
                return Register(local);
            }
        }

        Type type = typeof(T);
        if (cachedObjects.TryGetValue(type, out UnityEngine.Object cachedObject))
        {
            T cached = cachedObject as T;
            if (cached != null)
            {
                return cached;
            }

            cachedObjects.Remove(type);
        }

        T found = UnityEngine.Object.FindObjectOfType<T>();
        return Register(found);
    }
}
