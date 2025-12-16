using UnityEngine;
using System.Collections.Generic;

public class ShadowMaterialSwapper : MonoBehaviour
{
    [SerializeField] private Material depthMaterial;

    // ======================================================
    // Cached state
    // ======================================================
    private Renderer[] renderers;
    private List<Material[]> originalMats = new List<Material[]>();
    private bool cached = false;

    // ======================================================
    // Cache
    // ======================================================
    private void CacheIfNeeded()
    {
        if (cached)
            return;

        renderers = FindObjectsOfType<Renderer>();
        originalMats.Clear();

        foreach (var r in renderers)
        {
            originalMats.Add(r.sharedMaterials);
        }

        cached = true;
    }

    // ======================================================
    // Depth override
    // ======================================================
    public void BeginDepthOverride()
    {
        if (depthMaterial == null)
        {
            Debug.LogError("ShadowMaterialSwapper: depthMaterial is null!");
            return;
        }

        CacheIfNeeded();

        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null)
                continue;

            int count = originalMats[i].Length;
            Material[] depthMats = new Material[count];

            for (int j = 0; j < count; j++)
            {
                depthMats[j] = depthMaterial;
            }

            r.sharedMaterials = depthMats;
        }
    }

    // ======================================================
    // Restore
    // ======================================================
    public void EndDepthOverride()
    {
        if (!cached)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null)
                continue;

            r.sharedMaterials = originalMats[i];
        }
    }
}
