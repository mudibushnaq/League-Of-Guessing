using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(GridLayoutGroup))]
public class AutoGridSizer : MonoBehaviour
{
    [SerializeField] private int columns = 2;  // 2 for Q/W/E/R layout
    [SerializeField] private int rows = 2;

    private GridLayoutGroup grid;
    private RectTransform rectTransform;

    private void Awake()
    {
        grid = GetComponent<GridLayoutGroup>();
        rectTransform = GetComponent<RectTransform>();
    }

    private void OnEnable()
    {
        UpdateCellSize();
    }

#if UNITY_EDITOR
    private void Update()
    {
        // Update in editor for live preview
        if (!Application.isPlaying) UpdateCellSize();
    }
#endif

    private void UpdateCellSize()
    {
        float totalWidth = rectTransform.rect.width;
        float totalHeight = rectTransform.rect.height;

        // Subtract spacing between cells
        float spacingX = grid.spacing.x * (columns - 1);
        float spacingY = grid.spacing.y * (rows - 1);

        float cellWidth = (totalWidth - spacingX - grid.padding.left - grid.padding.right) / columns;
        float cellHeight = (totalHeight - spacingY - grid.padding.top - grid.padding.bottom) / rows;

        grid.cellSize = new Vector2(cellWidth, cellHeight);
    }
}