using UnityEngine;
using Mandible.PlayerController;
using Mandible.FPSController;

namespace Mandible.FPSController
{
    public abstract class ProceduralWeaponModifier : MonoBehaviour
    {
        public Weapon weapon;
        public IPlayer owner;

        public void Initialize(Weapon weapon = null, IPlayer owner = null)
        {
            this.weapon = weapon;
            this.owner = owner;
        }

        protected virtual void OnEnable()
        {
            Weapon weapon = GetComponent<Weapon>();
            IPlayer owner = weapon?.owner;
            Initialize(weapon, owner);

            this.weapon?.OnWeaponUse.AddListener(OnUse);
        }

        protected virtual void OnDisable()
        {
            this.weapon?.OnWeaponUse.RemoveListener(OnUse);
        }

        protected virtual void Start() { }

        protected virtual void Update() { }

        public virtual void Handle() { }
        public virtual void OnUse() { }
        public virtual void Reset() { }

        public virtual Quaternion GetRotationOffset()
        {
            return Quaternion.identity;
        }

        public virtual Vector3 GetPositionOffset()
        {
            return Vector3.zero;
        }
    }
}
