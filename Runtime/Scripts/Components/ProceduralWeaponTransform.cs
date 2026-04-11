using UnityEngine;
using System.Collections;
using System.Collections.Generic;

using Mandible.FPSController;
using Mandible.PlayerController;

using System.Buffers;
//using System.Diagnostics;

namespace Mandible.FPSController{
    [DefaultExecutionOrder(-100)]
    public class ProceduralWeaponTransform : MonoBehaviour
    {
        public Weapon weapon;
        [Header("References")]
        public Transform parentTransform;
        public AimPivot aimPivot;
        public Transform handle;
        public Transform forwardTransform;
        
        [Header("Transform")]
        [SerializeField] protected bool updateTransform = true;
        public DirectionalAxis forwardAxis = DirectionalAxis.PositiveZ;
        public enum DirectionalAxis
        {
            [InspectorName("+Z")] PositiveZ,
            [InspectorName("-Z")] NegativeZ,
            [InspectorName("+X")] PositiveX,
            [InspectorName("-X")] NegativeX,
            [InspectorName("+Y")] PositiveY,
            [InspectorName("-Y")] NegativeY
        }
        [SerializeField] public Quaternion rotationOffset = Quaternion.identity;
        [SerializeField] public Vector3 positionOffset = Vector3.zero;

        [Header("Procedural Positioning")]
        [SerializeField] protected Vector3 defaultPosition;
        [SerializeField] protected float transitionSpeed = 1f;

        [Header("Advanced")]
        [SerializeField] bool disableProcedural = false;

        private Mandible.FPSController.Player player;
        private FPSProceduralController controller;
        private HumanoidProceduralRig hpr;

        //Base
        protected Quaternion baseRot = Quaternion.identity;
        protected Vector3 basePos = Vector3.zero;

        //Position
        Vector3 currentProceduralPosition = default;

        //Mods
        [SerializeField] public List<ProceduralWeaponModifier> modifiers = new List<ProceduralWeaponModifier>();
        [HideInInspector] public Quaternion rotationMod = Quaternion.identity;
        [HideInInspector] public Vector3 positionMod = Vector3.zero;

        //Cache
        private Quaternion initialRotation;
        private Quaternion initialRotationParent;

        private Quaternion initialRotationPivot;
        private Quaternion initialLocalRotationPivot;
        private Quaternion initialRotationPivotParent;

        //Flags
        protected bool hasInitializedPostProcessingCache = false;

        void Awake()
        {
            //References
            weapon = GetComponentInChildren<Weapon>();
            if(!weapon) Debug.LogError("ProceduralGunTransform: No Weapon component found in children.");

            //Transform
            initialRotation = transform.rotation;
            if(parentTransform) initialRotationParent = parentTransform.rotation;

            //Aim Pivot
            if(aimPivot) initialRotationPivot = aimPivot.transform.rotation;
            if(aimPivot) initialLocalRotationPivot = aimPivot.transform.rotation * Quaternion.Inverse(aimPivot.transform.parent.rotation);
            if(aimPivot) initialRotationPivotParent = aimPivot.transform.parent.rotation;

            //Modifiers
            InitializeProceduralWeaponModifiers();
        }

        void Start()
        {
            //Other References
            player = (Mandible.FPSController.Player)weapon?.owner;
            controller = (FPSProceduralController)player?.Controller;
            hpr = controller?.proceduralRig;

            //Post Process
            hpr.onPostProcessCompleted += UpdateNonProcedural;
        }

        void Update()
        {
            HandleProceduralWeaponModifiers();
        }

        void LateUpdate()
        {
            if(disableProcedural) return;

            if(CanUpdate()) UpdateTransform();

            PostProcessingPass();
        }

        //Transform
        public void UpdateNonProcedural()
        {
            if(!disableProcedural) return;

            Quaternion targetRot = parentTransform.rotation * GetForwardRotation() * rotationMod;
            transform.rotation = targetRot;

            Vector3 offsetFromHandle = handle.position - transform.position;
            transform.position = parentTransform.position - offsetFromHandle;

            Debug.DrawRay(parentTransform.position, parentTransform.right, Color.red);
            Debug.DrawRay(parentTransform.position, parentTransform.up, Color.green);
            Debug.DrawRay(parentTransform.position, parentTransform.forward, Color.blue);
        }

