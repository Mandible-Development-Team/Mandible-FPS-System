using System.Collections;
using System.Collections.Generic;
using System;

using UnityEngine;
using UnityEngine.Events;

using Mandible.FPSController;
using Mandible.Entities;
using Mandible.Entities.StatusEffects;

namespace Mandible.FPSController
{
    public class Gun : Weapon
    {
        [SerializeField] GameObject currentHitObject;

        public enum GunState { Idling, Firing, Reloading }
        public enum GunPosition { Default, Aimed }

        public Dictionary<GunState, Func<IEnumerator>> GunActionMapping;

        [Header("Gun State")]
        public GunState gunState = GunState.Idling;
        public GunPosition positionState = GunPosition.Default;

        [Header("Flags")]
        public bool isRaycast = true;
        public bool isAutomatic = true;
        public bool isInfiniteAmmo = false;

        [Header("Gun Settings")]
        public float fireRate = 10f;
        public int bulletsPerShot = 1;
        public float reloadTime = 2f;
        [Space(4)]
        public float bulletSpeed = 1f;
        public float bulletForce = 1f;
        [SerializeField] public float spreadAngle = 6f;
        [SerializeField] public float spreadRadius = 0.05f;
        [Space(4)]
        public int ammoInMagazine;
        public int magazineSize = 30;
        public int spareAmmo = 90;

        [Header("Bullets and Trails")]
        public TrailRenderer bulletFire;
        public int trailPoolSize = 256;
        private HitscanTrailManager trailManager;

        [Header("Particles")]
        public Projectile projectilePrefab;
        public Transform muzzlePoint;
        public ParticleSystem muzzleFlash;
        public ParticleSystem bulletHit;

        //Events
        [HideInInspector] public UnityEvent OnFire = new UnityEvent();
        [HideInInspector] public UnityEvent OnReloadStart = new UnityEvent();
        [HideInInspector] public UnityEvent OnReloadComplete = new UnityEvent();
        [HideInInspector] public UnityEvent OnAim = new UnityEvent();
        [HideInInspector] public UnityEvent OnUnAim = new UnityEvent();

        //State
        private bool triggerHeld = false;
        private bool _hasFired = false;
        private bool isReloading = false;
        private float nextFireTime = 0f;

        private Coroutine _currentAction;
        
        [HideInInspector] public WeaponZoom zoom;

        //Test
        PlayerInputActions inputActions;

        protected override void Awake()
        {
            base.Awake();

            // Data
            isReloading = false;
            ammoInMagazine = magazineSize;

            // Actions
            GunActionMapping = new()
            {
                { GunState.Idling,    () => null },
                { GunState.Firing,    Fire },
                { GunState.Reloading, Reload }
            };

            // FX
            if (bulletFire != null)
            {
                trailManager = new HitscanTrailManager(bulletFire, trailPoolSize, null);
                trailManager.OnTrailHit += HandleTrailImpact;
            }

            // Input
            inputActions = new PlayerInputActions();
            inputActions.Enable();

            inputActions.Weapons.Fire.performed += ctx => triggerHeld = true;
            inputActions.Weapons.Fire.canceled += ctx => triggerHeld = false;

            inputActions.Weapons.Aim.performed += ctx => Aim();
            inputActions.Weapons.Aim.canceled += ctx => UnAim();

            inputActions.Weapons.Reload.performed += ctx => StartNewAction(GunState.Reloading);
        }

        protected void Start()
        {
            ownerCamera = owner?.Camera.GetComponent<Camera>();
        }

        public void LateUpdate()
        {
            if (!isEquipped) return;
            if (triggerHeld) Use();

            // Trails
            trailManager?.UpdateTrails();
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

            if (_currentAction != null) StopCoroutine(_currentAction);

            _currentAction = null;
            triggerHeld = false;
            isReloading = false;
        }

        public override void Use()
        {
            if (!CanUseWeapon()) return;
            if (gunState != GunState.Idling) return; // If action is in progress

            GunState stateToStart = GunState.Idling;
            if (ShouldReload())
            {
                stateToStart = GunState.Reloading;
            }
            else if (Time.time >= nextFireTime)
            {
                stateToStart = GunState.Firing;
            }

            if (stateToStart != GunState.Idling)
            {
                StartNewAction(stateToStart);
            }
        }

