using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Butun ekranni qoplaydigan shaffof UI maydon. Bu yerga bosish — sakrash / boshlash / qayta boshlash.
/// </summary>
public class TapArea : MonoBehaviour, IPointerDownHandler
{
    public event Action Pressed;

    public void OnPointerDown(PointerEventData eventData)
    {
        Pressed?.Invoke();
    }
}
