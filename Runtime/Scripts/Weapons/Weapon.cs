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
    [RequireComponent(typeof(ProceduralWeaponTransform))] 
    [DefaultExecutionOrder(-150)]
    public class Weapon : MonoBehaviour
    {
        public GameObject ownerObject;
        public IPlayer owner;
        public Camera ownerCamera;
        [HideInInspector] public ProceduralWeaponTransform pt;
        [HideInInspector] public bool isEquipped;

        [Header("Weapon Settings")]
        public float damage;
        public LayerMask hitMask;

        [Header("Status Effects")]
        public StatusEffectContribution contribution;

        protected Vector3 positionOffset = Vector3.zero;

        [Header("Handles")]
        public Transform handle;
        public Transform foreHandle;

        [Header("UI / Config")]
        public Sprite icon;

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
        }

        void Start()
        {
            
        }

        void Update()
        {
            HandleData();
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

        //Flags
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
