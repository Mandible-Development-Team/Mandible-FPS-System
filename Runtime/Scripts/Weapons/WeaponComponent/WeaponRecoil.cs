using UnityEngine;
using Mandible.FPSController;
using Mandible.PlayerController;

public class WeaponRecoil : WeaponComponent
{
    [Header("Recoil Settings")]
    [SerializeField] float recoilStrength = 2f;
    [SerializeField] float recoilSnapSpeed = 12f;
    [SerializeField] float recoilRecoverySpeed = 8f;
    [SerializeField] RecoilData recoilData = new RecoilData(RecoilShape.Linear, RecoilShape.Linear);

    [Header("Advanced")]
    [SerializeField] bool applyToCamera = true;
    [SerializeField] [Range(0f, 1f)] float cameraScale = 1f;
    [SerializeField] Vector3 positionalInfluence = new Vector3(1f, 1f, 1f);
    [SerializeField] Vector3 rotationalInfluence = new Vector3(1f, 1f, 1f);

    //Camera
    private CameraController cameraController;
    private AimPivot aimPivot;

    //Recoil
    private Vector3 targetRecoilRotation;
    private Vector3 currentRecoilRotation;

    private Vector3 targetRecoilPosition;
    private Vector3 currentRecoilPosition;
    private float recoilT = 0f;

    [SerializeField] private bool xIsForward = true;
    protected override void Start()
    {
        base.Start();

        if(owner != null)
        {
            cameraController = owner.Camera.GetComponent<CameraController>();
            aimPivot = (owner as MonoBehaviour).GetComponent<FPSProceduralController>().aimPivot;
        }
    }

    const float POSITIONAL_INFLUENCE_EPSILON = 0.02f;
    const float ROTATIONAL_INFLUENCE_EPSILON = 1f;
    const float VERT_KICK_EPSILON = 0.8f;
    const float ROLL_KICK_EPSILON = 0.8f;
    public override void OnUse()
    {
        //Vert kick
        float vertKickFactor = Random.Range(VERT_KICK_EPSILON, 1f);
        float vertKick = recoilStrength * vertKickFactor;

        //Roll kick
        float rollKickFactor = Random.Range(ROLL_KICK_EPSILON, 1f);
        float rollKick = recoilStrength * (Random.value < 0.5f ? -rollKickFactor : rollKickFactor);

        //Assignment
        float xKickPos = 0f;
        float yKickPos = vertKick * positionalInfluence.y;
        float zKickPos = -recoilStrength * positionalInfluence.z;

        float xKickRot = -vertKick * rotationalInfluence.x;
        float yKickRot = 0f;
        float zKickRot = rollKick * rotationalInfluence.z;

        if(xIsForward)
        {
            xKickPos = 0f;
            yKickPos = vertKick * positionalInfluence.y;
            zKickPos = recoilStrength * positionalInfluence.x;

            xKickRot = rollKick * rotationalInfluence.x;
            yKickRot = 0f;
            zKickRot = vertKick * rotationalInfluence.z;
        }

        Vector3 finalPosRecoil = new Vector3(xKickPos, yKickPos, zKickPos) * POSITIONAL_INFLUENCE_EPSILON;
        Vector3 finalRotRecoil = new Vector3(xKickRot, yKickRot, zKickRot) * ROTATIONAL_INFLUENCE_EPSILON;
    
        AddPositionalRecoil(finalPosRecoil);
        AddRotationalRecoil(finalRotRecoil); 
        AddCameraRecoil();   
    }

    protected override void Update()
    {
        base.Update();

        currentRecoilRotation = Vector3.Lerp(currentRecoilRotation, targetRecoilRotation, Time.fixedDeltaTime * recoilSnapSpeed);
        targetRecoilRotation = Vector3.Lerp(targetRecoilRotation, Vector3.zero, Time.fixedDeltaTime * recoilRecoverySpeed);

        currentRecoilPosition = Vector3.Lerp(currentRecoilPosition, targetRecoilPosition, Time.fixedDeltaTime * recoilSnapSpeed);
        targetRecoilPosition = Vector3.Lerp(targetRecoilPosition, Vector3.zero, Time.fixedDeltaTime * recoilRecoverySpeed);
    }

    //API

    public void AddRotationalRecoil(Vector3 recoil)
    {
        targetRecoilRotation += recoil;
    }

    public void AddPositionalRecoil(Vector3 recoil)
    {
        targetRecoilPosition += recoil;
    }

    public void AddCameraRecoil()
    {
        if(applyToCamera)
        {
            recoilT += recoilStrength * cameraScale * Time.fixedDeltaTime;
            Vector2 recoil = GetRecoil(recoilT);
            aimPivot.AddRecoil(recoil);

            Vector3 recoilVector = new Vector3(Random.Range(-1, 1), Random.Range(-1, 1), 0f) * recoilStrength * Time.fixedDeltaTime;
            cameraController?.AddShakeImpulse(recoilVector, cameraScale);
        }
    }

    public Vector2 GetRecoil(float t)
    {
        float xRecoil = GetRecoilShape(recoilData.xShape, t);
        float yRecoil = GetRecoilShape(recoilData.yShape, t);

        return new Vector2(xRecoil, yRecoil);
    }

    public float GetRecoilShape(RecoilShape recoilShape, float t)
    {
        if(recoilShape == RecoilShape.Linear)
        {
            return recoilStrength;
        }
        else if(recoilShape == RecoilShape.Sine)
        {
            return recoilStrength * Mathf.Sin(t * Mathf.PI * 0.5f);
        }

        return 0f;
    }

    //Helpers

    public override Quaternion GetRotationOffset()
    {
        return Quaternion.Euler(currentRecoilRotation);
    }

    public override Vector3 GetPositionOffset()
    {
        return currentRecoilPosition;
    }
}

[System.Serializable]
public struct RecoilData
{
    public RecoilShape xShape;
    public RecoilShape yShape;

    public RecoilData(RecoilShape xShape, RecoilShape yShape)
    {
        this.xShape = xShape;
        this.yShape = yShape;
    }
}

public enum RecoilShape
{
    Linear,
    Sine
}


