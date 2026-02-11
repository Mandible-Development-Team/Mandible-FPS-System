using UnityEngine;
using UnityEngine.UI;

using Mandible.FPSController;
using Mandible.Entities;

namespace Mandible.FPSController
{  
    public class Hitmarker : MonoBehaviour
    {
        [SerializeField] FPSProceduralController fpsController;
        [SerializeField] RectTransform rectTransform;
        [SerializeField] Image image;

        [Header("Scale Settings")]
        [SerializeField] private Vector3 initialScale = Vector3.zero;
        [SerializeField] private float punchForce = 0.2f;
        [SerializeField] private float maxForce = 1.5f;
        [SerializeField] private float pullSpeed = 10f;

        private Vector3 currentScale = Vector3.zero;
        private Color currentColor = Color.white;
        private Color initialColor = Color.white;
        private void Awake()
        {
            if (fpsController == null)
            {
                Debug.LogError("Hitmarker: FPSController reference is missing.");
                this.enabled = false;
                return;
            }

            SetEventListeners();
        }

        private void Start()
        {
            //RectTransform
            if (rectTransform != null)
            {
                rectTransform.localScale = initialScale;
            }

            currentScale = initialScale;

            //Image
            if (image != null)
            {
                initialColor = image.color;
                currentColor = initialColor;
            }
        }

        private void Update()
        {
            if (rectTransform == null) return;

            //Scale
            currentScale = Vector3.Lerp(currentScale, initialScale, Time.deltaTime * pullSpeed);
            rectTransform.localScale = currentScale;

            //Color
            currentColor = Color.Lerp(currentColor, initialColor, Time.deltaTime * pullSpeed);
            image.color = currentColor;
        }

        public void Trigger(HitType hitType, RaycastHit hitInfo, Vector3 hitPoint)
        {
            if (rectTransform == null) return;
            
            float multiplier = 1f;
            float maxForceMultiplier = 1f;

            Color targetColor = initialColor;
            float colorPunchForce = 0.75f;

            switch (hitType)
            {
                case HitType.Critical:
                    multiplier = 1.5f;
                    maxForceMultiplier = 1.25f;
                    targetColor = Color.red;
                    break;
                case HitType.Normal:
                    break;
                default:
                    break;
            }

            //Scale
            currentScale += new Vector3(1f, 1f, 1f) * punchForce * multiplier;
            if(currentScale.magnitude > (maxForce * maxForceMultiplier))
            {
                currentScale = currentScale.normalized * maxForce * maxForceMultiplier;
            }

            //Color
            Color colorPunch = (targetColor - currentColor) * colorPunchForce;
            currentColor = new Color(
                Mathf.Clamp01(currentColor.r + colorPunch.r),
                Mathf.Clamp01(currentColor.g + colorPunch.g),
                Mathf.Clamp01(currentColor.b + colorPunch.b),
                currentColor.a
            );
        }

        //Event Listeners
        private void SetEventListeners()
        {
            fpsController.weaponSystem.onHitTarget?.AddListener(Trigger);
        }
    }
}
