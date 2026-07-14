using System;
using System.Collections;
using System.Collections.Generic;
using MochiPark.Runtime.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace MochiPark.Runtime.Views
{
    public enum UICosmeticType
    {
        Bouncy = 0
    }

    [DisallowMultipleComponent]
    public sealed class UIButtonCosmeticService : MonoBehaviour
    {
        [Header("Bouncy Cosmetic")]
        [SerializeField] private BouncyCosmeticConfig bouncyConfig = new BouncyCosmeticConfig();

        [Header("Audio")]
        [SerializeField] private bool playClickSound = true;
        [SerializeField, Range(0f, 1f)] private float clickSoundVolumeScale = 1f;

        private readonly Dictionary<Button, ButtonRegistration> registrations = new Dictionary<Button, ButtonRegistration>();

        public void Register(Button button, UICosmeticType type, RectTransform visualTarget = null)
        {
            if (button == null)
            {
                return;
            }

            Unregister(button);

            RectTransform target = visualTarget != null ? visualTarget : button.transform as RectTransform;
            if (target == null)
            {
                return;
            }

            UIButtonCosmeticEventRelay relay = button.GetComponent<UIButtonCosmeticEventRelay>();
            if (relay == null)
            {
                relay = button.gameObject.AddComponent<UIButtonCosmeticEventRelay>();
            }

            var context = new CosmeticContext
            {
                Host = this,
                Button = button,
                Target = target,
                BaseScale = target.localScale,
                BaseRotation = target.localRotation,
                BaseAnchoredPosition = target.anchoredPosition,
                Graphic = ResolveGraphic(button, target)
            };

            if (context.Graphic != null)
            {
                context.BaseColor = context.Graphic.color;
            }

            CosmeticBehaviour cosmetic = CreateCosmetic(type, context);
            registrations.Add(button, new ButtonRegistration(relay, cosmetic));
            relay.Bind(this, button);
            cosmetic.ResetImmediate();
        }

        public void Unregister(Button button)
        {
            if (button == null || !registrations.TryGetValue(button, out ButtonRegistration registration))
            {
                return;
            }

            registration.Cosmetic.Dispose();
            registration.Relay.Clear(this);
            registrations.Remove(button);
        }

        internal void NotifyPointerEnter(Button button)
        {
            if (TryGetCosmetic(button, out CosmeticBehaviour cosmetic) && IsButtonInteractable(button))
            {
                cosmetic.OnHoverStart();
            }
        }

        internal void NotifyPointerExit(Button button)
        {
            if (TryGetCosmetic(button, out CosmeticBehaviour cosmetic))
            {
                cosmetic.OnHoverEnd();
            }
        }

        internal void NotifyPointerDown(Button button)
        {
            if (TryGetCosmetic(button, out CosmeticBehaviour cosmetic) && IsButtonInteractable(button))
            {
                cosmetic.OnPress();
            }
        }

        internal void NotifyPointerUp(Button button)
        {
            if (TryGetCosmetic(button, out CosmeticBehaviour cosmetic))
            {
                cosmetic.OnRelease();
            }
        }

        internal void NotifyPointerClick(Button button)
        {
            if (!TryGetCosmetic(button, out CosmeticBehaviour cosmetic))
            {
                return;
            }

            if (IsButtonInteractable(button))
            {
                PlayButtonClickSound();
                cosmetic.OnClick();
                return;
            }

            cosmetic.OnClickDisabled();
        }

        internal void NotifySubmit(Button button)
        {
            if (TryGetCosmetic(button, out CosmeticBehaviour cosmetic) && IsButtonInteractable(button))
            {
                PlayButtonClickSound();
                cosmetic.OnClick();
            }
        }

        private void OnDestroy()
        {
            foreach (ButtonRegistration registration in registrations.Values)
            {
                registration.Cosmetic.Dispose();
                registration.Relay.Clear(this);
            }

            registrations.Clear();
        }

        private CosmeticBehaviour CreateCosmetic(UICosmeticType type, CosmeticContext context)
        {
            return type switch
            {
                UICosmeticType.Bouncy => new BouncyCosmetic(context, bouncyConfig),
                _ => new BouncyCosmetic(context, bouncyConfig)
            };
        }

        private bool TryGetCosmetic(Button button, out CosmeticBehaviour cosmetic)
        {
            if (button != null && registrations.TryGetValue(button, out ButtonRegistration registration))
            {
                cosmetic = registration.Cosmetic;
                return true;
            }

            cosmetic = null;
            return false;
        }

        private void PlayButtonClickSound()
        {
            if (playClickSound)
            {
                MochiParkAudioManager.PlayUiButtonClickSfx(clickSoundVolumeScale);
            }
        }

        private static bool IsButtonInteractable(Button button)
        {
            return button != null && button.IsActive() && button.IsInteractable();
        }

        private static Graphic ResolveGraphic(Button button, RectTransform target)
        {
            if (button != null && button.targetGraphic != null)
            {
                return button.targetGraphic;
            }

            return target != null ? target.GetComponentInChildren<Graphic>(true) : null;
        }

        [Serializable]
        private sealed class BouncyCosmeticConfig
        {
            [Header("Hover")]
            public float HoverScale = 1.08f;
            public float HoverDuration = 0.12f;

            [Header("Press")]
            public float PressScale = 0.96f;
            public float PressDuration = 0.05f;

            [Header("Click")]
            public float ClickScale = 1.16f;
            public int PulseCount = 2;
            public float PulseUpDuration = 0.04f;
            public float PulseDownDuration = 0.05f;

            [Header("Disabled")]
            public float DisabledShakeDegrees = 6f;
            public int DisabledShakeCycles = 2;
            public float DisabledShakeStepDuration = 0.035f;
        }

        private sealed class ButtonRegistration
        {
            public readonly UIButtonCosmeticEventRelay Relay;
            public readonly CosmeticBehaviour Cosmetic;

            public ButtonRegistration(UIButtonCosmeticEventRelay relay, CosmeticBehaviour cosmetic)
            {
                Relay = relay;
                Cosmetic = cosmetic;
            }
        }

        private sealed class CosmeticContext
        {
            public UIButtonCosmeticService Host;
            public Button Button;
            public RectTransform Target;

            public Vector3 BaseScale;
            public Quaternion BaseRotation;
            public Vector2 BaseAnchoredPosition;

            public Graphic Graphic;
            public Color BaseColor;
        }

        private abstract class CosmeticBehaviour
        {
            protected readonly CosmeticContext Context;
            private Coroutine routine;

            protected CosmeticBehaviour(CosmeticContext context)
            {
                Context = context;
            }

            public virtual void OnHoverStart()
            {
            }

            public virtual void OnHoverEnd()
            {
            }

            public virtual void OnPress()
            {
            }

            public virtual void OnRelease()
            {
            }

            public virtual void OnClick()
            {
            }

            public virtual void OnClickDisabled()
            {
            }

            public virtual void ResetImmediate()
            {
                if (Context.Target == null)
                {
                    return;
                }

                Context.Target.localScale = Context.BaseScale;
                Context.Target.localRotation = Context.BaseRotation;
                Context.Target.anchoredPosition = Context.BaseAnchoredPosition;
            }

            public void Dispose()
            {
                Stop();
                ResetImmediate();
            }

            protected void Play(IEnumerator animation)
            {
                Stop();
                if (Context.Host != null && Context.Host.isActiveAndEnabled)
                {
                    routine = Context.Host.StartCoroutine(PlayRoutine(animation));
                }
            }

            protected void Stop()
            {
                if (routine == null || Context.Host == null)
                {
                    routine = null;
                    return;
                }

                Context.Host.StopCoroutine(routine);
                routine = null;
            }

            private IEnumerator PlayRoutine(IEnumerator animation)
            {
                yield return animation;
                routine = null;
            }
        }

        private sealed class BouncyCosmetic : CosmeticBehaviour
        {
            private readonly BouncyCosmeticConfig config;
            private bool isHovering;

            public BouncyCosmetic(CosmeticContext context, BouncyCosmeticConfig config)
                : base(context)
            {
                this.config = config;
            }

            public override void OnHoverStart()
            {
                isHovering = true;
                Play(ScaleRoutine(GetRestScale(), config.HoverDuration));
            }

            public override void OnHoverEnd()
            {
                isHovering = false;
                Play(ScaleRoutine(Context.BaseScale, config.HoverDuration));
            }

            public override void OnPress()
            {
                Play(ScaleRoutine(Context.BaseScale * config.PressScale, config.PressDuration));
            }

            public override void OnRelease()
            {
                Play(ScaleRoutine(GetRestScale(), config.PressDuration));
            }

            public override void OnClick()
            {
                Play(ClickPulseRoutine());
            }

            public override void OnClickDisabled()
            {
                Play(DisabledShakeRoutine());
            }

            public override void ResetImmediate()
            {
                base.ResetImmediate();
                isHovering = false;
            }

            private Vector3 GetRestScale()
            {
                return isHovering ? Context.BaseScale * config.HoverScale : Context.BaseScale;
            }

            private IEnumerator ClickPulseRoutine()
            {
                int pulseCount = Mathf.Max(1, config.PulseCount);
                for (int i = 0; i < pulseCount; i++)
                {
                    yield return ScaleRoutine(Context.BaseScale * config.ClickScale, config.PulseUpDuration);
                    yield return ScaleRoutine(GetRestScale(), config.PulseDownDuration);
                }

                if (Context.Target != null)
                {
                    Context.Target.localScale = GetRestScale();
                }
            }

            private IEnumerator DisabledShakeRoutine()
            {
                if (Context.Target == null)
                {
                    yield break;
                }

                int cycles = Mathf.Max(1, config.DisabledShakeCycles);
                float stepDuration = Mathf.Max(0.01f, config.DisabledShakeStepDuration);
                for (int i = 0; i < cycles; i++)
                {
                    yield return RotateRoutine(-config.DisabledShakeDegrees, stepDuration);
                    yield return RotateRoutine(config.DisabledShakeDegrees, stepDuration);
                }

                yield return RotateRoutine(0f, stepDuration);
                Context.Target.localRotation = Context.BaseRotation;
            }

            private IEnumerator ScaleRoutine(Vector3 targetScale, float duration)
            {
                if (Context.Target == null)
                {
                    yield break;
                }

                Vector3 startScale = Context.Target.localScale;
                float safeDuration = Mathf.Max(0.01f, duration);
                float elapsed = 0f;

                while (elapsed < safeDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / safeDuration);
                    float eased = 1f - (1f - t) * (1f - t);
                    Context.Target.localScale = Vector3.LerpUnclamped(startScale, targetScale, eased);
                    yield return null;
                }

                Context.Target.localScale = targetScale;
            }

            private IEnumerator RotateRoutine(float targetDegrees, float duration)
            {
                if (Context.Target == null)
                {
                    yield break;
                }

                Quaternion startRotation = Context.Target.localRotation;
                Quaternion targetRotation = Context.BaseRotation * Quaternion.Euler(0f, 0f, targetDegrees);
                float safeDuration = Mathf.Max(0.01f, duration);
                float elapsed = 0f;

                while (elapsed < safeDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / safeDuration);
                    Context.Target.localRotation = Quaternion.Slerp(startRotation, targetRotation, t);
                    yield return null;
                }

                Context.Target.localRotation = targetRotation;
            }
        }
    }
}
