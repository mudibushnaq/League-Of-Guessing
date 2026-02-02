using TMPro;
using UnityEngine;

public sealed class SectionHeaderView : MonoBehaviour
{
    [SerializeField] TMP_Text label;
    public void SetText(string t) { if (label) label.text = t; }
}