using UnityEngine;
using UnityEngine.Serialization;

namespace MochiPark.Runtime.Data
{
    [CreateAssetMenu(
        fileName = "ui_config",
        menuName = "Mochi Park/Data/UI Config")]
    public sealed class UIConfigSO : ScriptableObject
    {
        [FormerlySerializedAs("floatingScoreCanvasPrefab")]
        [FormerlySerializedAs("floatingJoyCanvasPrefab")]
        [SerializeField] private GameObject floatingScoreCanvasPrefab;
        [FormerlySerializedAs("joyAmountCanvasPrefab")]
        [SerializeField] private GameObject screenspaceIndicatorCanvasPrefab;
        [SerializeField] private GameObject hudCanvasPrefab;
        [SerializeField] private GameObject draftCanvasPrefab;
        [SerializeField] private GameObject frenzyVfxPrefab;
        [SerializeField] private GameObject frenzyMomentVfxPrefab;
        [SerializeField] private GameObject countdownVfxPrefab;
        [SerializeField] private GameObject uiVfxCanvasPrefab;
        [SerializeField] private GameObject joyFlyVfxPrefab;
        [SerializeField] private GameObject resultCanvasPrefab;
        [SerializeField] private GameObject resultVfxPrefab;

        public GameObject FloatingScoreCanvasPrefab => floatingScoreCanvasPrefab;
        public GameObject ScreenspaceIndicatorCanvasPrefab => screenspaceIndicatorCanvasPrefab;
        public GameObject HudCanvasPrefab => hudCanvasPrefab;
        public GameObject DraftCanvasPrefab => draftCanvasPrefab;
        public GameObject FrenzyVfxPrefab => frenzyVfxPrefab;
        public GameObject FrenzyMomentVfxPrefab => frenzyMomentVfxPrefab;
        public GameObject CountdownVfxPrefab => countdownVfxPrefab;
        public GameObject UIVfxCanvasPrefab => uiVfxCanvasPrefab;
        public GameObject JoyFlyVfxPrefab => joyFlyVfxPrefab;
        public GameObject ResultCanvasPrefab => resultCanvasPrefab;
        public GameObject ResultVfxPrefab => resultVfxPrefab;
    }
}
