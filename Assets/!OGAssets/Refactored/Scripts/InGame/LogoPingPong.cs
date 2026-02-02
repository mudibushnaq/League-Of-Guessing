using DG.Tweening;
using UnityEngine;

public class LogoPingPong : MonoBehaviour
{
    [SerializeField] RectTransform logo;   // leave empty to use this object
    [SerializeField] float amplitude = 20f; // how far it moves (px)
    [SerializeField] float duration  = 1.2f; // time to go up or down
    [SerializeField] bool ignoreTimeScale = true;

    Tween _tween;
    Vector2 _startPos;

    void Awake()
    {
        if (!logo) logo = (RectTransform)transform;
        _startPos = logo.anchoredPosition;
    }

    void OnEnable()
    {
        DOTween.Kill(logo);                       // kill any old tween on this target
        logo.anchoredPosition = _startPos;        // reset

        _tween = logo
            .DOAnchorPosY(_startPos.y + amplitude, duration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)          // ping-pong forever
            .SetUpdate(ignoreTimeScale)           // animate even if Time.timeScale == 0
            .SetLink(gameObject);                 // auto-kill on destroy
    }

    void OnDisable()
    {
        DOTween.Kill(logo);
        logo.anchoredPosition = _startPos;
    }
}