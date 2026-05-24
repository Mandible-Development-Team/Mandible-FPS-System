using UnityEngine;
using System.Collections;
using System.Collections.Generic;

using Mandible.FPSController;
using Mandible.PlayerController;

namespace Mandible.FPSController
{
    public class AimSights : MonoBehaviour
    {
        [SerializeField] Gun owner;

        [Header("Sights")]
        [SerializeField] SpriteRenderer sights;
        [SerializeField] float sightsVisibilitySpeed = 10f;
        Color defaultSightsColor;
        Color targetSightsColor;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            InitializeSights();
        }

        // Update is called once per frame
        void Update()
        {
            HandleSights();
        }

        //Sights
        
        private void HandleSights()
        {
            sights.color = Color.Lerp(sights.color, GetSightsColor(), sightsVisibilitySpeed * Time.deltaTime);
        }

        public Color GetSightsColor()
        {
            Gun.GunPosition positionState = owner.positionState;
            switch (positionState)
            {
                case Gun.GunPosition.Default: return Color.clear;
                case Gun.GunPosition.Aimed: return defaultSightsColor;
            }
            return defaultSightsColor;
        }

        public void InitializeSights()
        {
            if (sights == null) return;
            
            defaultSightsColor = sights.color;
            sights.color = Color.clear;
        }

    }
}


