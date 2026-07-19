using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace MochiPark.Runtime.Views
{
    [DisallowMultipleComponent]
    public sealed class ScreenOverlayCameraController : MonoBehaviour
    {
        private const string OverlayCameraName = "Screen Overlay Camera";
        private const string ScreenOverlayLayerName = "ScreenOverlay";
        private const float DefaultDestroyLingerDuration = 10f;
        private static readonly Vector3 OverlayCameraPosition = new Vector3(0f, 0f, -20f);

        [SerializeField] private Camera overlayCamera;
        [SerializeField] private float orthographicSize = 5f;
        [SerializeField] private float nearClipPlane = 0.3f;
        [SerializeField] private float farClipPlane = 30f;

        private readonly List<Coroutine> lifetimeCoroutines = new List<Coroutine>();
        private Camera baseCamera;
        private UniversalAdditionalCameraData baseCameraData;
        private bool ownsStackEntry;

        public Camera OverlayCamera => overlayCamera;
        public Transform EffectsRoot => transform;

        public bool Initialize(Camera targetBaseCamera)
        {
            baseCamera = targetBaseCamera != null ? targetBaseCamera : Camera.main;
            if (baseCamera == null)
            {
                Debug.LogWarning($"{nameof(ScreenOverlayCameraController)} cannot initialize without a base camera.", this);
                return false;
            }

            EnsureOverlayCamera();
            ConfigureOverlayCamera();
            AddOverlayCameraToBaseStack();
            return overlayCamera != null;
        }

        public GameObject PlayTimed(
            GameObject prefab,
            float activeDuration,
            float destroyLingerDuration = DefaultDestroyLingerDuration,
            bool centerOnCamera = true,
            bool useUnscaledTime = true)
        {
            GameObject instance = Spawn(prefab, centerOnCamera);
            if (instance == null)
            {
                return null;
            }

            Coroutine coroutine = StartCoroutine(StopAndDestroyRoutine(
                instance,
                Mathf.Max(0f, activeDuration),
                Mathf.Max(0f, destroyLingerDuration),
                useUnscaledTime));
            lifetimeCoroutines.Add(coroutine);
            return instance;
        }

        public GameObject Spawn(GameObject prefab, bool centerOnCamera = true)
        {
            if (prefab == null)
            {
                return null;
            }

            if (overlayCamera == null && !Initialize(baseCamera))
            {
                return null;
            }

            GameObject instance = Instantiate(prefab, EffectsRoot, false);
            instance.name = prefab.name;
            if (centerOnCamera)
            {
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
            }

            SetLayerRecursively(instance, GetScreenOverlayLayer());
            DisableEmbeddedCameras(instance);
            return instance;
        }

        public void Clear(GameObject instance, bool destroyImmediately)
        {
            if (instance == null)
            {
                return;
            }

            if (destroyImmediately)
            {
                Destroy(instance);
                return;
            }

            StopParticles(instance);
            Destroy(instance, DefaultDestroyLingerDuration);
        }

        public void ClearEffects(bool destroyImmediately)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (overlayCamera != null && child == overlayCamera.transform)
                {
                    continue;
                }

                if (destroyImmediately)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    StopParticles(child.gameObject);
                    Destroy(child.gameObject, DefaultDestroyLingerDuration);
                }
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < lifetimeCoroutines.Count; i++)
            {
                if (lifetimeCoroutines[i] != null)
                {
                    StopCoroutine(lifetimeCoroutines[i]);
                }
            }

            lifetimeCoroutines.Clear();
            RemoveOverlayCameraFromBaseStack();
        }

        private void EnsureOverlayCamera()
        {
            if (overlayCamera != null)
            {
                return;
            }

            Transform existing = transform.Find(OverlayCameraName);
            if (existing != null)
            {
                overlayCamera = existing.GetComponent<Camera>();
            }

            if (overlayCamera != null)
            {
                return;
            }

            GameObject cameraObject = new GameObject(OverlayCameraName);
            cameraObject.transform.SetParent(transform, false);
            overlayCamera = cameraObject.AddComponent<Camera>();
        }

        private void ConfigureOverlayCamera()
        {
            if (overlayCamera == null)
            {
                return;
            }

            overlayCamera.tag = "Untagged";
            overlayCamera.transform.SetParent(transform, false);
            overlayCamera.transform.localPosition = OverlayCameraPosition;
            overlayCamera.transform.localRotation = Quaternion.identity;
            overlayCamera.transform.localScale = Vector3.one;
            overlayCamera.clearFlags = CameraClearFlags.Nothing;
            overlayCamera.orthographic = true;
            overlayCamera.orthographicSize = Mathf.Max(0.01f, orthographicSize);
            overlayCamera.nearClipPlane = nearClipPlane;
            overlayCamera.farClipPlane = farClipPlane;
            overlayCamera.cullingMask = 1 << GetScreenOverlayLayer();
            overlayCamera.enabled = true;

            UniversalAdditionalCameraData overlayCameraData =
                overlayCamera.GetComponent<UniversalAdditionalCameraData>();
            if (overlayCameraData == null)
            {
                overlayCameraData = overlayCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            }

            overlayCameraData.renderType = CameraRenderType.Overlay;
        }

        private void AddOverlayCameraToBaseStack()
        {
            if (baseCamera == null || overlayCamera == null)
            {
                return;
            }

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

        private static IEnumerator WaitForDuration(float duration, bool useUnscaledTime)
        {
            if (duration <= 0f)
            {
                yield break;
            }

            if (useUnscaledTime)
            {
                yield return new WaitForSecondsRealtime(duration);
            }
            else
            {
                yield return new WaitForSeconds(duration);
            }
        }

        private IEnumerator StopAndDestroyRoutine(
            GameObject instance,
            float activeDuration,
            float destroyLingerDuration,
            bool useUnscaledTime)
        {
            yield return WaitForDuration(activeDuration, useUnscaledTime);

            StopParticles(instance);

            yield return WaitForDuration(destroyLingerDuration, useUnscaledTime);

            if (instance != null)
            {
                Destroy(instance);
            }
        }

        private static void StopParticles(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particleSystems.Length; i++)
            {
                if (particleSystems[i] != null)
                {
                    particleSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }
        }

        private static void DisableEmbeddedCameras(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            Camera[] cameras = instance.GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] == null)
                {
                    continue;
                }

                cameras[i].tag = "Untagged";
                cameras[i].enabled = false;
            }
        }

        private static int GetScreenOverlayLayer()
        {
            int layer = LayerMask.NameToLayer(ScreenOverlayLayerName);
            return layer >= 0 ? layer : 0;
        }

        private static void SetLayerRecursively(GameObject target, int layer)
        {
            if (target == null)
            {
                return;
            }

            target.layer = layer;
            Transform targetTransform = target.transform;
            for (int i = 0; i < targetTransform.childCount; i++)
            {
                SetLayerRecursively(targetTransform.GetChild(i).gameObject, layer);
            }
        }
    }
}
