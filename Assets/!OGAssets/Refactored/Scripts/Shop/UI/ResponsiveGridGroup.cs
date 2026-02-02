using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways, RequireComponent(typeof(GridLayoutGroup)), DisallowMultipleComponent]
public sealed class ResponsiveGridGroup : MonoBehaviour
{
    [Header("Minimums per cell")]
    [Min(100)] public float minColumnWidth  = 280f;
    [Min(100)] public float minColumnHeight = 280f;

    [Header("Layout")]
    [Min(0)] public float gutter = 16f;
    [Min(1)] public int   minColumns = 1;
    [Min(1)] public int   maxColumns = 5;

    // New: control responsive row calculation bounds (optional)
    [Min(1)] public int   minRows = 1;
    [Min(1)] public int   maxRows = 100;

    [Header("Behavior")]
    public bool squareCells = false; // if true, height = width

    GridLayoutGroup _grid;
    RectTransform   _rt;

    void OnEnable()  { Cache(); Recalc(); }
    void OnRectTransformDimensionsChange() { Recalc(); }
    void OnValidate(){ Cache(); Recalc(); }

    void Cache()
    {
        if (!_grid) _grid = GetComponent<GridLayoutGroup>();
        if (!_rt)   _rt   = transform as RectTransform;
        if (_grid)
        {
            _grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _grid.spacing    = new Vector2(gutter, gutter);
        }
    }

    public void Recalc()
    {
        if (!_rt || !_grid) return;

        var w = _rt.rect.width;
        var h = _rt.rect.height;
        if (w <= 1f) return;

        // --- Columns from available width ---
        // Each column needs at least (minColumnWidth + gutter), except last has no trailing gutter
        int cols = Mathf.FloorToInt((w + gutter) / (minColumnWidth + gutter));
        cols = Mathf.Clamp(cols, minColumns, maxColumns);
        if (cols < 1) cols = 1;

        float totalGutterX = gutter * (cols - 1);
        float cellW = Mathf.Floor((w - totalGutterX) / cols);

        float cellH;
        if (squareCells)
        {
            cellH = cellW;
        }
        else
        {
            var fitter = _rt.GetComponent<ContentSizeFitter>();
            bool heightDrivenByContent = fitter && fitter.verticalFit == ContentSizeFitter.FitMode.PreferredSize;

            if (heightDrivenByContent || h <= 1f)
            {
                // Avoid circular sizing when height is driven by content.
                cellH = Mathf.Max(minColumnHeight, _grid.cellSize.y);
            }
            else
            {
                // --- Rows from available height ---
                int rows = Mathf.FloorToInt((h + gutter) / (minColumnHeight + gutter));
                rows = Mathf.Clamp(rows, minRows, maxRows);
                if (rows < 1) rows = 1;

                float totalGutterY = gutter * (rows - 1);
                cellH = Mathf.Floor((h - totalGutterY) / rows);
            }
        }

        // Apply to GridLayoutGroup
        _grid.cellSize        = new Vector2(Mathf.Max(1f, cellW), Mathf.Max(1f, cellH));
        _grid.constraintCount = cols;
        _grid.spacing         = new Vector2(gutter, gutter);
    }
}


/*using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways, RequireComponent(typeof(GridLayoutGroup)), DisallowMultipleComponent]
public sealed class ResponsiveGridGroup : MonoBehaviour
{
    [Min(100)] public float minColumnWidth = 280f;
    [Min(100)] public float minColumnHeight = 280f;
    [Min(0)]   public float gutter = 16f;
    [Min(1)]   public int   minColumns = 1;
    [Min(1)]   public int   maxColumns = 5;
    public bool squareCells = false; // if true, height = width

    GridLayoutGroup _grid;
    RectTransform   _rt;

    void OnEnable()  { Cache(); Recalc(); }
    void OnRectTransformDimensionsChange() { Recalc(); }
    void OnValidate(){ Cache(); Recalc(); }

    void Cache()
    {
        if (!_grid) _grid = GetComponent<GridLayoutGroup>();
        if (!_rt)   _rt   = transform as RectTransform;
        if (_grid) { _grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; _grid.spacing = new Vector2(gutter, gutter); }
    }

    public void Recalc()
    {
        if (!_rt || !_grid) return;
        var w = _rt.rect.width;
        if (w <= 1f) return;

        // Available width minus one gutter per column (except last)
        int cols = Mathf.Clamp(Mathf.FloorToInt((w + gutter) / (minColumnWidth + gutter)), minColumns, maxColumns);
        if (cols < 1) cols = 1;

        float totalGutter = gutter * (cols - 1);
        float cellW = Mathf.Floor((w - totalGutter) / cols);
        float cellH = squareCells ? cellW : _grid.cellSize.y; // keep current height unless square

        _grid.cellSize = new Vector2(cellW, cellH);
        _grid.constraintCount = cols;
        _grid.spacing = new Vector2(gutter, gutter);
    }
}*/