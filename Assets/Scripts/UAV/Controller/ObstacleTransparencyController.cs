using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Makes scene obstacles translucent while algorithm visualization is being played.
/// This only changes renderer materials at runtime; colliders, layers and path planning data stay unchanged.
/// </summary>
public class ObstacleTransparencyController : MonoBehaviour
{
    [Header("References")]
    public DroneManager droneManager;
    public Transform obstacleRoot;
    public string fallbackObstacleRootName = "Buildings";

    [Header("Transparency")]
    [Range(0.05f, 0.85f)]
    public float transparentAlpha = 0.24f;
    public Color transparentTint = new Color(0.72f, 0.86f, 1f, 1f);
    public bool includeInactiveRenderers = true;
    public bool disableShadowsWhileTransparent = true;

    private readonly List<RendererState> rendererStates = new List<RendererState>();
    private readonly Dictionary<int, Material[]> transparentMaterialArrays = new Dictionary<int, Material[]>();
    private Material transparentMaterial;

    public bool IsTransparent { get; private set; }

    private void Awake()
    {
        RuntimeSceneRegistry.Register(this);
    }

    private void OnDisable()
    {
        RestoreOriginalRenderers();
    }

    private void OnDestroy()
    {
        RestoreOriginalRenderers();
        if (transparentMaterial != null)
        {
            Destroy(transparentMaterial);
            transparentMaterial = null;
        }
    }

    public void SetTransparent(bool enabled)
    {
        if (enabled)
        {
            ApplyTransparency();
            return;
        }

        RestoreOriginalRenderers();
    }

    public void RefreshTargets()
    {
        bool restoreAfterRefresh = IsTransparent;
        RestoreOriginalRenderers();
        rendererStates.Clear();
        transparentMaterialArrays.Clear();

        if (restoreAfterRefresh)
        {
            ApplyTransparency();
        }
    }

    private void ApplyTransparency()
    {
        if (IsTransparent)
        {
            return;
        }

        Transform root = ResolveObstacleRoot();
        if (root == null)
        {
            return;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(includeInactiveRenderers);
        rendererStates.Clear();
        transparentMaterialArrays.Clear();

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer is LineRenderer)
            {
                continue;
            }

            Material[] originalMaterials = renderer.sharedMaterials;
            if (originalMaterials == null || originalMaterials.Length == 0)
            {
                continue;
            }

            rendererStates.Add(new RendererState
            {
                renderer = renderer,
                originalSharedMaterials = originalMaterials,
                shadowCastingMode = renderer.shadowCastingMode,
                receiveShadows = renderer.receiveShadows
            });

            renderer.sharedMaterials = GetTransparentMaterials(originalMaterials.Length);
            if (disableShadowsWhileTransparent)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        IsTransparent = rendererStates.Count > 0;
    }

    private void RestoreOriginalRenderers()
    {
        if (!IsTransparent && rendererStates.Count == 0)
        {
            return;
        }

        for (int i = 0; i < rendererStates.Count; i++)
        {
            RendererState state = rendererStates[i];
            if (state == null || state.renderer == null)
            {
                continue;
            }

            state.renderer.sharedMaterials = state.originalSharedMaterials;
            state.renderer.shadowCastingMode = state.shadowCastingMode;
            state.renderer.receiveShadows = state.receiveShadows;
        }

        rendererStates.Clear();
        transparentMaterialArrays.Clear();
        IsTransparent = false;
    }

    private Transform ResolveObstacleRoot()
    {
        if (obstacleRoot != null)
        {
            return obstacleRoot;
        }

        droneManager = RuntimeSceneRegistry.Resolve(droneManager, this);
        if (droneManager != null && droneManager.obstacleRoot != null)
        {
            obstacleRoot = droneManager.obstacleRoot;
            return obstacleRoot;
        }

        if (!string.IsNullOrWhiteSpace(fallbackObstacleRootName))
        {
            GameObject rootObject = GameObject.Find(fallbackObstacleRootName);
            if (rootObject != null)
            {
                obstacleRoot = rootObject.transform;
            }
        }

        return obstacleRoot;
    }

    private Material[] GetTransparentMaterials(int count)
    {
        if (transparentMaterialArrays.TryGetValue(count, out Material[] materials))
        {
            return materials;
        }

        EnsureTransparentMaterial();
        materials = new Material[count];
        for (int i = 0; i < count; i++)
        {
            materials[i] = transparentMaterial;
        }

        transparentMaterialArrays[count] = materials;
        return materials;
    }

    private void EnsureTransparentMaterial()
    {
        if (transparentMaterial != null)
        {
            ApplyMaterialColor();
            return;
        }

        Shader shader = Shader.Find("Legacy Shaders/Transparent/Diffuse");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Transparent");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        transparentMaterial = new Material(shader)
        {
            name = "Obstacle Visualization Transparent Material",
            renderQueue = (int)RenderQueue.Transparent
        };
        ApplyMaterialColor();
    }

    private void ApplyMaterialColor()
    {
        if (transparentMaterial == null)
        {
            return;
        }

        Color color = transparentTint;
        color.a = Mathf.Clamp01(transparentAlpha);
        if (transparentMaterial.HasProperty("_Color"))
        {
            transparentMaterial.SetColor("_Color", color);
        }

        if (transparentMaterial.HasProperty("_Mode"))
        {
            transparentMaterial.SetFloat("_Mode", 3f);
            transparentMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            transparentMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            transparentMaterial.SetInt("_ZWrite", 0);
            transparentMaterial.DisableKeyword("_ALPHATEST_ON");
            transparentMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            transparentMaterial.EnableKeyword("_ALPHABLEND_ON");
            transparentMaterial.renderQueue = (int)RenderQueue.Transparent;
        }
    }

    private sealed class RendererState
    {
        public Renderer renderer;
        public Material[] originalSharedMaterials;
        public ShadowCastingMode shadowCastingMode;
        public bool receiveShadows;
    }
}
