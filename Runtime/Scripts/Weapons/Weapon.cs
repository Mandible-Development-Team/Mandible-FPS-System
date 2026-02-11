using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

using Mandible.FPSController;
using Mandible.Entities;
using Mandible.PlayerController;

namespace Mandible.FPSController
{
    [RequireComponent(typeof(ProceduralGunTransform))] 
    [DefaultExecutionOrder(-150)]
    public class Weapon : MonoBehaviour
    {
        public GameObject ownerObject;
        public IPlayer owner;
        public Camera ownerCamera;
        public ProceduralGunTransform proceduralGunTransform;

        [Header("Weapon Settings")]
        public bool isEquipped;
        public float damage;

        [Header("Transform")]
        private Quaternion rotationOffset = Quaternion.identity;
        protected Vector3 positionOffset = Vector3.zero;

        [Header("Handles")]
        public Transform handle;
        public Transform foreHandle;

        [Header("UI / Config")]
        public Sprite icon;
        public List<WeaponComponent> components = new List<WeaponComponent>();

        [Header("Weapon Events")]
        public UnityEvent OnWeaponEquip = new UnityEvent();
        public UnityEvent OnWeaponUse = new UnityEvent();
        public UnityEvent OnWeaponUnequip = new UnityEvent();

        [Header("Hit Events")]
        public UnityEvent <HitType, RaycastHit, Vector3> OnHitTarget;
        public UnityEvent <HitType, RaycastHit, Vector3> OnKillTarget;

        [Header("Input Events")]
        public UnityEvent OnTriggerEvent = new UnityEvent();
        public UnityEvent OnTriggerDownEvent = new UnityEvent();
        public UnityEvent OnTriggerUpEvent = new UnityEvent();

        public UnityEvent OnAlternateTriggerEvent = new UnityEvent();
        public UnityEvent OnAlternateTriggerDownEvent = new UnityEvent();
        public UnityEvent OnAlternateTriggerUpEvent = new UnityEvent();
        
        [Header("Debug")]
        public bool debug = false;

        protected virtual void Awake()
        {
            InitializeDefaults();
            InitializeWeaponComponents();
        }

        void Start()
        {
            
        }

        void Update()
        {
            HandleWeaponComponents();
        }

        public virtual void LateUpdate()
        {
            ApplyTransformMod();
        }

        public virtual void Use()
        {
            if (!isEquipped) return;
            if (!CanUseWeapon()) return;

            OnWeaponUse.Invoke();
        }

        public virtual void Equip()
        {
            isEquipped = true;
            OnWeaponEquip.Invoke();
        }

        public virtual void Unequip()
        {
            isEquipped = false;
            OnWeaponUnequip.Invoke();
        }

        public virtual void OnTrigger() { }

        public virtual void OnTriggerDown() { }

        public virtual void OnTriggerUp() { }

        public virtual void OnAlternateTrigger(){ }

        public virtual void OnAlternateTriggerDown() { }

        public virtual void OnAlternateTriggerUp() { }

        void ApplyTransformMod()
        {
            Quaternion rotationMod = Quaternion.identity;
            Vector3 positionMod = Vector3.zero;

            foreach (var component in components)
            {
                rotationMod *= component.GetRotationOffset();
                positionMod += component.GetPositionOffset();
            }

            //ProceduralGunTransform

            ProceduralGunTransform pgt = GetComponent<ProceduralGunTransform>();
            if (pgt != null)
            {
                pgt.rotationOffset = rotationOffset;
                pgt.rotationMod = rotationMod;
                pgt.positionOffset = positionOffset;
                pgt.positionMod = positionMod;
            }
            else
            {
                transform.localRotation = rotationOffset * rotationMod;   
                transform.localPosition = positionOffset + positionMod;
            }
        }

        public virtual void Aim() { }

        protected virtual bool CanUseWeapon()
        {
            return true;
        }

        //Initialization

        public void Initialize(GameObject ownerObject)
        {
            this.ownerObject = ownerObject;

            InitializeDefaults();
        }

        void InitializeDefaults()
        {
            owner = ownerObject.GetComponent<IPlayer>();

            if(owner != null) 
            {
                ownerCamera = owner.Camera.GetComponent<Camera>();
            }

            if(proceduralGunTransform == null)
            {
                proceduralGunTransform = GetComponent<ProceduralGunTransform>();
            }

            if(proceduralGunTransform != null)
            {
                //Will initialize with FPSProceduralController values (e.g. aim pivot, parent transform)

                //proceduralGunTransform.Initialize();
            }
        }

        //Weapon Components
        void InitializeWeaponComponents()
        {
            components = new List<WeaponComponent>(GetComponents<WeaponComponent>());
            foreach (var component in components)
            {
                component.Initialize(weapon: this, owner: owner);
            }
        }

        void HandleWeaponComponents()
        {
            foreach (var component in components)
            {
                component.Handle();
            }      
        }

        void ResetWeaponComponents()
        {
            components = new List<WeaponComponent>(GetComponents<WeaponComponent>());
            foreach (var component in components)
            {
                component.Reset();
            }      
        }

        //Setters
        public void SetPositionOffset(Vector3 newPosition)
        {
            positionOffset = newPosition;
        }
    }
}
