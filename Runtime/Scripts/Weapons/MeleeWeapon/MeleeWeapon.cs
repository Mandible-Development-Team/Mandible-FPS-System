using System.Collections;

using UnityEngine;
using UnityEngine.Events;

using Mandible.FPSController;
using Mandible.Entities;
using Mandible.Entities.StatusEffects;

namespace Mandible.FPSController
{
    public class MeleeWeapon : Weapon
    {
        public enum WeaponState { Idling, Swinging }
        public enum WeaponPosition { Default, Charged }

        [Header("Weapon State")]
        public WeaponState weaponState = WeaponState.Idling;
        public WeaponPosition positionState = WeaponPosition.Default;

        [Header("Weapon Settings")]
        public float attackSpeed = 1f;
        public float hitRadius = 0.5f;
        public Vector3 hitOffset = Vector3.zero;
        
        [Header("Procedural Aim Positioning")]
        [SerializeField] Vector3 defaultPosition;
        [SerializeField] float transitionSpeed = 1f;
        [HideInInspector] Vector3 weaponPosition = default;

        //State
        private bool isInUse = false;
        private bool triggerHeld = false;
        private float nextFireTime = 0f;

        private Coroutine currentCoroutine;

        //Test
        PlayerInputActions inputActions;

        protected override void Awake()
        {
            base.Awake();

            isInUse = false;
            weaponPosition = transform.localPosition;

            //INPUT ACTIONS TEST
            inputActions = new PlayerInputActions();
            inputActions.Enable();

            inputActions.Player.Fire.performed += ctx => triggerHeld = true;
            inputActions.Player.Fire.canceled += ctx => triggerHeld = false;
        }

        protected void Start()
        {
            ownerCamera = owner?.Camera.GetComponent<Camera>();
        }

        public void LateUpdate()
        {
            UpdatePosition();

            //if (!isEquipped) return;

            if (triggerHeld) Use();
        }

        void OnEnable()
        {
            inputActions?.Enable();
        }

        void OnDisable()
        {
            inputActions?.Disable();
        }

        public override void Unequip()
        {
            base.Unequip();
            ResetState();

            StopAllCoroutines();
            currentCoroutine = null;

            isInUse = false;
        }

        public override void Use()
        {
            if (Time.fixedTime < nextFireTime) return;
            base.Use();
            currentCoroutine = StartCoroutine(Hit());
        }

        private IEnumerator Hit()
        {
            // Set next fire time
            nextFireTime = Time.time + 1f / attackSpeed;
            
            HitWithSphere();

            yield return new WaitForSeconds(1f / attackSpeed);
        }

        private void HitWithSphere()
        {
            Vector3 origin = ownerCamera.transform.position + ownerCamera.transform.rotation * hitOffset;

            DebugDrawSphere(origin, hitRadius, Color.blue, 1f);

            Collider[] hits = Physics.OverlapSphere(origin, hitRadius, hitMask, QueryTriggerInteraction.Collide);

            foreach (var col in hits)
            {
                ProcessHit(col, ownerCamera.transform.forward);
                Debug.DrawLine(origin, col.bounds.center, Color.red, 1f);
            }
        }

        //Delete!
        private void DebugDrawSphere(Vector3 position, float radius, Color color, float duration)
        {
            int segments = 16;
            float angle = 360f / segments;

            for (int i = 0; i < segments; i++)
            {
                float a1 = Mathf.Deg2Rad * angle * i;
                float a2 = Mathf.Deg2Rad * angle * (i + 1);

                Vector3 p1 = position + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
                Vector3 p2 = position + new Vector3(Mathf.Cos(a2), 0f, Mathf.Sin(a2)) * radius;
                Debug.DrawLine(p1, p2, color, duration);

                Vector3 p3 = position + new Vector3(0f, Mathf.Cos(a1), Mathf.Sin(a1)) * radius;
                Vector3 p4 = position + new Vector3(0f, Mathf.Cos(a2), Mathf.Sin(a2)) * radius;
                Debug.DrawLine(p3, p4, color, duration);

                Vector3 p5 = position + new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f) * radius;
                Vector3 p6 = position + new Vector3(Mathf.Cos(a2), Mathf.Sin(a2), 0f) * radius;
                Debug.DrawLine(p5, p6, color, duration);
            }
        }
        //End

        private void ProcessHit(Collider col, Vector3 forward)
        {
            var dmg = col.GetComponent<IDamageable>();
            if (CanDamage(dmg))
            {
                HitType type = dmg.GetHitType();
                OnHitTarget?.Invoke(type, new RaycastHit(), forward);

                HitData data = new HitData
                {
                    hitTarget = dmg,
                    hitType = type,
                    hitAmount = damage,
                    hitInfo = new RaycastHit(),
                    hitDirection = forward.normalized
                };
                dmg.TakeDamage(damage, data);
                dmg.AddStatusEffectContribution(contribution);

                if (dmg.IsDead)
                {
                    OnKillTarget?.Invoke(type, new RaycastHit(), forward);
                }
            }
        }

        //States

        public void UpdatePosition()
        {
            switch (positionState)
            {
                case WeaponPosition.Default:
                    weaponPosition = defaultPosition;
                    break;
            }
            
            Vector3 offset = Vector3.Lerp(positionOffset, weaponPosition, transitionSpeed);
            SetPositionOffset(offset);
        }

        public void ResetState()
        {
            positionState = WeaponPosition.Default;
            weaponState = WeaponState.Idling;
        }

        protected override bool CanUseWeapon()
        {
            if (isInUse) return false;
            return true;
        }

        protected bool CanDamage(IDamageable dmg)
        {
            if (dmg == null) return false;
            if ((dmg as MonoBehaviour).gameObject == (owner as MonoBehaviour).gameObject) return false;
            if (dmg.IsDead) return false;

            return true;
        }

        //Getters/Setters
        public bool IsInUse => isInUse;
    }
}
