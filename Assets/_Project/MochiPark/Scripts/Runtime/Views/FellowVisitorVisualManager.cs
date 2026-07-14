using System;
using System.Collections.Generic;
using UnityEngine;

namespace MochiPark.Runtime.Views
{
    public sealed class FellowVisitorVisualManager
    {
        private const string BodyBoneName = "body";

        private readonly Transform root;
        private readonly Func<string> idleStateNameResolver;
        private readonly Func<float> crossFadeDurationResolver;
        private readonly Dictionary<string, VisitorFellowStack> stacksByVisitorId =
            new Dictionary<string, VisitorFellowStack>();

        private int maxFellowCount;
        private float stackOffset;
        private float fellowScale;

        public FellowVisitorVisualManager(
            Transform root,
            int maxFellowCount,
            float stackOffset,
            float fellowScale,
            Func<string> idleStateNameResolver,
            Func<float> crossFadeDurationResolver)
        {
            this.root = root;
            this.idleStateNameResolver = idleStateNameResolver;
            this.crossFadeDurationResolver = crossFadeDurationResolver;
            Configure(maxFellowCount, stackOffset, fellowScale);
        }

        public void Configure(int maxFellowCount, float stackOffset, float fellowScale)
        {
            this.maxFellowCount = Mathf.Max(0, maxFellowCount);
            this.stackOffset = stackOffset;
            this.fellowScale = Mathf.Max(0.01f, fellowScale);
        }

        public void SetFellowCount(string visitorId, GameObject visitorView, int requestedCount)
        {
            if (string.IsNullOrWhiteSpace(visitorId) || visitorView == null || root == null)
            {
                return;
            }

            int targetCount = Mathf.Clamp(requestedCount, 0, maxFellowCount);
            if (targetCount == 0)
            {
                RemoveVisitor(visitorId);
                return;
            }

            if (!stacksByVisitorId.TryGetValue(visitorId, out VisitorFellowStack stack))
            {
                if (!TryFindBodyBone(visitorView.transform, out Transform originalBody))
                {
                    Debug.LogError(
                        $"{nameof(FellowVisitorVisualManager)} could not find a '{BodyBoneName}' bone under visitor '{visitorId}'. Fellows cannot be displayed.",
                        visitorView);
                    return;
                }

                stack = new VisitorFellowStack(visitorView, originalBody);
                stacksByVisitorId[visitorId] = stack;
            }

            while (stack.Fellows.Count > targetCount)
            {
                DestroyFellow(stack.Fellows[stack.Fellows.Count - 1]);
                stack.Fellows.RemoveAt(stack.Fellows.Count - 1);
            }

            while (stack.Fellows.Count < targetCount)
            {
                Transform targetBody = stack.Fellows.Count == 0
                    ? stack.OriginalBody
                    : stack.Fellows[stack.Fellows.Count - 1].Body;

                if (targetBody == null || !TryCreateFellow(visitorId, stack.VisitorView, targetBody, out FellowVisual fellow))
                {
                    break;
                }

                stack.Fellows.Add(fellow);
            }

            UpdateStack(stack);
        }

        public void RemoveVisitor(string visitorId)
        {
            if (string.IsNullOrWhiteSpace(visitorId) ||
                !stacksByVisitorId.TryGetValue(visitorId, out VisitorFellowStack stack))
            {
                return;
            }

            for (int i = stack.Fellows.Count - 1; i >= 0; i--)
            {
                DestroyFellow(stack.Fellows[i]);
            }

            stacksByVisitorId.Remove(visitorId);
        }

        public void Clear()
        {
            foreach (VisitorFellowStack stack in stacksByVisitorId.Values)
            {
                for (int i = stack.Fellows.Count - 1; i >= 0; i--)
                {
                    DestroyFellow(stack.Fellows[i]);
                }
            }

            stacksByVisitorId.Clear();
        }

        public void LateUpdateFellows()
        {
            foreach (VisitorFellowStack stack in stacksByVisitorId.Values)
            {
                UpdateStack(stack);
            }
        }

