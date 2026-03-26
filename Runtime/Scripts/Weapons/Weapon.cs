using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

using Mandible.FPSController;
using Mandible.Entities;
using Mandible.Entities.StatusEffects;
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
        [HideInInspector] public ProceduralGunTransform pt;
        [HideInInspector] public bool isEquipped;

        [Header("Weapon Settings")]
        public float damage;
        public LayerMask hitMask;

        [Header("Status Effects")]
        public StatusEffectContribution contribution;

        //Procedural
        [Header("Transform")]
        private Quaternion rotationOffset = Quaternion.identity;
        protected Vector3 positionOffset = Vector3.zero;

        [Header("Handles")]
        public Transform handle;
        public Transform foreHandle;

        [Header("UI / Config")]
        [HideInInspector] public Sprite icon;
        [HideInInspector] public List<WeaponComponent> components = new List<WeaponComponent>();
        
        //Events
        [Header("Weapon Events")]
        [HideInInspector] public UnityEvent OnWeaponEquip = new UnityEvent();
        [HideInInspector] public UnityEvent OnWeaponUse = new UnityEvent();
        [HideInInspector] public UnityEvent OnWeaponUnequip = new UnityEvent();

        [Header("Hits")]
        [HideInInspector] public List<HitData> hitData = new List<HitData>();
        [HideInInspector] public UnityEvent <HitType, RaycastHit, Vector3> OnHitTarget;
        [HideInInspector] public UnityEvent <HitType, RaycastHit, Vector3> OnKillTarget;
        
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
            //Components
            HandleWeaponComponents();

            HandleData();
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

        //Procedural

        void ApplyTransformMod()
        {
            Quaternion rotationMod = Quaternion.identity;
            Vector3 positionMod = Vector3.zero;

            foreach (var component in components)
            {
                rotationMod *= component.GetRotationOffset();
                positionMod += component.GetPositionOffset();
            }

            //ProceduralTransform
            if (pt != null)
            {
                pt.rotationOffset = rotationOffset;
                pt.rotationMod = rotationMod;
                pt.positionOffset = positionOffset;
                pt.positionMod = positionMod;
            }
            else
            {
                transform.localRotation = rotationOffset * rotationMod;   
                transform.localPosition = positionOffset + positionMod;
            }
        }

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

            if(pt == null)
            {
                pt = GetComponent<ProceduralGunTransform>();
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

        //Data

        public void HandleData()
        {   
            hitData.Clear();
        }

        public HitData ExportHitData(HitData data)
        {
            hitData.Add(data);
            return data;
        }
        
        public HitData ExportHitData(HitType hitType = HitType.Normal, RaycastHit hitInfo = default(RaycastHit), Vector3 hitDirection = default(Vector3), float hitAmount = 0f)
        {
            HitData data = new HitData();
            data.hitType = hitType;
            data.hitInfo = hitInfo;
            data.hitDirection = hitDirection;
            data.hitAmount = hitAmount;

            hitData.Add(data);
            return data;
        }

        //Setters
        public void SetPositionOffset(Vector3 newPosition)
        {
            positionOffset = newPosition;
        }
    }
}
