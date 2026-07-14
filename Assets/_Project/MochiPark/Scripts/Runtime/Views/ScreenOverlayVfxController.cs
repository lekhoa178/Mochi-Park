using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace MochiPark.Runtime.Views
{
    public sealed class ScreenOverlayVfxController : MonoBehaviour
    {
        private const string FlashLeftName = "FlashLeft";
        private const string FlashRightName = "FlashRight";
        private const string VignetteName = "Vignette";
        private static readonly int OverallOpacityPropertyId = Shader.PropertyToID("_Alpha");
        private const float DefaultDepthFromCamera = 20f;

        [SerializeField] private float fadeInDuration = 0.35f;
        [SerializeField] private float fadeOutDuration = 0.45f;
        [SerializeField] private float destroyLingerDuration = 10f;

        private Camera baseCamera;
        private Camera overlayCamera;
        private UniversalAdditionalCameraData baseCameraData;
        private Transform flashLeft;
        private Transform flashRight;
        private ParticleSystem[] particleSystems;
        private Renderer vignetteRenderer;
        private Material vignetteMaterial;
        private Coroutine opacityCoroutine;
        private Coroutine destroyCoroutine;
        private float flashLeftDepth = DefaultDepthFromCamera;
        private float flashRightDepth = DefaultDepthFromCamera;
        private float flashLeftViewportY = 0.5f;
        private float flashRightViewportY = 0.5f;
        private bool ownsStackEntry;
        private bool ownsVignetteMaterial;
        private bool usesSharedOverlayCamera;

        public void Initialize(Camera targetBaseCamera)
        {
            Initialize(targetBaseCamera, null);
        }

        public void Initialize(Camera targetBaseCamera, Camera targetOverlayCamera)
        {
            baseCamera = targetBaseCamera != null ? targetBaseCamera : Camera.main;
            overlayCamera = targetOverlayCamera != null ? targetOverlayCamera : GetComponentInChildren<Camera>(true);
            usesSharedOverlayCamera = targetOverlayCamera != null;
            if (baseCamera == null || overlayCamera == null)
            {
                Debug.LogWarning(
                    $"{nameof(ScreenOverlayVfxController)} on '{name}' needs a base camera and an overlay camera.",
                    this);
                return;
            }

            if (usesSharedOverlayCamera)
            {
                DisableEmbeddedCameras();
            }
            else
            {
                ConfigureOverlayCamera();
                AddOverlayCameraToBaseStack();
            }

            CacheVfx();
            CacheSideAnchors();
            UpdateSideAnchors();
        }

        public void BeginFrenzy()
        {
            CancelDestroy();
            CacheVfx();
            SetVignetteOpacity(0f);
            PlayParticles();
            TweenVignetteOpacity(1f, fadeInDuration, null);
        }

        public void EndFrenzyAndDestroy()
        {
            EndFrenzyAndDestroy(destroyLingerDuration);
        }

        public void EndFrenzyAndDestroy(float lingerDuration)
        {
            CacheVfx();
            StopParticles();
            SetVignetteOpacity(1f);
            TweenVignetteOpacity(0f, fadeOutDuration, () => StartDestroyAfterLinger(lingerDuration));
        }

        public void DestroyImmediately()
        {
            CancelDestroy();
            if (gameObject != null)
            {
                Destroy(gameObject);
            }
        }

        private void LateUpdate()
        {
            UpdateSideAnchors();
        }

        private void OnDestroy()
        {
            StopRunningCoroutines();
            RemoveOverlayCameraFromBaseStack();

            if (ownsVignetteMaterial && vignetteMaterial != null)
            {
                Destroy(vignetteMaterial);
                vignetteMaterial = null;
                ownsVignetteMaterial = false;
            }
        }

        private void ConfigureOverlayCamera()
        {
            overlayCamera.tag = "Untagged";

            UniversalAdditionalCameraData overlayCameraData =
                overlayCamera.GetComponent<UniversalAdditionalCameraData>();
            if (overlayCameraData == null)
            {
                overlayCameraData = overlayCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            }

            overlayCameraData.renderType = CameraRenderType.Overlay;
        }

        private void DisableEmbeddedCameras()
        {
            Camera[] cameras = GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] == null || cameras[i] == overlayCamera)
                {
                    continue;
                }

                cameras[i].tag = "Untagged";
                cameras[i].enabled = false;
            }
        }

        private void AddOverlayCameraToBaseStack()
        {
            baseCameraData = baseCamera.GetComponent<UniversalAdditionalCameraData>();
            if (baseCameraData == null)
            {
                baseCameraData = baseCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            }

            baseCameraData.renderType = CameraRenderType.Base;
            List<Camera> cameraStack = baseCameraData.cameraStack;
            if (cameraStack == null || cameraStack.Contains(overlayCamera))
            {
                return;
            }

            cameraStack.Add(overlayCamera);
            ownsStackEntry = true;
        }

        private void RemoveOverlayCameraFromBaseStack()
        {
            if (!ownsStackEntry || baseCameraData == null || overlayCamera == null)
            {
                return;
            }

            List<Camera> cameraStack = baseCameraData.cameraStack;
            if (cameraStack != null)
            {
                cameraStack.Remove(overlayCamera);
            }

            ownsStackEntry = false;
        }

        private void CacheSideAnchors()
        {
            flashLeft = FindChildByName(transform, FlashLeftName);
            flashRight = FindChildByName(transform, FlashRightName);
            flashLeftDepth = GetDepthFromCamera(flashLeft);
            flashRightDepth = GetDepthFromCamera(flashRight);
            flashLeftViewportY = GetViewportY(flashLeft);
            flashRightViewportY = GetViewportY(flashRight);
        }

        private void CacheVfx()
        {
            particleSystems ??= GetComponentsInChildren<ParticleSystem>(true);
            if (vignetteRenderer != null && vignetteMaterial != null)
            {
                return;
            }

            Transform vignette = FindChildByName(transform, VignetteName);
            vignetteRenderer = vignette != null
                ? vignette.GetComponent<Renderer>()
                : GetComponentInChildren<Renderer>(true);
            if (vignetteRenderer == null)
            {
                return;
            }

            vignetteMaterial = vignetteRenderer.material;
            ownsVignetteMaterial = vignetteMaterial != null;
        }

        private void PlayParticles()
        {
            if (particleSystems == null)
            {
                return;
            }

            foreach (ParticleSystem particleSystem in particleSystems)
            {
                if (particleSystem == null)
                {
                    continue;
                }

                particleSystem.Play(true);
            }
        }

        private void StopParticles()
        {
            if (particleSystems == null)
            {
                return;
            }

            foreach (ParticleSystem particleSystem in particleSystems)
            {
                if (particleSystem == null)
                {
                    continue;
                }

                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private void TweenVignetteOpacity(float targetOpacity, float duration, System.Action completed)
        {
            if (opacityCoroutine != null)
            {
                StopCoroutine(opacityCoroutine);
                opacityCoroutine = null;
            }

            if (vignetteMaterial == null || !vignetteMaterial.HasProperty(OverallOpacityPropertyId))
            {
                completed?.Invoke();
                return;
            }

            opacityCoroutine = StartCoroutine(TweenVignetteOpacityRoutine(targetOpacity, duration, completed));
        }

        private IEnumerator TweenVignetteOpacityRoutine(float targetOpacity, float duration, System.Action completed)
        {
            float startOpacity = vignetteMaterial.GetFloat(OverallOpacityPropertyId);
            float elapsed = 0f;
            duration = Mathf.Max(0f, duration);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
                SetVignetteOpacity(Mathf.Lerp(startOpacity, targetOpacity, t));
                yield return null;
            }

            SetVignetteOpacity(targetOpacity);
            opacityCoroutine = null;
            completed?.Invoke();
        }

        private void SetVignetteOpacity(float opacity)
        {
            if (vignetteMaterial == null || !vignetteMaterial.HasProperty(OverallOpacityPropertyId))
            {
                return;
            }

            vignetteMaterial.SetFloat(OverallOpacityPropertyId, Mathf.Clamp01(opacity));
        }

        private void StartDestroyAfterLinger(float lingerDuration)
        {
            CancelDestroy();
            destroyCoroutine = StartCoroutine(DestroyAfterLingerRoutine(Mathf.Max(0f, lingerDuration)));
        }

        private IEnumerator DestroyAfterLingerRoutine(float lingerDuration)
        {
            if (lingerDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(lingerDuration);
            }

            destroyCoroutine = null;
            Destroy(gameObject);
        }

        private void CancelDestroy()
        {
            if (destroyCoroutine == null)
            {
                return;
            }

            StopCoroutine(destroyCoroutine);
            destroyCoroutine = null;
        }

        private void StopRunningCoroutines()
        {
            if (opacityCoroutine != null)
            {
                StopCoroutine(opacityCoroutine);
                opacityCoroutine = null;
            }

            CancelDestroy();
        }

        private void UpdateSideAnchors()
        {
            if (overlayCamera == null)
            {
                return;
            }

            PositionSideAnchor(flashLeft, 0f, flashLeftViewportY, flashLeftDepth);
            PositionSideAnchor(flashRight, 1f, flashRightViewportY, flashRightDepth);
        }

        private void PositionSideAnchor(Transform anchor, float viewportX, float viewportY, float depthFromCamera)
        {
            if (anchor == null)
            {
                return;
            }

            Vector3 viewportPosition = new Vector3(viewportX, viewportY, Mathf.Max(0.01f, depthFromCamera));
            anchor.position = overlayCamera.ViewportToWorldPoint(viewportPosition);
        }

        private float GetDepthFromCamera(Transform anchor)
        {
            if (anchor == null || overlayCamera == null)
            {
                return DefaultDepthFromCamera;
            }

            Vector3 cameraSpacePosition = overlayCamera.transform.InverseTransformPoint(anchor.position);
            return Mathf.Max(0.01f, cameraSpacePosition.z);
        }

        private float GetViewportY(Transform anchor)
        {
            if (anchor == null || overlayCamera == null)
            {
                return 0.5f;
            }

            return overlayCamera.WorldToViewportPoint(anchor.position).y;
        }

        private static Transform FindChildByName(Transform root, string targetName)
        {
            if (root == null)
            {
                return null;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == targetName)
                {
                    return child;
                }

                Transform nestedMatch = FindChildByName(child, targetName);
                if (nestedMatch != null)
                {
                    return nestedMatch;
                }
            }

            return null;
        }
    }
}