        private IEnumerator Fire()
        {
            base.Use(); // Firing is when we truly use the weapon
            OnFire?.Invoke();

            // Consume ammo
            ammoInMagazine--;

            // Muzzle & Bullet
            if (muzzleFlash != null)
            {
                muzzleFlash.Play(true);
            }

            for(int x = 0; x < bulletsPerShot; x++)
            {
                // Spread
                Vector3 origin = muzzlePoint.position;
                Vector3 cameraLook = ownerCamera.transform.position + (ownerCamera.transform.forward * 1000f);
                Vector3 shootDir = (cameraLook - origin).normalized;

                if (isRaycast)
                {
                    RaycastHit cameraAimHit;
                    shootDir = GetRaycastSpreadVector(origin, out cameraAimHit);

                    Vector3 actualWhiteEnd = cameraAimHit.collider != null ? cameraAimHit.point : cameraLook;
                    Debug.DrawLine(ownerCamera.transform.position, actualWhiteEnd, Color.white, 2f);

                    Ray ray = new Ray(origin, shootDir);
                    RaycastHit hit;

                    if (Physics.Raycast(ray, out hit, 1000f, hitMask, triggerInteraction))
                    {
                        Debug.DrawLine(origin, hit.point, Color.red, 2f);

                        // Ignore self
                        if (hit.collider.gameObject == (owner as MonoBehaviour).gameObject) continue;

                        ProcessHit(hit, shootDir);
                        EmitBulletfire(origin, hit.point, shootDir, hit.normal);
                        currentHitObject = hit.collider.gameObject;
                    }
                    else
                    {
                        Debug.DrawLine(origin, origin + (shootDir * 1000f), Color.red, 2f);

                        currentHitObject = null;
                        // Visuals only hit at max distance
                        EmitBulletfire(origin, origin + (shootDir * 1000f), shootDir, Vector3.up);
                    }
                }
                else
                {
                    FireProjectile(shootDir);
                }
            }

            // Set next fire time
            nextFireTime = Time.time + 1f / fireRate;
            yield return new WaitForSeconds(1f / fireRate);

            StopCurrentAction();
        }

        // Bullets & Projectiles
        private void FireProjectile(Vector3 dir)
        {
            if (projectilePrefab == null) return;
            Projectile p = Instantiate(projectilePrefab, muzzlePoint.position, Quaternion.LookRotation(dir));
            p.sender = (owner as MonoBehaviour).gameObject;
        }

        private void EmitBulletfire(Vector3 origin, Vector3 targetHitPoint, Vector3 dir, Vector3 normal)
        {
            trailManager.FireTrail(origin, targetHitPoint, dir, normal, bulletSpeed);
        }

        private const float DETECTION_DISTANCE = 1000f;
        Vector3 GetRaycastSpreadVector(Vector3 origin, out RaycastHit targetHit)
        {
            Ray cameraRay = new Ray(ownerCamera.transform.position, ownerCamera.transform.forward);
            bool hitSomething = Physics.Raycast(cameraRay, out targetHit, 1000f, hitMask, triggerInteraction);

            float distance = hitSomething ? Mathf.Max(targetHit.distance, 0.5f) : 1000f;
            Vector3 targetPoint = cameraRay.GetPoint(distance);

            Vector3 direction = targetPoint - origin;
            Vector3 baseDir = (direction.magnitude < 0.01f) ? ownerCamera.transform.forward : direction.normalized;

            float randYaw = UnityEngine.Random.Range(-spreadAngle, spreadAngle);
            float randPitch = UnityEngine.Random.Range(-spreadAngle, spreadAngle);
            Quaternion spreadRot = Quaternion.Euler(randPitch, randYaw, 0f);

            return (spreadRot * baseDir).normalized;
        }

