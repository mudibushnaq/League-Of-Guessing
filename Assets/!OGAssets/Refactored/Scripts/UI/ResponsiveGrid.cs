using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform), typeof(GridLayoutGroup))]
public class ResponsiveGrid : MonoBehaviour
{
    [SerializeField] bool keepSquare = true;
    [SerializeField] int columns = 2;
    [SerializeField] int rows = 2;

    RectTransform _rt;
    GridLayoutGroup _grid;

    void Awake()
    {
        _rt = GetComponent<RectTransform>();
        _grid = GetComponent<GridLayoutGroup>();
        _grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        _grid.constraintCount = columns;
    }

    void OnEnable() => Rebuild();
    void OnRectTransformDimensionsChange() => Rebuild();

    void Rebuild()
    {
        var pad = _grid.padding;
        var spacing = _grid.spacing;

        float innerW = _rt.rect.width  - pad.left - pad.right  - spacing.x * (columns - 1);
        float innerH = _rt.rect.height - pad.top  - pad.bottom - spacing.y * (rows    - 1);

        float cellW = innerW / columns;
        float cellH = innerH / rows;

        if (keepSquare)
        {
            float s = Mathf.Floor(Mathf.Min(cellW, cellH));
            _grid.cellSize = new Vector2(s, s);
        }
        else
        {
            _grid.cellSize = new Vector2(cellW, cellH);
        }
    }
}
