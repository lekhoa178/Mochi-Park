using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MochiPark.Runtime.Views
{
    public sealed class LobbyController : MonoBehaviour
    {
        [SerializeField] private Button level1Button;
        [SerializeField] private Button level2Button;
        [SerializeField] private Button level3Button;
        [SerializeField] private Button level4Button;
        [SerializeField] private UIButtonCosmeticService buttonCosmeticService;
        [SerializeField] private string level1SceneName = "Gameplay_Level_1";
        [SerializeField] private GameObject transitionScreenPrefab;
        [SerializeField] private float minimumLoadingSeconds = 0.35f;
        [SerializeField] private float transitionEventTimeoutSeconds = 3f;

        private readonly List<Button> registeredCosmeticButtons = new List<Button>();
        private bool isLoading;

        public GameObject TransitionScreenPrefab => transitionScreenPrefab;

        private void Awake()
        {
            ResolveButtonCosmeticService();
            SetPlaceholderState();
        }

        private void OnEnable()
        {
            ResolveButtonCosmeticService();
            RegisterAllButtonCosmetics();

            if (level1Button != null)
            {
                level1Button.onClick.AddListener(LoadLevel1);
            }
        }

        private void OnDisable()
        {
            if (level1Button != null)
            {
                level1Button.onClick.RemoveListener(LoadLevel1);
            }

            UnregisterAllButtonCosmetics();
        }

        private void OnValidate()
        {
            SetPlaceholderState();
        }

        private void LoadLevel1()
        {
            if (isLoading)
            {
                return;
            }

            isLoading = true;
            SetButtonsInteractable(false);
            DontDestroyOnLoad(gameObject);

            if (!SceneTransitionLoader.TryLoadScene(
                    level1SceneName,
                    transitionScreenPrefab,
                    minimumLoadingSeconds,
                    transitionEventTimeoutSeconds,
                    RemoveLobbyUiForGameplay,
                    DestroyLobby,
                    RestoreLobbyAfterLoadFailure))
            {
                RestoreLobbyAfterLoadFailure();
            }
        }

        private void RemoveLobbyUiForGameplay()
        {
            foreach (Canvas canvas in GetComponentsInChildren<Canvas>(true))
            {
                canvas.enabled = false;
            }

            foreach (GraphicRaycaster raycaster in GetComponentsInChildren<GraphicRaycaster>(true))
            {
                raycaster.enabled = false;
            }
        }

        private void DestroyLobby()
        {
            Destroy(gameObject);
        }

        private void RestoreLobbyAfterLoadFailure()
        {
            isLoading = false;
            SetButtonsInteractable(true);
        }

        private void SetPlaceholderState()
        {
            if (level2Button != null)
            {
                level2Button.interactable = false;
            }

            if (level3Button != null)
            {
                level3Button.interactable = false;
            }

            if (level4Button != null)
            {
                level4Button.interactable = false;
            }
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (level1Button != null)
            {
                level1Button.interactable = interactable;
            }

            SetPlaceholderState();
        }

        private void ResolveButtonCosmeticService()
        {
            if (buttonCosmeticService != null)
            {
                return;
            }

            buttonCosmeticService = GetComponentInParent<UIButtonCosmeticService>(true);
            if (buttonCosmeticService != null)
            {
                return;
            }

            buttonCosmeticService = GetComponentInChildren<UIButtonCosmeticService>(true);
            if (buttonCosmeticService != null)
            {
                return;
            }

            buttonCosmeticService = gameObject.AddComponent<UIButtonCosmeticService>();
        }

        private void RegisterAllButtonCosmetics()
        {
            if (buttonCosmeticService == null)
            {
                return;
            }

            UnregisterAllButtonCosmetics();

            Button[] buttons = GetComponentsInChildren<Button>(true);
            foreach (Button button in buttons)
            {
                if (button == null)
                {
                    continue;
                }

                buttonCosmeticService.Register(button, UICosmeticType.Bouncy, button.transform as RectTransform);
                registeredCosmeticButtons.Add(button);
            }
        }

        private void UnregisterAllButtonCosmetics()
        {
            if (buttonCosmeticService == null)
            {
                registeredCosmeticButtons.Clear();
                return;
            }

            foreach (Button button in registeredCosmeticButtons)
            {
                buttonCosmeticService.Unregister(button);
            }

            registeredCosmeticButtons.Clear();
        }
    }
}
