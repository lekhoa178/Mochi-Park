using System.Collections;
using MochiPark.Runtime.Board;
using UnityEngine;

namespace MochiPark.Runtime.Views
{
    public sealed class CameraController : MonoBehaviour
    {
        private const float CameraBackingDistance = 200f;
        // x = top, y = bottom, z = left, w = right.
        private static readonly Vector4 EditCameraBoundaryPadding = new Vector4(5f, 3f, 3f, 3f);
        private static readonly Vector4 GameplayCameraBoundaryPadding = new Vector4(1f, 1f, 3f, 3f);
        private const float NearClipPlane = 3f;
        private const float FarClipPlane = 1000f;
        private const float ParkOpenTweenDuration = 0.8f;
        private static readonly Vector3 EditCameraEuler = new Vector3(90f, 0f, 0f);
        private static readonly Vector3 ParkOpenCameraEuler = new Vector3(30f, 45f, 0f);

        private Camera mainCamera;

        public void Initialize()
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                GameObject cameraObject = new GameObject("Mochi Park Camera");
                mainCamera = cameraObject.AddComponent<Camera>();
                mainCamera.tag = "MainCamera";
            }

            mainCamera.nearClipPlane = NearClipPlane;
            mainCamera.farClipPlane = FarClipPlane;
        }

        public void FrameBoard(RuntimeBoardState board)
        {
            if (mainCamera == null)
            {
                Initialize();
            }

            mainCamera.orthographic = true;
            BuildEditView(board, out Vector3 targetPosition, out Quaternion targetRotation, out float targetSize);
            mainCamera.transform.position = targetPosition;
            mainCamera.transform.rotation = targetRotation;
            mainCamera.orthographicSize = targetSize;
        }

        public void FrameParkOpenView(RuntimeBoardState board)
        {
            if (mainCamera == null)
            {
                Initialize();
            }

            if (mainCamera == null || board == null)
            {
                return;
            }

            mainCamera.orthographic = true;
            Quaternion targetRotation = Quaternion.Euler(ParkOpenCameraEuler);
            BuildBoardView(board, targetRotation, GameplayCameraBoundaryPadding, out Vector3 targetViewCenter, out float targetSize);
            mainCamera.transform.position = AddOrthographicBacking(targetViewCenter, targetRotation);
            mainCamera.transform.rotation = targetRotation;
            mainCamera.orthographicSize = targetSize;
        }

        public IEnumerator TweenToParkOpenView(RuntimeBoardState board)
        {
            yield return TweenToBoardView(board, Quaternion.Euler(ParkOpenCameraEuler), GameplayCameraBoundaryPadding);
        }

        public IEnumerator TweenToEditView(RuntimeBoardState board)
        {
            yield return TweenToBoardView(board, Quaternion.Euler(EditCameraEuler), EditCameraBoundaryPadding);
        }

        private IEnumerator TweenToBoardView(RuntimeBoardState board, Quaternion targetRotation, Vector4 padding)
        {
            if (mainCamera == null)
            {
                Initialize();
            }

            if (mainCamera == null || board == null)
            {
                yield break;
            }

            BuildBoardView(board, targetRotation, padding, out Vector3 targetViewCenter, out float targetSize);
            Quaternion startRotation = mainCamera.transform.rotation;
            float startSize = mainCamera.orthographicSize;

            float elapsed = 0f;
            while (elapsed < ParkOpenTweenDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / ParkOpenTweenDuration);
                float eased = Mathf.SmoothStep(0f, 1f, t);

                Quaternion rotation = Quaternion.Slerp(startRotation, targetRotation, eased);
                mainCamera.transform.position = AddOrthographicBacking(targetViewCenter, rotation);
                mainCamera.transform.rotation = rotation;
                mainCamera.orthographicSize = Mathf.Lerp(startSize, targetSize, eased);
                yield return null;
            }

            mainCamera.transform.position = AddOrthographicBacking(targetViewCenter, targetRotation);
            mainCamera.transform.rotation = targetRotation;
            mainCamera.orthographicSize = targetSize;
        }

        public bool TryGetScreenRay(Vector2 screenPosition, out Ray ray)
        {
            if (mainCamera == null)
            {
                Initialize();
            }

            if (mainCamera == null)
            {
                ray = default;
                return false;
            }

            ray = mainCamera.ScreenPointToRay(screenPosition);
            return true;
        }

        public bool TryScreenToBoardCell(
            Vector2 screenPosition,
            RuntimeBoardState board,
            out Vector2Int cell,
            out Vector3 surfacePoint)
        {
            cell = default;
            surfacePoint = default;

            if (!TryGetScreenRay(screenPosition, out Ray ray))
            {
                return false;
            }

            Plane boardPlane = new Plane(Vector3.up, new Vector3(0f, board.TileSurfaceY, 0f));
            if (!boardPlane.Raycast(ray, out float distance))
            {
                return false;
            }

            surfacePoint = ray.GetPoint(distance);
            cell = new Vector2Int(
                Mathf.RoundToInt(surfacePoint.x / board.TileSizeXZ),
                Mathf.RoundToInt(surfacePoint.z / board.TileSizeXZ));
            return true;
        }

        private static Vector3 GetBoardCenter(RuntimeBoardState board)
        {
            return new Vector3(
                (board.Width - 1) * board.TileSizeXZ * 0.5f,
                board.TileSurfaceY,
                (board.Height - 1) * board.TileSizeXZ * 0.5f);
        }

        private void BuildBoardView(
            RuntimeBoardState board,
            Quaternion cameraRotation,
            Vector4 padding,
            out Vector3 viewCenter,
            out float orthographicSize)
        {
            Vector3 boardCenter = GetBoardCenter(board);
            float minX = -board.TileSizeXZ * 0.5f;
            float maxX = (board.Width - 0.5f) * board.TileSizeXZ;
            float minZ = -board.TileSizeXZ * 0.5f;
            float maxZ = (board.Height - 0.5f) * board.TileSizeXZ;
            Quaternion inverseRotation = Quaternion.Inverse(cameraRotation);

            Vector3[] corners =
            {
                new Vector3(minX, board.TileSurfaceY, minZ),
                new Vector3(minX, board.TileSurfaceY, maxZ),
                new Vector3(maxX, board.TileSurfaceY, minZ),
                new Vector3(maxX, board.TileSurfaceY, maxZ)
            };

            float viewMinX = float.PositiveInfinity;
            float viewMaxX = float.NegativeInfinity;
            float viewMinY = float.PositiveInfinity;
            float viewMaxY = float.NegativeInfinity;
            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 cameraSpace = inverseRotation * (corners[i] - boardCenter);
                viewMinX = Mathf.Min(viewMinX, cameraSpace.x);
                viewMaxX = Mathf.Max(viewMaxX, cameraSpace.x);
                viewMinY = Mathf.Min(viewMinY, cameraSpace.y);
                viewMaxY = Mathf.Max(viewMaxY, cameraSpace.y);
            }

            viewMinX -= padding.z;
            viewMaxX += padding.w;
            viewMinY -= padding.y;
            viewMaxY += padding.x;

            Vector2 viewCenterOffset = new Vector2(
                (viewMinX + viewMaxX) * 0.5f,
                (viewMinY + viewMaxY) * 0.5f);

            float aspect = mainCamera != null && mainCamera.aspect > 0f ? mainCamera.aspect : 16f / 9f;
            float verticalSize = (viewMaxY - viewMinY) * 0.5f;
            float horizontalSize = (viewMaxX - viewMinX) * 0.5f / aspect;

            viewCenter = boardCenter + cameraRotation * new Vector3(viewCenterOffset.x, viewCenterOffset.y, 0f);
            orthographicSize = Mathf.Max(verticalSize, horizontalSize);
        }

        private void BuildEditView(
            RuntimeBoardState board,
            out Vector3 targetPosition,
            out Quaternion targetRotation,
            out float targetSize)
        {
            targetRotation = Quaternion.Euler(EditCameraEuler);
            BuildBoardView(board, targetRotation, EditCameraBoundaryPadding, out Vector3 targetViewCenter, out targetSize);
            targetPosition = AddOrthographicBacking(targetViewCenter, targetRotation);
        }

        private static Vector3 AddOrthographicBacking(Vector3 viewPosition, Quaternion viewRotation)
        {
            return viewPosition + viewRotation * Vector3.back * CameraBackingDistance;
        }
    }
}
