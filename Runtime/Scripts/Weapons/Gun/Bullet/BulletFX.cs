using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Mandible.FPSController
{
    public class BulletFX : MonoBehaviour
    {
        public ParticleSystem bulletFire;
        public ParticleSystem bulletHit;

        void Start()
        {
            if(bulletFire == null) bulletFire = GetComponent<ParticleSystem>();
            if(bulletHit == null) Debug.LogWarning("BulletFX: No bullet hit effect assigned.");
            
            if(bulletFire.collision.sendCollisionMessages == false) 
                Debug.LogWarning("BulletFX: Particle system must have 'Send Collision Messages' enabled.");

        }

        void Update()
        {
            
        }

        //Collisions
        void OnParticleCollision(GameObject other)
        {
            if(bulletFire == null || bulletHit == null) return;

            List<ParticleCollisionEvent> collisionEvents = new List<ParticleCollisionEvent>();
            int num = bulletFire.GetCollisionEvents(other, collisionEvents);

            for (int i = 0; i < num; i++)
            {
                ParticleCollisionEvent evt = collisionEvents[i];

                ParticleSystem impact = Instantiate(bulletHit, evt.intersection, Quaternion.LookRotation(evt.normal));
                impact.Play();
                Destroy(impact.gameObject, impact.main.duration + impact.main.startLifetime.constantMax);
            }
        }
    }
}