        public void UpdateTransform()
        {
            if (!parentTransform) return;
    
            //Rotation
            baseRot = Quaternion.identity;
            baseRot *= aimPivot.transform.rotation;   // aim rotation
            baseRot *=  Quaternion.Inverse(aimPivot.transform.parent.rotation) * parentTransform.rotation; // isolated anim rotation
            baseRot *= GetForwardRotation(); // relative forward

            // Position
            basePos = Vector3.zero; 
            basePos += GetProceduralPosition();
        }

        public Vector3 GetForwardAxis()
        {
            return AxisToVector(forwardAxis);
        }

        public Vector3 AxisToVector(DirectionalAxis axis)
        {
            switch (axis)
            {
                case DirectionalAxis.PositiveZ: return Vector3.forward;
                case DirectionalAxis.NegativeZ: return Vector3.back;
                case DirectionalAxis.PositiveX: return Vector3.right;
                case DirectionalAxis.NegativeX: return Vector3.left;
                case DirectionalAxis.PositiveY: return Vector3.up;
                case DirectionalAxis.NegativeY: return Vector3.down;
                default: return Vector3.forward;
            }
        }

        public Quaternion GetForwardRotation()
        {
            Vector3 weaponForward = AxisToVector(forwardAxis); // Weapon's local forward
            return Quaternion.FromToRotation(weaponForward, Vector3.forward);
        }

        //Procedural Positioning
        public Vector3 GetProceduralPosition()
        {
            System.Enum positionState = ReadWeaponPositionState();
            Vector3 targetBasePos = PositionFromState(positionState);
            
            currentProceduralPosition = Vector3.Lerp(currentProceduralPosition, targetBasePos, transitionSpeed * Time.deltaTime);

            Vector3 proceduralPosition = currentProceduralPosition + GetPositionalSway();
            return proceduralPosition;
        }

        public virtual System.Enum ReadWeaponPositionState() => default;
        public virtual Vector3 PositionFromState(System.Enum state) => defaultPosition;

        //Weapon Sway
        [Header("Weapon Sway")]
        public float swayMultiplier = 0.002f;
        public float maxSway = 0.08f;
        public float swaySpeed = 8f;
        [Space(4)]
        public Vector3 rotSwayMultiplier = new Vector3(0.5f, 0.3f, 0.2f);
        public Vector3 maxRotSway = new Vector3(4f, 3f, 2f);
        public float rotSwaySpeed = 8f;

        private Vector3 currentSway;
        private Vector3 swayVelocity;
        private Vector3 currentRotSwayEuler;
        private Vector3 targetRotSwayEuler;

        protected Vector3 GetPositionalSway()
        {
            if (!aimPivot) return Vector3.zero;

            Vector3 vel = aimPivot.GetScreenVelocity();
            Vector3 targetSway = new Vector3(-vel.x, -vel.y, 0f) * swayMultiplier;
            targetSway.x = Mathf.Clamp(targetSway.x, -maxSway, maxSway);
            targetSway.y = Mathf.Clamp(targetSway.y, -maxSway, maxSway);

            float alpha = 1f - Mathf.Exp(-swaySpeed * Time.deltaTime);
            currentSway = Vector3.Lerp(currentSway, targetSway, alpha);

            return currentSway;
        }

        protected Quaternion GetRotationalSway()
        {
            if (!aimPivot) return Quaternion.identity;

            Vector3 vel = aimPivot.GetScreenVelocity();

            targetRotSwayEuler.x = -vel.y * rotSwayMultiplier.x;   // pitch
            targetRotSwayEuler.y =  vel.x * rotSwayMultiplier.y;   // yaw
            targetRotSwayEuler.z = -vel.x * rotSwayMultiplier.z;   // roll

            // Clamp
            targetRotSwayEuler.x = Mathf.Clamp(targetRotSwayEuler.x, -maxRotSway.x, maxRotSway.x);
            targetRotSwayEuler.y = Mathf.Clamp(targetRotSwayEuler.y, -maxRotSway.y, maxRotSway.y);
            targetRotSwayEuler.z = Mathf.Clamp(targetRotSwayEuler.z, -maxRotSway.z, maxRotSway.z);

            // Smooth using same exponential lerp as positional sway
            float alpha = 1f - Mathf.Exp(-rotSwaySpeed * Time.deltaTime);
            currentRotSwayEuler = Vector3.Lerp(currentRotSwayEuler, targetRotSwayEuler, alpha);

            return Quaternion.Euler(currentRotSwayEuler);
        }

