using UnityEngine;

namespace Mandible.FPSController
{
    [DefaultExecutionOrder(-150)]
    public class AimPivot : MonoBehaviour
    {
        [Header("General")]
        public Transform transformSource;

        [Header("Tuning")]
        [Range(0f, 1f)]
        public float weight = 1f;

        [Header("Axes")]
        public bool applyYaw   = true;
        public bool applyPitch = true;
        public bool applyRoll  = false;

        [Header("Smoothing")]
        [Tooltip("0 = instant, higher = slower")]
        public float rotationLerpSpeed = 0f;

        [Header("Advanced")]
        public bool enableRecoil = true;
        public Quaternion rotationDelta = Quaternion.identity;

        //Input System
        private PlayerInputActions input;
        private Vector2 lookInput = Vector2.zero;

        //Velocity
        private Vector3 lastEuler;
        private Vector3 screenVelocity;

        //Recoil
        private Vector2 recoil;

        void Awake()
        {
            InitializeInput();
        }

        void OnEnable()
        {
            input?.Enable();
        }

        void OnDisable()
        {
            input?.Disable();
        }
        
        float pitch;

        void LateUpdate()
        {
            if (transformSource) transform.position = transformSource.position;

            HandleLook();

            pitch = Mathf.Clamp(pitch, -90f + 1e-3f, 90f - 1e-3f);
            transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            CalculateLookVelocity();
        }

        //Look
        private const float LOOK_PITCH_EPSILON = 1e-3f;
        void HandleLook()
        {
            float sensitivity = 3f;
            pitch -= lookInput.y * sensitivity * Time.deltaTime;
        }

        void CalculateLookVelocity()
        {
            Vector3 currentEuler = transform.eulerAngles;

            float deltaYaw = Mathf.DeltaAngle(lastEuler.y, currentEuler.y);
            float deltaPitch = Mathf.DeltaAngle(-lastEuler.x, -currentEuler.x);

            Vector3 rawVelocity = new Vector3(deltaYaw, deltaPitch, 0f) / Time.deltaTime;
            screenVelocity = Vector3.Lerp(screenVelocity, rawVelocity, Time.deltaTime * 10f);

            lastEuler = currentEuler;
        }

        //API

        public void AddRecoil(Vector2 recoilAmount)
        {
            if(enableRecoil) pitch -= recoilAmount.y * Time.deltaTime;
        }

        //Input

        void InitializeInput()
        {
            input = new PlayerInputActions();

            //Look
            input.Player.Look.performed += ctx => 
            {
                lookInput = ctx.ReadValue<Vector2>();
                //currentDevice = ctx.control.device;
            };

            input.Player.Look.canceled += _ => lookInput = Vector2.zero;
        }

        //Helpers

        Quaternion SnapToZero(Quaternion q, float threshold = 0.005f)
        {
            if (Quaternion.Angle(q, Quaternion.identity) < threshold * Mathf.Rad2Deg)
                return Quaternion.identity;
            return q;
        }

        public Vector3 GetScreenVelocity()
        {
            return screenVelocity;
        }
    }
}
