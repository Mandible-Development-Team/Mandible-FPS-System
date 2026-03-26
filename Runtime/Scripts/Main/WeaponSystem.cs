using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Mandible.FPSController;
using Mandible.Entities;
using Mandible.PlayerController;

namespace Mandible.FPSController
{
    public class WeaponSystem : MonoBehaviour
    {
        [SerializeField] GameObject ownerObject;
        [SerializeField] FPSProceduralController proceduralController;
        [SerializeField] Transform weaponHolder;

        [Header("General")]
        [SerializeField] List<Weapon> weapons = new List<Weapon>();
        [SerializeField] Weapon currentWeapon;

        //Dependencies
        IPlayer owner;
        HumanoidProceduralRig proceduralRig;

        //Events
        [Header("Events")]
        [HideInInspector] public UnityEvent <HitType, RaycastHit, Vector3> onHitTarget;
        [HideInInspector] public UnityEvent <HitType, RaycastHit, Vector3> onKillTarget;

        //Test
        PlayerInputActions inputActions;

        void Awake()
        {
            if(!Validate()) return;

            Initialize();

            SetInputListeners();
        }

        void Start()
        {
            if(!Validate(false)) return;

            weapons = weaponHolder?.GetComponentsInChildren<Weapon>(true).ToList() ?? new List<Weapon>();

            //Initialize Weapons
            foreach(Weapon weapon in weapons)
            {
                InitializeWeapon(weapon);
            }
            
            //Equip first weapon by default
            if(weapons.Count > 0) 
            {
                EquipWeapon(weapons[0]);
            }
            else
            {
                Debug.LogWarning("WeaponSystem: No weapons found in Weapon Holder. Please assign weapons to the Weapon Holder.");
            }
        }

        void Update()
        {
            
        }

        void Initialize()
        {
            owner = ownerObject.GetComponent<IPlayer>();
            
            if(owner == null)
            {
                Debug.LogError("WeaponSystem: Owner Object does not implement IPlayer interface.");
            }

            proceduralRig = proceduralController.GetProceduralRig();

        }

        //API

        void SwitchWeapon(Weapon newWeapon)
        {
            if(weapons.Count < 2) return;

            UnequipWeapon();
            EquipWeapon(newWeapon);
        }

        void NextWeapon()
        {
            Weapon nextWeapon = weapons[(weapons.IndexOf(currentWeapon) + 1) % weapons.Count];
            SwitchWeapon(nextWeapon);
        }

        void PreviousWeapon()
        {
            Weapon previousWeapon = weapons[(weapons.IndexOf(currentWeapon) - 1 + weapons.Count) % weapons.Count];
            SwitchWeapon(previousWeapon);
        }

        void SelectWeapon(int index)
        {
            if(index < 0 || index >= weapons.Count) return;

            UnequipWeapon();
            EquipWeapon(weapons[index]);
        }

        void EquipWeapon(Weapon weapon)
        {
            if(weapon != null)
            {
                currentWeapon = weapon;
                currentWeapon.gameObject.SetActive(true);

                proceduralRig?.SetTargets(currentWeapon.handle, currentWeapon.foreHandle);

                StartListening(currentWeapon); //Events
            }
        }

        void UnequipWeapon()
        {
            if(currentWeapon != null) 
            {
                StopListening(currentWeapon); //Events
                currentWeapon.gameObject.SetActive(false);
                currentWeapon = null;
            }
        }

        void AddWeapon(Weapon weapon, bool equipImmediately = false)
        {
            if(weapon == null) return;

            //Spawn
            Weapon spawnedWeapon = Instantiate(weapon, weaponHolder);
            spawnedWeapon.gameObject.SetActive(false);
            Destroy(weapon.gameObject);

            //Initialize
            weapons.Add(spawnedWeapon);
            InitializeWeapon(spawnedWeapon);

            //Equip Weapon Immediately
            if(equipImmediately)
            {
                EquipWeapon(spawnedWeapon);
            }
        }

        void InitializeWeapon(Weapon weapon)
        {
            weapon.Initialize(ownerObject);
        }

        //Aim Sense (Experimental)

        public void HandleAimSense(AimSenseData data)
        {
            ProceduralGunTransform proceduralGun = currentWeapon.GetComponent<ProceduralGunTransform>();
            if(proceduralGun != null)
                proceduralGun.HandleAimSenseData(data);
            else
                Debug.LogWarning("WeaponSystem: Current weapon does not have a ProceduralGunTransform component for Aim Sense.");
        }

        //Events

        void OnCurrentWeaponHitTarget(HitType hitType, RaycastHit hitInfo, Vector3 hitPoint)
        {
            onHitTarget?.Invoke(hitType, hitInfo, hitPoint);
        }

        void OnCurrentWeaponKillTarget(HitType hitType, RaycastHit hitInfo, Vector3 hitPoint)
        {
            onKillTarget?.Invoke(hitType, hitInfo, hitPoint);
        }

        //Input Events

        void SetInputListeners()
        {
            inputActions = new PlayerInputActions();
            inputActions.Enable();

            //Weapons
            inputActions.Weapons.SwitchWeapon.performed += ctx =>
            {
                Vector2 scroll = ctx.ReadValue<Vector2>();
                if(scroll.y > 0f) NextWeapon();
                else if(scroll.y < 0f) PreviousWeapon();
            };
            inputActions.Weapons.SelectWeapon.performed += ctx =>
            {
                var key = ctx.control.name;
                int weaponIndex = int.Parse(key) - 1;
                SelectWeapon(weaponIndex);
            };
        }

        //Generic Event Helpers

        void StartListening(Weapon weapon)
        {
            weapon.OnHitTarget.AddListener(OnCurrentWeaponHitTarget);
            weapon.OnKillTarget.AddListener(OnCurrentWeaponKillTarget);
        }

        void StopListening(Weapon weapon)
        {
            weapon.OnHitTarget.RemoveListener(OnCurrentWeaponHitTarget);
            weapon.OnKillTarget.RemoveListener(OnCurrentWeaponKillTarget);
        }

        //Validation

        bool Validate(bool logErrors = true)
        {
            bool valid = true;

            if(weaponHolder == null)
            {
                if(logErrors) Debug.LogError("WeaponSystem: Weapon Holder is not assigned.");
                valid = false;
            }

            if(proceduralController == null)
            {
                if(logErrors) Debug.LogError("WeaponSystem: Procedural Controller is not assigned.");
                valid = false;
            }

            if(ownerObject == null)
            {
                if(logErrors) Debug.LogError("WeaponSystem: Owner Object is not assigned.");
                valid = false;
            }

            return valid;
        }

        //Helpers

        public bool IsAiming()
        {
            Gun gun = currentWeapon as Gun;
            if(gun == null) return false;

            return gun.positionState == Gun.GunPosition.Aimed;
        }
    }
}