        //Post Processing
        public void InitializePostProcessingCache()
        {
            hasInitializedPostProcessingCache = true;
        }

        public virtual void PostProcessingPass()
        {
            //Post-Process Rotation
            if(!hasInitializedPostProcessingCache) InitializePostProcessingCache();
            
            //Rotational Sway / Lag
            baseRot *= GetRotationalSway();

            //Modifiers (staged after post processing for better behavior)
            rotationMod = Quaternion.identity;
            positionMod = Vector3.zero;
            foreach(var modifier in modifiers)
            {
                rotationMod *= modifier.GetRotationOffset();
                positionMod += modifier.GetPositionOffset();
            }
            baseRot *= rotationMod;
            basePos += positionMod;

            //Apply
            transform.rotation = baseRot; 
            transform.position = aimPivot.transform.TransformPoint(basePos);
        }

        //Modifiers
        void InitializeProceduralWeaponModifiers()
        {
            modifiers = new List<ProceduralWeaponModifier>(GetComponents<ProceduralWeaponModifier>());
            foreach (var modifier in modifiers)
            {
                //weapon.OnWeaponUse.AddListener(modifier.OnUse);
                modifier.Initialize(weapon, weapon.owner);
            }
        }

        void HandleProceduralWeaponModifiers()
        {
            foreach (var modifier in modifiers)
            {
                modifier.Handle();
            }      
        }

        void ResetProceduralWeaponModifiers()
        {
            modifiers = new List<ProceduralWeaponModifier>(GetComponents<ProceduralWeaponModifier>());
            foreach (var modifier in modifiers)
            {
                modifier.Reset();
            }      
        }

        //Helpers
        public bool CanUpdate()
        {
            //Flag
            if (!updateTransform)
            {
                return false;
            }

            //References
            if(!parentTransform)
            {
                Debug.LogWarning("ProceduralChildTransform: Cannot update transform. Update requires Parent Transform.");
                return false;
            }
            if(!aimPivot)
            {
                Debug.LogWarning("ProceduralChildTransform: Cannot update transform. Update requires Aim Pivot.");
                return false;
            }
            if(!forwardTransform)
            {
                Debug.LogWarning("ProceduralChildTransform: Cannot update transform. Update requires Forward Transform (for auto-calculation cache).");
                return false;
            }

            return true;
        }

        //DEPRECATED 
        /*
        public void UpdateTransform_ExplicitQuaternion()
        {
            if (!parentTransform) return;
    
            baseRot = Quaternion.identity;
            basePos = Vector3.zero; 

            //Rotation
            if(aimPivot) // Isolate pivot rotation to not double count
            {
                Quaternion localPivot = aimPivot.transform != null ? Quaternion.Inverse(aimPivot.transform.parent.rotation) : Quaternion.identity;
                Quaternion offsetPivot = initialLocalRotationPivot * localPivot;

                baseRot *= aimPivot.transform.rotation * offsetPivot;
            }

            baseRot *= rotationOffset; // Custom offset applied after pivot, used for weapon types with different forwards
            baseRot *= parentTransform.rotation;

            transform.rotation = baseRot;

            // Position
            Vector3 proceduralLocalPos = positionOffset + positionMod;
            Vector3 animLocalPos = parentTransform.position - aimPivot.transform.position;

            basePos = proceduralPosition ? proceduralLocalPos : animLocalPos;

            if(aimPivot)
            {
                transform.position = aimPivot.transform.TransformPoint(basePos);
            }
            else
            {
                transform.position = basePos;
            }
        }
        */
    }
}
