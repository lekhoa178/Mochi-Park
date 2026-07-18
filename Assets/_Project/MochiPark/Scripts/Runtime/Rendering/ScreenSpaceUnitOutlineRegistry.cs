using System.Collections.Generic;
using UnityEngine;

namespace MochiPark.Runtime.Rendering
{
    public enum ScreenSpaceUnitOutlineStyle
    {
        Selected,
        Hovered
    }

    /// <summary>
    /// Keeps the small set of renderers that the outline render pass should draw.
    /// The registry never changes a renderer's materials, layer, or rendering-layer mask.
    /// </summary>
    public static class ScreenSpaceUnitOutlineRegistry
    {
        private sealed class Entry
        {
            public GameObject Root;
            public ScreenSpaceUnitOutlineStyle Style;
            public RendererInfo[] Renderers;
        }

        private readonly struct RendererInfo
        {
            public RendererInfo(Renderer renderer)
            {
                Renderer = renderer;
                SubMeshCount = renderer != null ? Mathf.Max(1, renderer.sharedMaterials.Length) : 0;
            }

            public Renderer Renderer { get; }
            public int SubMeshCount { get; }
        }

        internal readonly struct DrawTarget
        {
            public DrawTarget(Renderer renderer, int subMeshCount, ScreenSpaceUnitOutlineStyle style)
            {
                Renderer = renderer;
                SubMeshCount = subMeshCount;
                Style = style;
            }

            public Renderer Renderer { get; }
            public int SubMeshCount { get; }
            public ScreenSpaceUnitOutlineStyle Style { get; }
        }

        private static readonly Dictionary<GameObject, Entry> Entries = new Dictionary<GameObject, Entry>();
        private static readonly List<GameObject> StaleEntryRoots = new List<GameObject>();

        public static void SetHighlighted(GameObject root, ScreenSpaceUnitOutlineStyle style)
        {
            if (root == null)
            {
                return;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            RendererInfo[] rendererInfos = new RendererInfo[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                rendererInfos[i] = new RendererInfo(renderers[i]);
            }

            Entries[root] = new Entry
            {
                Root = root,
                Style = style,
                Renderers = rendererInfos
            };
        }

        public static void Remove(GameObject root)
        {
            if (root != null)
            {
                Entries.Remove(root);
            }
        }

        internal static bool CopyDrawTargets(List<DrawTarget> output)
        {
            output.Clear();
            StaleEntryRoots.Clear();

            foreach (KeyValuePair<GameObject, Entry> pair in Entries)
            {
                Entry entry = pair.Value;
                if (entry.Root == null)
                {
                    StaleEntryRoots.Add(pair.Key);
                    continue;
                }

                RendererInfo[] renderers = entry.Renderers;
                for (int i = 0; i < renderers.Length; i++)
                {
                    RendererInfo rendererInfo = renderers[i];
                    Renderer renderer = rendererInfo.Renderer;
                    if (renderer == null ||
                        !renderer.enabled ||
                        renderer.forceRenderingOff ||
                        !renderer.gameObject.activeInHierarchy ||
                        renderer is ParticleSystemRenderer ||
                        renderer is TrailRenderer ||
                        renderer is LineRenderer)
                    {
                        continue;
                    }

                    output.Add(new DrawTarget(renderer, rendererInfo.SubMeshCount, entry.Style));
                }
            }

            for (int i = 0; i < StaleEntryRoots.Count; i++)
            {
                Entries.Remove(StaleEntryRoots[i]);
            }

            return output.Count > 0;
        }
    }
}
