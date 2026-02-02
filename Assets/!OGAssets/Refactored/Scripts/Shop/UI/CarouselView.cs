using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public sealed class CarouselView : MonoBehaviour
{
    [Header("Wiring")]
    public ScrollRect scrollRect;          // Horizontal
    public RectTransform content;          // Child of scrollRect.content
    public Button prevButton;
    public Button nextButton;
    public Transform dotsParent;           // optional
    public GameObject dotPrefab;           // small Image as dot (enabled=active)

    [Header("Layout")]
    public float pageSpacing = 24f;
    public float snapDuration = 0.2f;

    readonly List<RectTransform> _pages = new();
    int _page;

    void Awake()
    {
        if (prevButton) prevButton.onClick.AddListener(()=> SnapTo(_page-1).Forget());
        if (nextButton) nextButton.onClick.AddListener(()=> SnapTo(_page+1).Forget());
    }

    public void Clear()
    {
        _pages.Clear();
        foreach (Transform c in content) Destroy(c.gameObject);
        if (dotsParent) foreach (Transform d in dotsParent) Destroy(d.gameObject);
        _page = 0;
    }

    public void AddPage(RectTransform page)
    {
        page.SetParent(content, false);
        _pages.Add(page);
    }

    public void BuildDots()
    {
        if (!dotsParent || !dotPrefab) return;
        foreach (Transform d in dotsParent) Destroy(d.gameObject);
        for (int i=0;i<_pages.Count;i++)
        {
            var dot = Instantiate(dotPrefab, dotsParent);
            dot.SetActive(i==_page);
        }
    }

    public void LayoutHorizontal()
    {
        // Position pages side-by-side
        float x = 0f;
        float h = ((RectTransform)scrollRect.viewport).rect.height;
        for (int i=0;i<_pages.Count;i++)
        {
            var rt = _pages[i];
            var w  = ((RectTransform)scrollRect.viewport).rect.width;
            // full-bleed width
            rt.anchorMin = rt.anchorMax = new Vector2(0,1);
            rt.pivot = new Vector2(0,1);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, 0);
            x += w + pageSpacing;
        }
        content.sizeDelta = new Vector2(x - pageSpacing, h);
    }

    public async UniTask SnapTo(int index)
    {
        if (_pages.Count == 0) return;
        _page = Mathf.Clamp(index, 0, _pages.Count-1);

        var w = ((RectTransform)scrollRect.viewport).rect.width;
        float targetX = _page * (w + pageSpacing);
        var cur = content.anchoredPosition;
        var dst = new Vector2(-targetX, 0f);

        float t=0f;
        while (t < snapDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / snapDuration);
            content.anchoredPosition = Vector2.Lerp(cur, dst, k*k*(3-2*k)); // smoothstep
            await UniTask.Yield();
        }
        content.anchoredPosition = dst;

        // update dots
        if (dotsParent)
        {
            for (int i=0;i<dotsParent.childCount;i++)
                dotsParent.GetChild(i).gameObject.SetActive(i==_page);
        }
    }
}
