using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

using TMPro;

namespace Mandible.FPSController
{
    [ExecuteAlways]
    public class WeaponUIRenderer : MonoBehaviour
    {
        public WeaponSystem weaponSystem;
        public TextMeshProUGUI ammoText;
        public Image icon;

        private Weapon _currentWeapon;

        void Start()
        {
            if(weaponSystem == null) return;

            _currentWeapon = weaponSystem.CurrentWeapon;
        }

        void Update()
        {
            if(weaponSystem == null) return;
            _currentWeapon = weaponSystem.CurrentWeapon;

            if(_currentWeapon == null) return;
            icon.sprite = _currentWeapon.icon;

            Gun gun = _currentWeapon as Gun;
            if(gun != null && gun.isInfiniteAmmo == false)
            {
                ammoText.text = "" + gun.ammoInMagazine + " / " + gun.magazineSize;
            }
            else if(gun != null && gun.isInfiniteAmmo == true)
            {
                ammoText.text = "" + gun.ammoInMagazine + " / ∞";
            }
            else
            {
                ammoText.text = "";
            }
        }
    }
}
