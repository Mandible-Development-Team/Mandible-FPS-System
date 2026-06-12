using System.Collections.Generic;
using UnityEngine;

namespace Mandible.FPSController
{
    public class HitscanTrailManager
    {
        private struct ActiveTrail
        {
            public TrailRenderer trail;
            public Vector3 startPos;
            public Vector3 endPos;
            public float startTime;
            public float duration;
        }

        private TrailRenderer trailPrefab;
        private Transform parentContainer;
        
        private List<ActiveTrail> activeTrails;
        private Queue<TrailRenderer> trailPool;

        public HitscanTrailManager(TrailRenderer prefab, int poolCapacity, Transform parent = null)
        {
            this.trailPrefab = prefab;
            this.parentContainer = parent;
            
            activeTrails = new List<ActiveTrail>(poolCapacity);
            trailPool = new Queue<TrailRenderer>(poolCapacity);

            for (int i = 0; i < poolCapacity; i++)
            {
                trailPool.Enqueue(CreateNewTrail());
            }
        }

        public void UpdateTrails()
        {
            if (activeTrails.Count == 0) return;

            float currentTime = Time.time;

            for (int i = activeTrails.Count - 1; i >= 0; i--)
            {
                ActiveTrail data = activeTrails[i];
                float timeElapsed = currentTime - data.startTime;
                float t = timeElapsed / data.duration;

                if (t >= 1f)
                {
                    data.trail.transform.position = data.endPos;
                    ReturnToPool(data.trail);

                    int lastIndex = activeTrails.Count - 1;
                    activeTrails[i] = activeTrails[lastIndex];
                    activeTrails.RemoveAt(lastIndex);
                }
                else
                {
                    data.trail.transform.position = Vector3.Lerp(data.startPos, data.endPos, t);
                }
            }
        }

        public void FireTrail(Vector3 origin, Vector3 hitPoint, float speed)
        {
            TrailRenderer trail = GetFromPool();
            
            trail.transform.position = origin;
            trail.Clear();
            
            float distance = Vector3.Distance(origin, hitPoint);

            float duration = (speed > 0f) ? distance / speed : 0.01f;

            activeTrails.Add(new ActiveTrail
            {
                trail = trail,
                startPos = origin,
                endPos = hitPoint,
                startTime = Time.time,
                duration = duration
            });
        }

        // Internal Pool Logic 
        private TrailRenderer GetFromPool()
        {
            if (trailPool.Count > 0)
            {
                TrailRenderer tr = trailPool.Dequeue();
                tr.gameObject.SetActive(true);
                return tr;
            }

            return CreateNewTrail(true);
        }

        private void ReturnToPool(TrailRenderer trail)
        {
            trail.gameObject.SetActive(false);
            trailPool.Enqueue(trail);
        }

        private TrailRenderer CreateNewTrail(bool active = false)
        {
            TrailRenderer tr = Object.Instantiate(trailPrefab, parentContainer);
            tr.gameObject.SetActive(active);
            return tr;
        }
    }
}