        private bool TryCreateFellow(
            string visitorId,
            GameObject visitorView,
            Transform targetBody,
            out FellowVisual fellow)
        {
            fellow = null;
            Transform sourceVisual = GetSourceVisualRoot(visitorView);
            if (sourceVisual == null)
            {
                Debug.LogError(
                    $"{nameof(FellowVisitorVisualManager)} could not find a visual model under visitor '{visitorId}'. Fellows cannot be displayed.",
                    visitorView);
                return false;
            }

            GameObject fellowRoot = new GameObject($"{visitorId}_fellow_{targetBody.GetEntityId()}");
            fellowRoot.transform.SetParent(root, false);
            fellowRoot.transform.localScale = Vector3.one * fellowScale;

            GameObject visual = UnityEngine.Object.Instantiate(sourceVisual.gameObject, fellowRoot.transform, false);
            visual.name = sourceVisual.name;
            StripNonVisualComponents(visual);
            DisableRootMotion(visual);

            if (!TryFindBodyBone(fellowRoot.transform, out Transform fellowBody))
            {
                Debug.LogError(
                    $"{nameof(FellowVisitorVisualManager)} could not find a '{BodyBoneName}' bone in spawned fellow '{fellowRoot.name}'. Destroying fellow visual.",
                    fellowRoot);
                UnityEngine.Object.Destroy(fellowRoot);
                return false;
            }

            PlayIdle(visual);
            fellow = new FellowVisual(fellowRoot, targetBody, fellowBody);
            UpdateFellow(fellow, targetBody);
            return true;
        }

        private void UpdateStack(VisitorFellowStack stack)
        {
            if (stack == null || stack.OriginalBody == null)
            {
                return;
            }

            for (int i = 0; i < stack.Fellows.Count; i++)
            {
                FellowVisual fellow = stack.Fellows[i];
                if (fellow == null ||
                    fellow.Root == null ||
                    fellow.FollowTargetBody == null ||
                    fellow.Body == null)
                {
                    return;
                }

                UpdateFellow(fellow, fellow.FollowTargetBody);
            }
        }

        private void UpdateFellow(FellowVisual fellow, Transform targetBody)
        {
            Quaternion targetRotation = targetBody.rotation;
            fellow.Root.transform.SetPositionAndRotation(
                targetBody.position + targetRotation * (Vector3.up * stackOffset),
                targetRotation);
        }

        private void PlayIdle(GameObject visual)
        {
            Animator animator = visual.GetComponentInChildren<Animator>(true);
            string idleStateName = idleStateNameResolver != null ? idleStateNameResolver() : "Idle";
            if (animator == null || string.IsNullOrWhiteSpace(idleStateName))
            {
                return;
            }

            animator.CrossFade(idleStateName, crossFadeDurationResolver != null ? crossFadeDurationResolver() : 0.1f);
        }

        private static Transform GetSourceVisualRoot(GameObject visitorView)
        {
            if (visitorView == null)
            {
                return null;
            }

            return visitorView.transform.childCount > 0
                ? visitorView.transform.GetChild(0)
                : visitorView.transform;
        }

        private static bool TryFindBodyBone(Transform root, out Transform body)
        {
            body = null;
            if (root == null)
            {
                return false;
            }

            if (string.Equals(root.name, BodyBoneName, StringComparison.OrdinalIgnoreCase))
            {
                body = root;
                return true;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                if (TryFindBodyBone(root.GetChild(i), out body))
                {
                    return true;
                }
            }

            return false;
        }

        private static void StripNonVisualComponents(GameObject visual)
        {
            Component[] components = visual.GetComponentsInChildren<Component>(true);
            for (int i = components.Length - 1; i >= 0; i--)
            {
                Component component = components[i];
                if (component == null ||
                    component is Transform ||
                    component is Renderer ||
                    component is MeshFilter ||
                    component is Animator ||
                    component is Animation ||
                    component is LODGroup ||
                    component is ParticleSystem)
                {
                    continue;
                }

                if (component is MonoBehaviour ||
                    component is Collider ||
                    component is Collider2D ||
                    component is Rigidbody ||
                    component is Rigidbody2D ||
                    component is CharacterController ||
                    component.GetType().FullName == "UnityEngine.AI.NavMeshAgent")
                {
                    UnityEngine.Object.Destroy(component);
                }
            }
        }

        private static void DisableRootMotion(GameObject visual)
        {
            Animator[] animators = visual.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                animators[i].applyRootMotion = false;
            }
        }

        private static void DestroyFellow(FellowVisual fellow)
        {
            if (fellow != null && fellow.Root != null)
            {
                UnityEngine.Object.Destroy(fellow.Root);
            }
        }

        private sealed class VisitorFellowStack
        {
            public VisitorFellowStack(GameObject visitorView, Transform originalBody)
            {
                VisitorView = visitorView;
                OriginalBody = originalBody;
            }

            public GameObject VisitorView { get; }
            public Transform OriginalBody { get; }
            public List<FellowVisual> Fellows { get; } = new List<FellowVisual>();
        }

        private sealed class FellowVisual
        {
            public FellowVisual(GameObject root, Transform followTargetBody, Transform body)
            {
                Root = root;
                FollowTargetBody = followTargetBody;
                Body = body;
            }

            public GameObject Root { get; }
            public Transform FollowTargetBody { get; }
            public Transform Body { get; }
        }
    }
}
