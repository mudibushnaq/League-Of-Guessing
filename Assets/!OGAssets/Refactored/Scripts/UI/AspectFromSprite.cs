using System;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image), typeof(AspectRatioFitter))]
public class AspectFromSprite : MonoBehaviour
{
    void OnEnable()
    {
        var img = GetComponent<Image>();
        var arf = GetComponent<AspectRatioFitter>();
        if (img.sprite != null)
        {
            var r = img.sprite.rect;
            arf.aspectRatio = r.width / r.height; // matches your parchment art
        }
    }

    private void OnValidate()
    {
        var img = GetComponent<Image>();
        var arf = GetComponent<AspectRatioFitter>();
        if (img.sprite != null)
        {
            var r = img.sprite.rect;
            arf.aspectRatio = r.width / r.height; // matches your parchment art
        }
    }
}