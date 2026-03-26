using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Mandible.FPSController;
using Mandible.PlayerController;

public class WeaponZoom : WeaponComponent
{
    [SerializeField] float zoomedInMultiplier = 1.5f;
    [SerializeField] float zoomSpeed = 5f;

    private CameraController fpsCamera;
    private float currentMultiplier = 1f;
    private float targetMultiplier = 1f;

    private void Awake()
    {
        if(owner != null)
        {
            fpsCamera = owner.Camera;
        }

        SetEventListeners();
    }

    private void SetEventListeners()
    {
        if(weapon != null)
        {
            Gun gun = weapon as Gun;

            gun?.OnAim.AddListener(ZoomIn);
            gun?.OnUnAim.AddListener(ZoomOut);
        }
    }

    private new void OnDisable()
    {
        ZoomOut();
    }

    public override void Handle() 
    { 
        currentMultiplier = Mathf.Lerp(currentMultiplier, targetMultiplier, zoomSpeed * Time.deltaTime);
    }

    public void ZoomIn()
    {
        targetMultiplier = zoomedInMultiplier;
        fpsCamera?.SetZoomMultiplier(targetMultiplier);
    }

    public void ZoomOut()
    {
        targetMultiplier = 1f;
        fpsCamera?.SetZoomMultiplier(targetMultiplier);
    }
}

