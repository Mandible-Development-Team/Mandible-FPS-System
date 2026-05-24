using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Events;

using Mandible.FPSController;
using Mandible.Entities;
using Mandible.Entities.StatusEffects;

namespace Mandible.FPSController
{
    public class Gun : Weapon
    {
        public enum GunState { Idling, Firing, Reloading }
        public enum GunPosition { Default, Aimed }

        [Header("Gun State")]
        public GunState gunState = GunState.Idling;
        public GunPosition positionState = GunPosition.Default;

        [Header("Flags")]
        public bool isRaycast = true;
        public bool isAutomatic = true;
        public bool isInfiniteAmmo = false;

        [Header("Gun Settings")]
        public float fireRate = 10f;
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

        [Header("Particles")]
        public Projectile projectilePrefab;
        public Transform muzzlePoint;
        public ParticleSystem muzzleFlash;
        public ParticleSystem bulletFire;
        public ParticleSystem bulletHit;

        //Events
        [HideInInspector] public UnityEvent OnAim = new UnityEvent();
        [HideInInspector] public UnityEvent OnUnAim = new UnityEvent();

        //State
        private bool triggerHeld = false;
        private bool isReloading = false;
        private float nextFireTime = 0f;

        private Coroutine currentCoroutine;
        
        [HideInInspector] public WeaponZoom zoom;

        //Test
        PlayerInputActions inputActions;

        protected override void Awake()
        {
            base.Awake();

            isReloading = false;
            ammoInMagazine = magazineSize;

            //INPUT ACTIONS TEST
            inputActions = new PlayerInputActions();
            inputActions.Enable();

            inputActions.Player.Fire.performed += ctx => triggerHeld = true;
            inputActions.Player.Fire.canceled += ctx => triggerHeld = false;

            inputActions.Player.Aim.performed += ctx => Aim();
            inputActions.Player.Aim.canceled += ctx => UnAim();
        }

        protected void Start()
        {
            ownerCamera = owner?.Camera.GetComponent<Camera>();
        }

        public void LateUpdate()
        {
            if (!isEquipped) return;


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

            if (currentCoroutine != null)
                StopCoroutine(currentCoroutine);
            StopAllCoroutines();

            isReloading = false;
        }

        public override void Use()
        {
            if (Time.fixedTime < nextFireTime) return;
            base.Use();
            currentCoroutine = StartCoroutine(Fire());
        }

        public void ReloadInput()
        {
            if (ammoInMagazine < magazineSize && spareAmmo > 0)
            {
                currentCoroutine ??= StartCoroutine(Reload());
            }
        }

        private IEnumerator Fire()
        {
            if (ammoInMagazine <= 0 && (spareAmmo > 0 || isInfiniteAmmo))
            {
                yield return currentCoroutine ??= StartCoroutine(Reload());
                currentCoroutine = null;
                yield break;
            }

            // Consume ammo
            ammoInMagazine--;

            // Muzzle & Bullet
            if (muzzleFlash != null)
            {
                muzzleFlash.Play(true);
            }

            // Spread
            Vector3 origin = Vector3.zero;
            Vector3 shootDir = GetSpreadDirection(out origin);
            
            if (bulletFire != null) EmitBulletfire(origin, shootDir);

            if (isRaycast)
            {
                Ray ray = new Ray(origin, shootDir);
                
                if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, hitMask)){
                    ProcessHit(hit, shootDir);
                }

                if (debug) Debug.DrawRay(origin, shootDir * 100f, Color.red, 1f);
            }
            else
            {
                FireProjectile(shootDir);
            }

            // Set next fire time
            nextFireTime = Time.time + 1f / fireRate;

            yield return new WaitForSeconds(1f / fireRate);
            currentCoroutine = null;
        }

        //Bullets & Projectiles

        private void FireProjectile(Vector3 dir)
        {
            if (projectilePrefab == null) return;
            Projectile p = Instantiate(projectilePrefab, muzzlePoint.position, Quaternion.LookRotation(dir));
            p.sender = (owner as MonoBehaviour).gameObject;
        }

        /*
        private void EmitBulletfire(Vector3 origin, Vector3 shootDir)
        {
            ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams();
            
            emitParams.velocity = shootDir.normalized * bulletSpeed;
            if(bulletFire.main.simulationSpace != ParticleSystemSimulationSpace.World){
                Debug.LogWarning("Bullet Fire Particle System should be set to World simulation space for correct bullet direction.");
                emitParams.position = origin - muzzlePoint.position;
            }
            else
            {
                if(emitParams.)
                Vector3 initialOffset = shootDir * bulletSpeed * Time.deltaTime;
                emitParams.position = origin - initialOffset;
            }

            bulletFire.Emit(emitParams, 1);
            Debug.Log(emitParams.position);
        }
        */

        private void EmitBulletfire(Vector3 origin, Vector3 shootDir)
        {
            if (bulletFire == null) return;

            bulletFire.transform.position = origin;
            bulletFire.transform.rotation = Quaternion.LookRotation(shootDir);

            float nudgeDistance = 0f;
            bulletFire.transform.position += shootDir.normalized * nudgeDistance;

            bulletFire.Play();
        }

        private const float DETECTION_DISTANCE = 1000f;
        Vector3 GetSpreadDirection(out Vector3 origin)
        {
            var cam = owner.Camera.transform;

            Ray camRay = new Ray(cam.position, cam.forward);

            Vector3 targetPoint;

            if (Physics.Raycast(camRay, out RaycastHit camHit, DETECTION_DISTANCE, hitMask))
            {
                targetPoint = camHit.point;
            }
            else
            {
                targetPoint = cam.position + cam.forward * DETECTION_DISTANCE;
            }

            origin = muzzlePoint.position;

            Vector3 baseDir = (targetPoint - origin).normalized;

            float randYaw   = Random.Range(-spreadAngle, spreadAngle);
            float randPitch = Random.Range(-spreadAngle, spreadAngle);

            Quaternion spreadRot = Quaternion.Euler(randPitch, randYaw, 0f);
            Vector3 shootDir = spreadRot * baseDir;

            return shootDir.normalized;
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

        //Actions

        private IEnumerator Reload()
        {
            isReloading = true;

            yield return new WaitForSeconds(reloadTime);

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

            isReloading = false;
            currentCoroutine = null;
        }

        //States

        public void Aim()
        {
            positionState = GunPosition.Aimed;
            OnAim?.Invoke();
        }

        public void UnAim()
        {
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

        //Getters/Setters
        public int AmmoInMagazine => ammoInMagazine;
        public int SpareAmmo => spareAmmo;
        public bool IsReloading => isReloading;
    }
}
