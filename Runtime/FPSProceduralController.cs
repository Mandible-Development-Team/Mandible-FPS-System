using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using System;

using Mandible.PlayerController;
using Mandible.FPSController;
using Mandible.Entities;
using Mandible.Core;

using _PlayerController = Mandible.PlayerController.PlayerController;

namespace Mandible.FPSController
{   
    public class FPSProceduralController : _PlayerController
    {
        [Header("Systems")]
        [SerializeField] public WeaponSystem weaponSystem;

        [Header("Rig")]
        [SerializeField] public HumanoidProceduralRig proceduralRig;
        [SerializeField] public AimPivot aimPivot;

        [Header("Animation")]
        [SerializeField] public Animator animator;
        [SerializeField] private float speedSmoothness = 0.2f;
        [SerializeField] private float animationBlendSpeed = 0.5f;
        private float smoothedSpeed;
        
        [Header("Aim Sense")]
        [SerializeField] bool useAimSense = true;
        [SerializeField] AimSenseData aimSenseData = new AimSenseData()
        {
            autoCalculateForward = true,
            forwardCalculationWeight = 1f,
            autoCalculateRoll = true,
            rollCalculationWeight = 1f,

            autoCalculateForwardProcedural = true,
            calculateForwardStability = 5f,
            calculateForwardSpeed = 30f
        };
        [SerializeField] float aimSenseBlendSpeed = 0f;
        bool aimSenseEnabled = false;
        private AimSenseData defaultData = new AimSenseData();
        private AimSenseData currentData = new AimSenseData();

        [Header("Camera Stabilization")]
        [SerializeField] bool stabilizeCamera = true;
        [SerializeField] private float maxCameraStability = 1f;
        [SerializeField] private float cameraStabilizationBlendSpeed = 0.5f;

        [Header("Auto Detection")]
        [SerializeField] private bool autoDetectSystems = true;
        [SerializeField] private bool autoDetectReferences = true;

        [Header("States")]
        public MovementState currentMovementState = MovementState.Idle;
        public WeaponState currentWeaponState = WeaponState.Idle;
        public enum MovementState
        {
            Idle,
            Walking,
            Sprinting,
            Flying,
            Falling
        }
        public enum WeaponState
        {
            Idle,
            Walking,
            Sprinting,
            Firing,
            Reloading
        }
        private MovementState lastMovementState = (MovementState)(-1);
        private WeaponState lastWeaponState = (WeaponState)(-1);

        [Header("Advanced")]
        [SerializeField] float armWeightInterpolationSpeed = 15f;
        private float targetLeftArmWeight;
        private float targetRightArmWeight;

        //Actions
        Coroutine action;

        //Refs
        CameraController cameraController;

        protected override void Awake()
        {
            base.Awake();

            Initialize();
            AddCapabilities();
        }

        protected override void Start()
        {
            base.Start();
            GetReferences();
        }

        protected override void Update()
        {
            base.Update();

            //States
            UpdateState();
            UpdateAnimations();

            //Post Processing
            HandlePostProcessing();

            //Advanced
            UpdateProceduralRigWeights();
        }

        //Actions

        public void Throw(ThrowActionData data)
        {
            if(action != null) StopCoroutine(action);
            action = StartCoroutine(ThrowItemCoroutine(data));
        }

        IEnumerator ThrowItemCoroutine(ThrowActionData data)
        {
            ReleaseLeftGrip();

            //Animator
            int layerIndex = animator.GetLayerIndex("UpperBody");
            animator.Play("Throw", layerIndex, 0f);
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(layerIndex);
            float time = state.length;

            yield return new WaitForSeconds(time);
            SetGrip(1f, 1f);
        }

        public void ReleaseLeftGrip()
        {
            targetLeftArmWeight = 0f;
            proceduralRig.SetLeftGripWeight(0f);
        }

        public void ReleaseRightGrip()
        {
            targetRightArmWeight = 0f;
            proceduralRig.SetRightGripWeight(0f);
        }

        public void SetGrip(float leftWeight, float rightWeight)
        {
            targetLeftArmWeight = leftWeight;
            targetRightArmWeight = rightWeight;
            proceduralRig.SetGripWeight(leftWeight, rightWeight);
        }

