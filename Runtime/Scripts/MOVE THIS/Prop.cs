using UnityEngine;
using System.Collections;
using System.Collections.Generic;

using Mandible.FPSController;
using Mandible.Entities;
using Mandible.Entities.StatusEffects;

namespace Mandible.FPSController
{
    public class Prop : MonoBehaviour, IDamageable
    {
        [SerializeField] Rigidbody rigidBody;
        [SerializeField] MeshFilter meshFilter;
        [SerializeField] MeshRenderer meshRenderer;

        [Header("Settings")]
        [SerializeField] protected float health = 100f;
        [SerializeField] protected float currentHealth = 0;

        [Header("Procedural Shattering Settings (Experimental)")]
        [SerializeField] private int shardCount = 3;
        [SerializeField] private float shardForce = 1.5f;


        public void Awake()
        {
            if(rigidBody == null) rigidBody = GetComponentInChildren<Rigidbody>();

            if(meshFilter == null) meshFilter = GetComponentInChildren<MeshFilter>();
            if(meshRenderer == null) meshRenderer = GetComponentInChildren<MeshRenderer>();
        }

        public void Start()
        {
            currentHealth = health;
        }

        public virtual void TakeDamage(float amount, HitData data = default)
        {
            currentHealth -= amount;
            Debug.Log($"{gameObject.name} took {amount} damage.");

            if (currentHealth <= 0f){
                currentHealth = 0f;
                SplitMesh();
            }
        }

        //Extension Conduct

        public void AddStatusEffectContribution(StatusEffectContribution contribution) {}

        //Getters / Setters

        public virtual HitType GetHitType()
        {
            return HitType.Normal;
        }

        public float GetHealthPercentage()
        {
            return currentHealth / health;
        }

        public virtual bool IsDead
        {
            get { return currentHealth <= 0f; }
        }

        //Mesh Computation

        public void SplitMesh()
        {
            if (meshFilter == null || meshRenderer == null)
            {
                Debug.LogWarning("Prop missing MeshFilter or MeshRenderer.");
                return;
            }

            Mesh originalMesh = meshFilter.mesh;
            Material originalMat = meshRenderer.material;

            Vector3[] verts = originalMesh.vertices;
            Vector3[] normals = originalMesh.normals;
            Vector2[] uvs = originalMesh.uv;
            int[] tris = originalMesh.triangles;

            // Compute shard seeds (random points in bounding box)
            Bounds bounds = originalMesh.bounds;
            Vector3[] seeds = new Vector3[shardCount];
            for (int i = 0; i < shardCount; i++)
            {
                seeds[i] = new Vector3(
                    Random.Range(bounds.min.x, bounds.max.x),
                    Random.Range(bounds.min.y, bounds.max.y),
                    Random.Range(bounds.min.z, bounds.max.z)
                );
            }

            // Prepare triangle lists for each shard
            List<int>[] shardTris = new List<int>[shardCount];
            for (int i = 0; i < shardCount; i++) shardTris[i] = new List<int>();

            // Assign each triangle to the closest shard seed (centroid distance)
            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 triCenter = (verts[tris[i]] + verts[tris[i + 1]] + verts[tris[i + 2]]) / 3f;

                int closestShard = 0;
                float closestDist = Vector3.Distance(triCenter, seeds[0]);

                for (int s = 1; s < shardCount; s++)
                {
                    float dist = Vector3.Distance(triCenter, seeds[s]);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closestShard = s;
                    }
                }

                shardTris[closestShard].Add(tris[i]);
                shardTris[closestShard].Add(tris[i + 1]);
                shardTris[closestShard].Add(tris[i + 2]);
            }

            // Spawn shard objects
            for (int x = 0; x < shardCount; x++)
            {
                if (shardTris[x].Count == 0) continue;
                SpawnShard(x, verts, normals, uvs, shardTris[x], originalMat);
            }

            // Hide the original
            gameObject.SetActive(false);
        }

        public void SpawnShard(int index, Vector3[] verts, Vector3[] normals, Vector2[] uvs, List<int> shardTris, Material originalMat)
        {
            GameObject shard = new GameObject($"{name}_Shard_{index}");

            //Set transform
            shard.transform.position = transform.position;
            shard.transform.rotation = transform.rotation;
            shard.transform.localScale = transform.localScale;

            //Compute mesh
            Mesh shardMesh = new Mesh();
            shardMesh.vertices = verts;
            shardMesh.normals = normals;
            shardMesh.uv = uvs;
            shardMesh.triangles = shardTris.ToArray();
            shardMesh.RecalculateBounds();
            shardMesh.RecalculateNormals();

            //Set up mesh filter
            MeshFilter shardMeshFilter = shard.AddComponent<MeshFilter>();
            shardMeshFilter.mesh = shardMesh;

            //Set up material
            MeshRenderer shardMR = shard.AddComponent<MeshRenderer>();
            shardMR.material = originalMat;

            //Add Rigidbody
            Rigidbody rb = shard.AddComponent<Rigidbody>();
            rb.mass = rb ? rb.mass : 1f;
            rb.AddForce(Random.onUnitSphere * shardForce, ForceMode.Impulse);

            //Add Collider
            BoxCollider col = shard.AddComponent<BoxCollider>();
        }
    }
}
