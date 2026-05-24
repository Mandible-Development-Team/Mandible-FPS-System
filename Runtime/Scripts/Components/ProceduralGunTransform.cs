using UnityEngine;

using Mandible.FPSController;
using Mandible.PlayerController;

using System.Buffers;
//using System.Diagnostics;

[DefaultExecutionOrder(-100)]
public class ProceduralGunTransform : ProceduralWeaponTransform
{
    [Header("Procedural Positions")]
    [SerializeField] Vector3 aimedPosition;
    [SerializeField] Vector3 loweredPosition;

    [Header("Auto-Calculation")]
    [SerializeField] bool autoCalculateForward = false;
    [Range(0f, 1f)] [SerializeField] float forwardCalculationWeight = 1f;
    [SerializeField] bool autoCalculateRoll = false;
    [Range(0f, 1f)] [SerializeField] float rollCalibrationWeight = 1f;

    private Quaternion calculatedForwardOffset;

    //Post Processing
    public override void PostProcessingPass()
    {
        //Post-Process Rotation
        if(!hasInitializedPostProcessingCache) InitializePostProcessingCache();
        
        //Rotational Sway / Lag
        baseRot *= GetRotationalSway();
        
        //Auto-Calculation
        if(CanAutoCalculateForward()) AutoCalculateForward();
        if(CanAutoCalculateForwardProcedural()) AutoCalculateForwardProcedural();

        if(autoCalculateRoll) AutoCalculateRoll();

        //Modifiers (staged after post processing for better behavior)
        rotationMod = Quaternion.identity;
        positionMod = Vector3.zero;
        foreach(var modifier in modifiers)
        {
            rotationMod *= modifier.GetRotationOffset();
            positionMod += modifier.GetPositionOffset();
        }
        baseRot *= rotationMod;
        basePos += positionMod;

        //Apply
        transform.rotation = baseRot; 
        transform.position = aimPivot.transform.TransformPoint(basePos);
    }

    //Procedural Positioning
    public override System.Enum ReadWeaponPositionState()
    {
        if(weapon is Gun gun) return gun.positionState;
        return default;
    }

    public override Vector3 PositionFromState(System.Enum state)
    {
        if(state is Gun.GunPosition gunPosition)
        {
            switch (gunPosition)
            {
                case Gun.GunPosition.Default: return defaultPosition;
                case Gun.GunPosition.Aimed: return aimedPosition;
            }
        }

        return defaultPosition;
    }

    //Post Processing
    public void InitializePostProcessingCache()
    {
        hasInitializedPostProcessingCache = true;

        //Cache
        CacheForwardOffset();
    }
    
    //Experimental
    public void CacheForwardOffset()
    {
        calculatedForwardOffset = Quaternion.identity;

        if(forwardTransform == null) return;

        Quaternion baseRot = transform.rotation;

        Vector3 gunForward = baseRot * Vector3.forward;
        Vector3 targetForward = forwardTransform.forward;

        Quaternion offsetWorld = Quaternion.FromToRotation(gunForward, targetForward);

        calculatedForwardOffset = Quaternion.Inverse(baseRot) * offsetWorld * baseRot;
    }

    public void AutoCalculateForward()
    {
        baseRot *= Quaternion.Slerp(Quaternion.identity, calculatedForwardOffset, forwardCalculationWeight);
    }

    [Header("Experimental")]
    [SerializeField] bool autoCalculateForwardProcedural = false;
    [Range(0f, 1f)] [SerializeField] float calculateForwardStability = 1f;
    [SerializeField] float calculateForwardSpeed = 1f;

    Quaternion finalDelta = Quaternion.identity;
    public void AutoCalculateForwardProcedural()
    {
        Vector3 forward = forwardTransform != null ? forwardTransform.forward : aimPivot.transform.forward;
        Quaternion worldDelta = Quaternion.FromToRotation(baseRot * Vector3.forward, forward);
        Quaternion localDelta = Quaternion.Inverse(baseRot) * worldDelta * baseRot;

        Quaternion weightedLocalDelta = Quaternion.Slerp(Quaternion.identity, localDelta, calculateForwardStability);
        finalDelta = calculateForwardSpeed > 0 ? Quaternion.Slerp(finalDelta, weightedLocalDelta, Time.deltaTime * calculateForwardSpeed) : weightedLocalDelta;

        baseRot *= finalDelta;
    }

    public void AutoCalculateRoll()
    {
        Transform forwardReference;
        if (forwardTransform != null)
        {
            forwardReference = forwardTransform;
        }
        else
        {
            Debug.LogError("ProceduralChildTransform: Cannot AutoCalculateRoll. Forward transform nor AimPivot are assigned.");
            return;
        }

        Vector3 gunForward = baseRot * Vector3.forward;
        Vector3 gunUp = baseRot * Vector3.up;
        Vector3 targetUp = forwardReference.up;

        Quaternion rollCorrected = Quaternion.LookRotation(gunForward, targetUp);
        
        Quaternion deltaRoll = Quaternion.Inverse(baseRot) * rollCorrected;
        Quaternion rollCorrection = Quaternion.Slerp(Quaternion.identity, deltaRoll, rollCalibrationWeight);
        
        baseRot *= rollCorrection;

        Debug.DrawRay(transform.position, gunForward * 2f, Color.green);
        Debug.DrawRay(transform.position, gunUp * 2f, Color.blue);
        Debug.DrawRay(transform.position, targetUp* 2f, Color.red);
    }

    //Aim Sense
    public void HandleAimSenseData(AimSenseData data)
    {
        //Settings
        autoCalculateForward = data.autoCalculateForward;
        forwardCalculationWeight = data.forwardCalculationWeight;
        autoCalculateRoll = data.autoCalculateRoll;
        rollCalibrationWeight = data.rollCalculationWeight;

        //Experimental
        autoCalculateForwardProcedural = data.autoCalculateForwardProcedural;
        calculateForwardStability = data.calculateForwardStability;
        calculateForwardSpeed = data.calculateForwardSpeed;
    }

    //Helpers
    public bool CanUpdate()
    {
        //Flag
        if (!updateTransform)
        {
            return false;
        }

        //References
        if(!parentTransform)
        {
            Debug.LogWarning("ProceduralChildTransform: Cannot update transform. Update requires Parent Transform.");
            return false;
        }
        if(!aimPivot)
        {
            Debug.LogWarning("ProceduralChildTransform: Cannot update transform. Update requires Aim Pivot.");
            return false;
        }
        if(!forwardTransform)
        {
            Debug.LogWarning("ProceduralChildTransform: Cannot update transform. Update requires Forward Transform (for auto-calculation cache).");
            return false;
        }

        return true;
    }
    
    public bool CanAutoCalculateForward()
    {
        return autoCalculateForward && !autoCalculateForwardProcedural;
    }

    public bool CanAutoCalculateForwardProcedural()
    {
        return autoCalculateForward && autoCalculateForwardProcedural;
    }

}
