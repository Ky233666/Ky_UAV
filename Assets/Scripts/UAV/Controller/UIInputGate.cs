using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Centralized UI hover detection used to block scene input while the pointer is over UI.
/// Scroll-wheel leakage is especially visible when a ScrollRect and the scene camera both
/// consume the same frame's input, so all mouse-driven scene controls should query this gate.
/// </summary>
public static class UIInputGate
{
    private static readonly List<RaycastResult> RaycastResults = new List<RaycastResult>(32);
    private static EventSystem cachedEventSystem;
    private static PointerEventData pointerEventData;

    public static bool IsPointerOverBlockingUi()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            return false;
        }

        if (pointerEventData == null || cachedEventSystem != eventSystem)
        {
            cachedEventSystem = eventSystem;
            pointerEventData = new PointerEventData(eventSystem);
        }

        pointerEventData.Reset();
        pointerEventData.position = Input.mousePosition;
        pointerEventData.scrollDelta = Input.mouseScrollDelta;

        RaycastResults.Clear();
        eventSystem.RaycastAll(pointerEventData, RaycastResults);

        for (int i = 0; i < RaycastResults.Count; i++)
        {
            RaycastResult hit = RaycastResults[i];
            GameObject hitObject = hit.gameObject;
            if (hitObject == null || !hitObject.activeInHierarchy)
            {
                continue;
            }

            if (hitObject.GetComponentInParent<Canvas>() == null)
            {
                continue;
            }

            Graphic graphic = hitObject.GetComponent<Graphic>();
            if (graphic != null && graphic.raycastTarget)
            {
                return true;
            }

            if (hitObject.GetComponentInParent<ScrollRect>() != null ||
                hitObject.GetComponentInParent<Selectable>() != null)
            {
                return true;
            }
        }

        return false;
    }
}
