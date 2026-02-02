using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIRaycastProbe : MonoBehaviour
{
    public GraphicRaycaster raycaster;
    public EventSystem eventSystem;

    void Reset()
    {
        raycaster = GetComponentInParent<GraphicRaycaster>();
        eventSystem = FindObjectOfType<EventSystem>();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            var ped = new PointerEventData(eventSystem) { position = Input.mousePosition };
            var results = new List<RaycastResult>();
            raycaster.Raycast(ped, results);

            if (results.Count == 0) Debug.Log("UIRaycastProbe: nothing hit.");
            else Debug.Log("UIRaycastProbe hit top: " + results[0].gameObject.name);
        }
    }
}