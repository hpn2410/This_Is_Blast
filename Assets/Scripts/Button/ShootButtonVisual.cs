using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ShootButtonVisual : MonoBehaviour
{
    [SerializeField] private Image buttonImage;
    [SerializeField] private GameObject cover;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    public void SetColorVisible(bool visible)
    {
        if (buttonImage != null)
        {
            buttonImage.enabled = visible;
        }

        if (cover != null)
        {
            cover.SetActive(!visible);
        }

        if (button != null)
        {
            button.interactable = visible;
        }
    }
}