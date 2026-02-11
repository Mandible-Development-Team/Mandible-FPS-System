using System.Collections;

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

        [Header("Gun Settings")]
        public bool isRaycast = true;
        public bool isInfiniteAmmo;
        public int ammoInMagazine;
        public int magazineSize = 30;
        public int spareAmmo = 90;
        public float fireRate = 10f;
        public float bulletSpeed = 1f;
        public float bulletForce = 1f;
        public float reloadTime = 2f;
        public bool isAutomatic = true;
        public bool xIsForward = false;

        [Header("Sights")]
        [SerializeField] SpriteRenderer sights;
        [SerializeField] float sightsVisibilitySpeed = 10f;
        Color defaultSightsColor;
        Color targetSightsColor;

        #if STATUS_EFFECTS
        [Header("Status Effect")]
        public StatusEffectContribution contribution;
        #endif

        [Header("Particles")]
        public Projectile projectilePrefab;
        public Transform muzzlePoint;
        public ParticleSystem muzzleFlash;
        public ParticleSystem bulletFire;
        public ParticleSystem bulletHit;

        [Header("Audio")]
        public AudioClip shootSFX;
        public AudioClip reloadSFX;
        public AudioClip hitSFX;
        public AudioClip killSFX;
        public LayerMask hitMask;

        //Events
        [HideInInspector] public UnityEvent OnAim = new UnityEvent();
        [HideInInspector] public UnityEvent OnUnAim = new UnityEvent();

        //State
        private bool triggerHeld = false;
        private bool isReloading = false;
        private float nextFireTime = 0f;

        private AudioSource audioSource;
        private AudioSource screenspaceSFX;

        private Coroutine currentCoroutine;
        
        [HideInInspector] public WeaponZoom zoom;

        //Test
        PlayerInputActions inputActions;

        protected override void Awake()
        {
            base.Awake();

            isReloading = false;
            ammoInMagazine = magazineSize;

            gunPosition = transform.localPosition;

            //Sights
            InitializeSights();

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
            AudioSource[] sources = GetComponents<AudioSource>();
            audioSource = sources[0];

            if (sources[1])
                screenspaceSFX = sources[1];

            ownerCamera = owner?.Camera.GetComponent<Camera>();
        }

        public override void LateUpdate()
        {
            UpdatePosition();
            base.LateUpdate();

            if (!isEquipped) return;


            if (triggerHeld) Use();
            if (sights) HandleSights();
            
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

            //Sound
            if (shootSFX != null)
            {
                audioSource.pitch = Random.Range(0.95f, 1.05f);
                audioSource.PlayOneShot(shootSFX, 0.25f);
            }

            // Muzzle & Bullet
            if (muzzleFlash != null)
            {
                muzzleFlash.Play(true);
            }

            // Spread
            Vector3 origin = Vector3.zero;
            Vector3 shootDir = GetSpreadDirection(out origin);

            
            if (bulletFire != null)
            {
                EmitBulletfire(origin, shootDir);
            }

            /*
            Vector3 localDir = transform.InverseTransformDirection(shootDir);
            Vector3 localUp  = transform.InverseTransformDirection(owner.Camera.transform.up);

            Quaternion modelCorrection = xIsForward ? Quaternion.Euler(0f, -90f, 0f) : Quaternion.identity;
            muzzlePoint.localRotation = modelCorrection * Quaternion.LookRotation(localDir, localUp);
            */

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

        [SerializeField] private float mult;
        private void EmitBulletfire(Vector3 origin, Vector3 shootDir)
        {
            ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams();
            
            if(bulletFire.main.simulationSpace != ParticleSystemSimulationSpace.World){
                Debug.LogWarning("Bullet Fire Particle System should be set to World simulation space for correct bullet direction.");
                emitParams.position = origin - muzzlePoint.position;
            }
            else
            {
                emitParams.position = muzzlePoint.position;
            }

            emitParams.velocity = shootDir * bulletSpeed;
            bulletFire.Emit(emitParams, 1);
        }

        private void SpawnBulletEffect(RaycastHit hit, float delay)
        {
            StartCoroutine(SpawnBulletHit(hit, delay));
        }

        private IEnumerator SpawnBulletHit(RaycastHit hit, float delay)
        {
            yield return new WaitForSeconds(delay);

            Quaternion rot = Quaternion.LookRotation(hit.normal);
            ParticleSystem impact = Instantiate(bulletHit, hit.point, rot);
            impact.Play();
            Destroy(impact.gameObject, impact.main.duration + impact.main.startLifetime.constantMax);
        }

        [SerializeField] private float spreadAngle = 6f;
        [SerializeField] private float spreadRadius = 0.05f;

        Vector3 GetSpreadDirection(out Vector3 origin)
        {
            var cam = owner.Camera.transform;

            float randYaw   = Random.Range(-spreadAngle, spreadAngle);
            float randPitch = Random.Range(-spreadAngle, spreadAngle);
            Quaternion spreadRot = Quaternion.Euler(randPitch, randYaw, 0f);

            Vector3 shootDir = (cam.rotation * spreadRot) * Vector3.forward;

            Vector2 circle = Random.insideUnitCircle * spreadRadius;
            origin = muzzlePoint.position +
                    cam.right * circle.x +
                    cam.up * circle.y;

            return shootDir.normalized;
        }

        private void ProcessHit(RaycastHit hit, Vector3 shootDir)
        {
            
            var dmg = hit.collider.GetComponent<IDamageable>();
            if (CanDamage(dmg))
            {
                HitType type = dmg.GetHitType();

                OnHitTarget?.Invoke(type, hit, shootDir);

                dmg.TakeDamage(damage);

                #if STATUS_EFFECTS
                dmg.AddStatusEffectContribution(contribution);
                #endif

                if(dmg.IsDead)
                {
                    OnKillTarget?.Invoke(type, hit, shootDir);
                }
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
            float delay = distance / bulletSpeed;

            if(bulletHit != null)
            {
                SpawnBulletEffect(hit, delay);
            }   
        }

        //Sights
        
        private void HandleSights()
        {
            ReadSightsState();

            sights.color = Color.Lerp(sights.color, targetSightsColor, sightsVisibilitySpeed * Time.deltaTime);
        }

        public void ReadSightsState()
        {
            if (positionState == GunPosition.Aimed)
            {
                targetSightsColor = defaultSightsColor;
            }
            else
            {
                targetSightsColor = Color.clear;
            }        
        }

        public void InitializeSights()
        {
            if (sights == null) return;
            
            defaultSightsColor = sights.color;
            sights.color = Color.clear;
        }

        //Actions

        private IEnumerator Reload()
        {
            isReloading = true;
            if (reloadSFX != null) screenspaceSFX.PlayOneShot(reloadSFX);

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

        //Get rid of these please

        public override void OnTrigger()
        {

        }

        public override void OnTriggerDown()
        {
            Use();
        }

        public override void OnTriggerUp()
        {

        }

        public override void OnAlternateTrigger()
        {
            Aim();
        }

        public override void OnAlternateTriggerDown()
        {
            Aim();
        }

        public override void OnAlternateTriggerUp()
        {
            positionState = GunPosition.Default;
        }

        //End

        public override void Aim()
        {
            positionState = GunPosition.Aimed;
            OnAim?.Invoke();
        }

        public void UnAim()
        {
            positionState = GunPosition.Default;
            OnUnAim?.Invoke();
        }

        [Header("TEST - ProceduralAim Positioning")]
        [SerializeField] Vector3 aimedPosition;
        [SerializeField] Vector3 defaultPosition;
        [SerializeField] float transitionSpeed = 1f;

        [HideInInspector] Vector3 gunPosition = default;

        public void UpdatePosition()
        {
            switch (positionState)
            {
                case GunPosition.Default:
                    gunPosition = defaultPosition;
                    break;

                case GunPosition.Aimed:
                    gunPosition = aimedPosition;
                    break;

                default:
                    gunPosition = defaultPosition;
                    break;
            }
            
            Vector3 offset = Vector3.Lerp(positionOffset, gunPosition, transitionSpeed);
            SetPositionOffset(offset);
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
