using System.Linq;
using MochiPark.Runtime.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace MochiPark.Editor
{
    public static class ScreenSpaceUnitOutlineInstaller
    {
        private static readonly string[] RendererDataPaths =
        {
            "Assets/Settings/PC_Renderer.asset",
            "Assets/Settings/Mobile_Renderer.asset"
        };

        [MenuItem("Mochi Park/Rendering/Install Screen-Space Unit Outline")]
        public static void Install()
        {
            bool changed = false;
            for (int i = 0; i < RendererDataPaths.Length; i++)
            {
                string path = RendererDataPaths[i];
                ScriptableRendererData rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(path);
                if (rendererData == null)
                {
                    Debug.LogWarning($"Could not install the unit outline renderer feature: '{path}' was not found.");
                    continue;
                }

                ScreenSpaceUnitOutlineFeature existingFeature = rendererData.rendererFeatures
                    .OfType<ScreenSpaceUnitOutlineFeature>()
                    .FirstOrDefault();
                if (existingFeature != null)
                {
                    existingFeature.SetActive(true);
                    EditorUtility.SetDirty(existingFeature);
                    continue;
                }

                ScreenSpaceUnitOutlineFeature feature =
                    ScriptableObject.CreateInstance<ScreenSpaceUnitOutlineFeature>();
                feature.name = "Mochi Screen-Space Unit Outline";
                feature.SetActive(true);
                AssetDatabase.AddObjectToAsset(feature, rendererData);
                rendererData.rendererFeatures.Add(feature);
                rendererData.SetDirty();
                EditorUtility.SetDirty(rendererData);
                changed = true;
            }

            if (changed)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }
    }
}
