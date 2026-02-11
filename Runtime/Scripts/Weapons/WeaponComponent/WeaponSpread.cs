using UnityEngine;
using Mandible.FPSController;

public class WeaponSpread : WeaponComponent
{
    [Header("Spread Settings")]
    public float spreadAngle = 2f;

    [Header("Smoothing")]
    [SerializeField] private float spreadSnapSpeed = 12f;
    [SerializeField] private float spreadRecoverySpeed = 6f;

    private Vector3 targetEuler = Vector3.zero;
    private Vector3 currentEuler = Vector3.zero;

    protected override void Start()
    {
        base.Start();
        weapon.OnWeaponUse.AddListener(OnUse);
    }

    public override void OnUse()
    {
        float randX = Random.Range(-spreadAngle, spreadAngle);
        float randY = Random.Range(-spreadAngle, spreadAngle);

        targetEuler += new Vector3(randX, randY, 0f);
    }

    protected override void Update()
    {
        base.Update();

        currentEuler = Vector3.Lerp(
            currentEuler,
            targetEuler,
            Time.deltaTime * spreadSnapSpeed
        );

        targetEuler = Vector3.Lerp(
            targetEuler,
            Vector3.zero,
            Time.deltaTime * spreadRecoverySpeed
        );
    }

    public override Quaternion GetRotationOffset()
    {
        return Quaternion.Euler(currentEuler);
    }
}