        private void ProcessHit(RaycastHit hit, Vector3 shootDir)
        {
            var dmg = hit.collider.GetComponent<IDamageable>();
            if (CanDamage(dmg))
            {
                HitType type = dmg.GetHitType();

                OnHitTarget?.Invoke(type, hit, shootDir);

                HitData data = new HitData()
                {
                    hitTarget = dmg,
                    hitType = type,
                    hitAmount = damage,
                    hitInfo = hit,
                    hitDirection = shootDir
                };

                dmg.TakeDamage(damage, data);
                dmg.AddStatusEffectContribution(contribution);

                if(dmg.IsDead)
                {
                    OnKillTarget?.Invoke(type, hit, shootDir);
                }

                ExportHitData(data);
            }

            if (isRaycast)
            {
                var rb = hit.collider.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.AddForce(shootDir.normalized * bulletForce, ForceMode.Impulse);
                }
            }

            float distance = Vector3.Distance(muzzlePoint.transform.position, hit.point);
        }

        private void HandleTrailImpact(HitscanTrailManager.TrailHitData hitData)
        {
            if (bulletHit != null)
            {
                Quaternion rotation = Quaternion.LookRotation(hitData.normal);
                ParticleSystem effect = Instantiate(bulletHit, hitData.point, rotation);
                effect.Play(true);
                Destroy(effect.gameObject, 2f);
            }
        }

        //Actions
        public void StopCurrentAction()
        {
            if (_currentAction != null) StopCoroutine(_currentAction);
            
            _currentAction = null;
            gunState = GunState.Idling;
        }

        public void StartNewAction(GunState state, bool interrupt = false)
        {
            if (!interrupt && gunState != GunState.Idling) return; // If action is in progress
            StopCurrentAction();

            GunActionMapping.TryGetValue(state, out Func<IEnumerator> actionFunc);
            if (actionFunc == null)
            {
                Debug.LogError($"Mandible.FPSController.Gun: No action found for state {state}");
                return;
            }

            IEnumerator action = actionFunc.Invoke();
            if (action == null) return;

            gunState = state;
            _currentAction = StartCoroutine(action);
        }

        float _timeUntilReloadComplete = 0f;
        public float timeUntilReloadComplete => _timeUntilReloadComplete;
        private IEnumerator Reload()
        {
            isReloading = true;
            OnReloadStart?.Invoke();

            _timeUntilReloadComplete = 0f;
            while (timeUntilReloadComplete < reloadTime)
            {
                _timeUntilReloadComplete += Time.deltaTime;
                yield return null;
            }

            if (!isInfiniteAmmo)
            {
                int needed = magazineSize - ammoInMagazine;
                int toLoad = Mathf.Min(needed, spareAmmo);
                ammoInMagazine += toLoad;
                spareAmmo -= toLoad;
            }
            else
            {
                ammoInMagazine = magazineSize;
            }

            OnReloadComplete?.Invoke();
            isReloading = false;

            StopCurrentAction();
        }

        //States

        public void Aim()
        {
            if (isDisabled) return;
            positionState = GunPosition.Aimed;
            OnAim?.Invoke();
        }

        public void UnAim()
        {
            if (isDisabled) return;
            positionState = GunPosition.Default;
            OnUnAim?.Invoke();
        }

        public void ResetState()
        {
            positionState = GunPosition.Default;
            gunState = GunState.Idling;
        }

        protected override bool CanUseWeapon()
        {
            if(!base.CanUseWeapon()) return false;
            if (isReloading) return false;
            if (ammoInMagazine <= 0 && !isInfiniteAmmo) return false;
            return true;
        }

        protected bool CanDamage(IDamageable dmg)
        {
            if (dmg == null) return false;
            if ((dmg as MonoBehaviour).gameObject == (owner as MonoBehaviour).gameObject) return false;
            if (dmg.IsDead) return false;

            return true;
        }

        protected bool ShouldReload()
        {
            return ammoInMagazine <= 0 && (spareAmmo > 0 || isInfiniteAmmo);
        }

        //Getters/Setters
        public int AmmoInMagazine => ammoInMagazine;
        public int SpareAmmo => spareAmmo;
        public bool IsReloading => isReloading;
    }
}
