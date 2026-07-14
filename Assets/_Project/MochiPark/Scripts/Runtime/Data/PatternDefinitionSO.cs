using System.Collections.Generic;
using MochiPark.Runtime.Core;
using UnityEngine;

namespace MochiPark.Runtime.Data
{
    [CreateAssetMenu(
        fileName = "pattern_new",
        menuName = "Mochi Park/Data/Pattern Definition")]
    public sealed class PatternDefinitionSO : ScriptableObject
    {
        [SerializeField] private string patternId;
        [SerializeField] private List<GridCell> cells = new List<GridCell>();
        [SerializeField] private PatternTargetType targetType = PatternTargetType.Footprint;
        [SerializeField] private PatternRotationMode rotationMode = PatternRotationMode.RotateWithUnit;
        [SerializeField] private string previewColorKey = "default";

        public string PatternId => patternId;
        public IReadOnlyList<GridCell> Cells => cells;
        public PatternTargetType TargetType => targetType;
        public PatternRotationMode RotationMode => rotationMode;
        public string PreviewColorKey => previewColorKey;
    }
}