        //States

        private void UpdateState()
        {
            UpdateMovementState();
            UpdateWeaponState();
        }

        private void UpdateMovementState()
        {
            if (IsFlying())
            {
                currentMovementState = MovementState.Flying;
                return;
            }
            else if (IsFalling())
            {
                currentMovementState = MovementState.Falling;
                return;
            }
            
            if (IsMoving())
            {
                currentMovementState = MovementState.Walking;
            }
            else
            {
                currentMovementState = MovementState.Idle;
                //currentMovementState = MovementState.Walking;
            }
        }

        private void UpdateWeaponState()
        {
            /*
            if (weaponSystem.IsReloading())
            {
                currentWeaponState = WeaponState.Reloading;
                return;
            }
            else if (weaponSystem.IsFiring())
            {
                currentWeaponState = WeaponState.Firing;
                return;
            }
            */
            if(IsSprinting())
            {
                currentWeaponState = WeaponState.Sprinting;
                return;
            }
            if (IsWalking())
            {
                currentWeaponState = WeaponState.Walking;
            }
            else
            {
                currentWeaponState = WeaponState.Idle;
            }
        }

        /*    
        void OnUpdateMovementState()
        {
            string movementState = GetMovementState(currentMovementState); 
            animator.CrossFade(movementState, animationBlendSpeed, 0);     
        }

        void OnUpdateWeaponState()
        {
            string weaponState = GetWeaponState(currentWeaponState);
            animator.CrossFade(weaponState, animationBlendSpeed, 1);
        }
        */

        //Animations

        private void UpdateAnimations()
        {
            if (animator == null) return;

            // Variables
            float speed = GetVelocityT();
            smoothedSpeed = Mathf.Lerp(smoothedSpeed, speed, 1/speedSmoothness * Time.deltaTime);

            animator.SetFloat("Speed", speed);
            animator.SetFloat("SmoothedSpeed", smoothedSpeed);

            animator.SetBool("IsFlying", IsFlying());
            animator.SetBool("IsFalling", IsFalling());
            animator.SetBool("IsSprinting", IsSprinting());
            
            //Movement State
            /*
            if (currentMovementState != lastMovementState){
                OnUpdateMovementState();
                lastMovementState = currentMovementState;
            }

            //WeaponState
            if (currentWeaponState != lastWeaponState){
                OnUpdateWeaponState();
                lastWeaponState = currentWeaponState;
            }
            */
        }

        string GetMovementState(MovementState state)
        {
            return state switch
            {
                MovementState.Idle => "Idle",
                MovementState.Walking => "Move",
                MovementState.Sprinting => "Sprint",
                MovementState.Flying => "Fly",
                MovementState.Falling => "Fall",
                _ => "Idle"
            };
        }

        string GetWeaponState(WeaponState state)
        {
            return state switch
            {
                WeaponState.Idle => "WeaponIdle",
                WeaponState.Walking => "WeaponMove",
                WeaponState.Sprinting => "WeaponSprint",
                WeaponState.Firing => "WeaponFire",
                WeaponState.Reloading => "WeaponReload",
                _ => "WeaponIdle"
            };
        }

        //Animation Events

        void OnHitGround(Vector3 impact)
        {
            animator.SetTrigger("OnHitGround");
        }

        void SetAnimatorEvents()
        {
            base.OnJump += OnJump;
            base.OnGroundImpact += OnHitGround;
        }
        
        //Post Processing

        void HandlePostProcessing()
        {
            if(useAimSense)
            {
                HandleAimSense();
            }

            if(stabilizeCamera)
            {
                StabilizeCamera();
            }
        }

