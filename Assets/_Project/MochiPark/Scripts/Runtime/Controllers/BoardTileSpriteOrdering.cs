using System;
using MochiPark.Runtime.Board;
using UnityEngine;

namespace MochiPark.Runtime.Views
{
    public enum BoardTileSpriteLayer
    {
        LockedBuildable = 0,
        Buildable = 1,
        Preview = 2
    }

    [Serializable]
    public sealed class BoardTileSpriteLayerConfig
    {
        [SerializeField] private float heightOffset;
        [SerializeField] private int sortingOrder;
        [SerializeField] private float rotationX = 90f;
        [SerializeField] private float spriteInset = 1f;

        public BoardTileSpriteLayerConfig(
            float heightOffset,
            int sortingOrder)
        {
            this.heightOffset = heightOffset;
            this.sortingOrder = sortingOrder;
        }

        public float HeightOffset => heightOffset;
        public int SortingOrder => sortingOrder;
        public Quaternion Rotation => Quaternion.Euler(rotationX, 0f, 0f);

        public float GetScale(Sprite sprite, float tileSize)
        {
            if (sprite == null)
            {
                return 1f;
            }

            float largestSpriteSide = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
            return largestSpriteSide > 0f
                ? tileSize * Mathf.Max(0f, spriteInset) / largestSpriteSide
                : 1f;
        }
    }

    public sealed class BoardTileSpriteOrdering : MonoBehaviour
    {
        private const float DefaultLayerOffset = 0.001f;

        [SerializeField] private BoardTileSpriteLayerConfig lockedBuildable =
            new BoardTileSpriteLayerConfig(DefaultLayerOffset, 1);
        [SerializeField] private BoardTileSpriteLayerConfig buildable =
            new BoardTileSpriteLayerConfig(DefaultLayerOffset * 2f, 2);
        [SerializeField] private BoardTileSpriteLayerConfig preview =
            new BoardTileSpriteLayerConfig(DefaultLayerOffset * 3f, 3);

        public void ConfigureSprite(
            SpriteRenderer spriteRenderer,
            RuntimeBoardState board,
            Vector2Int cell,
            Sprite sprite,
            BoardTileSpriteLayer layer)
        {
            if (spriteRenderer == null || board == null)
            {
                return;
            }

            BoardTileSpriteLayerConfig config = GetConfig(layer);
            Transform spriteTransform = spriteRenderer.transform;
            spriteTransform.position = board.CellToWorldSurface(cell) + Vector3.up * config.HeightOffset;
            spriteTransform.rotation = config.Rotation;
            spriteTransform.localScale = Vector3.one * config.GetScale(sprite, board.TileSizeXZ);

            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingOrder = config.SortingOrder;
        }

        private BoardTileSpriteLayerConfig GetConfig(BoardTileSpriteLayer layer)
        {
            switch (layer)
            {
                case BoardTileSpriteLayer.LockedBuildable:
                    return lockedBuildable;
                case BoardTileSpriteLayer.Buildable:
                    return buildable;
                case BoardTileSpriteLayer.Preview:
                    return preview;
                default:
                    return buildable;
            }
        }
    }
}
