using UnityEngine;
using Mandible.Entities;
using Mandible.Core;

namespace Mandible.FPSController
{
    public class FPSThrowCapability : IThrowCapability
    {
        [SerializeField] IAgent agent;
        [SerializeField] Transform throwOrigin;

        public void Initialize(IAgent agent)
        {
            this.agent = agent;
        }

        public void Throw(ThrowActionData data)
        {
            if (data == null || data.projectilePrefab == null) return;
            throwOrigin = (agent as MonoBehaviour).GetComponent<FPSProceduralController>()?.camera.transform;

            Quaternion rot = Quaternion.LookRotation(throwOrigin.forward);
            var instance = Object.Instantiate(data.projectilePrefab, throwOrigin.position + rot * data.spawnOffset, rot);

            if (instance.TryGetComponent<Rigidbody>(out var rb))
            {
                Vector3 dir = agent.GetLookDirection();
                rb.AddForce(dir.normalized * data.force, ForceMode.Impulse);
            }

            // animation triggering should be capability based
            
            FPSProceduralController fpsController = (agent as MonoBehaviour).GetComponent<FPSProceduralController>();
            //int throwTime = fpsController.GetAnimationDuration("Throw", "UpperBody");
            fpsController.Throw(data);
            

            /*
            Animator anim = (agent as MonoBehaviour).GetComponent<Animator>();
            if(anim != null)
            {
                int layerIndex = anim.GetLayerIndex("UpperBody");
                anim.Play("Throw", layerIndex, 0f);
            }
            */
        }
    }
}