        void HandleAimSense()
        {
            //Calculate
            aimSenseEnabled = CalculateAimSenseEnabled();

            AimSenseData targetData = aimSenseEnabled ? aimSenseData : defaultData;
            float blendSpeed = aimSenseBlendSpeed > 0f ? aimSenseBlendSpeed * Time.deltaTime : 1f;

            //Transfer
            currentData.autoCalculateForward = aimSenseData.autoCalculateForward; //Should be true
            currentData.autoCalculateRoll = aimSenseData.autoCalculateRoll; //Should be true

            currentData.autoCalculateForwardProcedural = aimSenseData.autoCalculateForwardProcedural; //Should be true
            currentData.calculateForwardSpeed = aimSenseData.calculateForwardSpeed; // Should be same as aimSenseData

            //Lerp
            currentData.forwardCalculationWeight = Mathf.Lerp(currentData.forwardCalculationWeight, targetData.forwardCalculationWeight, blendSpeed);
            currentData.rollCalculationWeight = Mathf.Lerp(currentData.rollCalculationWeight, targetData.rollCalculationWeight, blendSpeed);

            currentData.calculateForwardStability = Mathf.Lerp(currentData.calculateForwardStability, targetData.calculateForwardStability, blendSpeed);

            //Apply
            weaponSystem.HandleAimSense(currentData);
        }

        void StabilizeCamera()
        {
            float currentStability = cameraController.cameraStability;
            float targetStability = CalculateStabilizeCamera() ? maxCameraStability : 0f;
            float blendSpeed = cameraStabilizationBlendSpeed > 0f ? cameraStabilizationBlendSpeed * Time.deltaTime : 1f;

            if(cameraController != null)
            {
                cameraController.cameraStability = Mathf.Lerp(currentStability, targetStability, blendSpeed);
            }
        }

        //Capabilities
        void AddCapabilities()
        {
            //Actions
            capabilities.Add(new FPSThrowCapability());
        }

        //Initialization

        void Initialize()
        {
            if(autoDetectSystems) 
                AutoDetectSystems();
                
            if(autoDetectReferences) 
                AutoDetectReferences();

            //Events
            SetAnimatorEvents();

            //Advanced
            targetLeftArmWeight = proceduralRig.GetLeftArmWeight();
            targetRightArmWeight = proceduralRig.GetRightArmWeight();  
        }

        void AutoDetectSystems()
        {
            weaponSystem = GetComponentInChildren<WeaponSystem>();
        }

        void AutoDetectReferences()
        {
            proceduralRig = GetComponentInChildren<HumanoidProceduralRig>();
            aimPivot = GetComponentInChildren<AimPivot>();
            animator = GetComponentInChildren<Animator>();
        }

        void GetReferences()
        {
            if(camera != null) cameraController = camera.GetComponent<CameraController>();
        }

        //Advanced

        void UpdateProceduralRigWeights()
        {
            proceduralRig.SetArmWeight(
                Mathf.Lerp(proceduralRig.GetLeftArmWeight(), targetLeftArmWeight, Time.deltaTime * armWeightInterpolationSpeed),
                Mathf.Lerp(proceduralRig.GetRightArmWeight(), targetRightArmWeight, Time.deltaTime * armWeightInterpolationSpeed)
            );
        }

        //Editor

        #if UNITY_EDITOR
        public void OnValidate()
        {
            Initialize();
        }
        #endif

        //Helpers
        public void OnJump()
        {
            animator.SetTrigger("OnJump");
        }

        private const float FLYING_THRESHOLD = 1e-3f;
        public bool IsFlying()
        {
            return IsInAir() && currentVelocity.y >= FLYING_THRESHOLD;
        }

        private const float FALLING_THRESHOLD = 1e-3f;
        public bool IsFalling()
        {
            return IsInAir() && currentVelocity.y < FALLING_THRESHOLD;
        }

        public bool IsSprinting()
        {
            return false;
        }

        public bool CalculateAimSenseEnabled()
        {
            //Conditions to enable Aim Sense
            //if (!weaponSystem.IsAiming()) return false;
            if (IsSprinting()) return false;

            return true;
        }

        public bool CalculateStabilizeCamera()
        {
            if (!weaponSystem.IsAiming()) return false;

            return true;
        }

        //Getters/Setters
        
        public AimPivot GetAimPivot() => aimPivot;
        public HumanoidProceduralRig GetProceduralRig() => proceduralRig;
        public WeaponSystem GetWeaponSystem() => weaponSystem;
    }
}


