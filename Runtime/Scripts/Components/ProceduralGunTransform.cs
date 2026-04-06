using UnityEngine;

using Mandible.FPSController;
using Mandible.PlayerController;

using System.Buffers;
//using System.Diagnostics;

[DefaultExecutionOrder(-100)]
public class ProceduralGunTransform : MonoBehaviour
{
    public Weapon weapon;
    [Header("References")]
    public Transform parentTransform;
    public AimPivot aimPivot;
    public Transform handle;
    public Transform forwardTransform;
    
    [Header("Base Transforms")]
    [SerializeField] bool updateTransform = true;
    [SerializeField] public Quaternion rotationOffset = Quaternion.identity;
    [SerializeField] public Vector3 positionOffset = Vector3.zero;

    [Header("Auto-Calculation")]
    [SerializeField] bool autoCalculateForward = false;
    [Range(0f, 1f)] [SerializeField] float forwardCalculationWeight = 1f;
    [SerializeField] bool autoCalculateRoll = false;
    [Range(0f, 1f)] [SerializeField] float rollCalibrationWeight = 1f;

    [Header("Advanced")]
    [SerializeField] bool disableProcedural = false;

    private Mandible.FPSController.Player player;
    private FPSProceduralController controller;
    private HumanoidProceduralRig hpr;

    //Base
    private Quaternion baseRot = Quaternion.identity;
    private Vector3 basePos = Vector3.zero;

    //Mods
    [HideInInspector] public Quaternion rotationMod = Quaternion.identity;
    [HideInInspector] public Vector3 positionMod = Vector3.zero;

    //Cache
    private Quaternion initialRotation;
    private Quaternion initialRotationParent;

    private Quaternion initialRotationPivot;
    private Quaternion initialLocalRotationPivot;
    private Quaternion initialRotationPivotParent;

    private Quaternion calculatedForwardOffset;

    //Flags
    private bool hasInitializedPostProcessingCache = false;

    void Awake()
    {
        //References
        weapon = GetComponentInChildren<Weapon>();
        if(!weapon) Debug.LogError("ProceduralGunTransform: No Weapon component found in children.");

        //Transform
        initialRotation = transform.rotation;
        if(parentTransform) initialRotationParent = parentTransform.rotation;

        //Aim Pivot
        if(aimPivot) initialRotationPivot = aimPivot.transform.rotation;
        if(aimPivot) initialLocalRotationPivot = aimPivot.transform.rotation * Quaternion.Inverse(aimPivot.transform.parent.rotation);
        if(aimPivot) initialRotationPivotParent = aimPivot.transform.parent.rotation;
    }

    void Start()
    {
        //Other References
        player = (Mandible.FPSController.Player)weapon?.owner;
        controller = (FPSProceduralController)player?.Controller;
        hpr = controller?.proceduralRig;

        //Post Process
        hpr.onPostProcessCompleted += UpdateNonProcedural;
    }

    void LateUpdate()
    {
        if(disableProcedural) return;

        if(CanUpdate()) UpdateTransform();
        
        PostProcessingPass();
    }

    //Transform
    public void UpdateNonProcedural()
    {
        if(!disableProcedural) return;

        Quaternion targetRot = parentTransform.rotation * weapon.GetForwardRotation() * rotationMod;
        transform.rotation = targetRot;

        Vector3 offsetFromHandle = handle.position - transform.position;
        transform.position = parentTransform.position - offsetFromHandle;

        Debug.DrawRay(parentTransform.position, parentTransform.right, Color.red);
        Debug.DrawRay(parentTransform.position, parentTransform.up, Color.green);
        Debug.DrawRay(parentTransform.position, parentTransform.forward, Color.blue);
    }

    public void UpdateTransform()
    {
        if (!parentTransform) return;
 
        //Rotation
        baseRot = Quaternion.identity;
        baseRot *= aimPivot.transform.rotation;   // aim rotation
        baseRot *=  Quaternion.Inverse(aimPivot.transform.parent.rotation) * parentTransform.rotation; // isolated anim rotation
        baseRot *= weapon.GetForwardRotation(); // relative forward

        // Position
        basePos = Vector3.zero; 
        basePos += positionOffset;
        basePos += positionMod;
        if(aimPivot) transform.position = aimPivot.transform.TransformPoint(basePos); //Require AimPivot
    }

    /*
    public void UpdateTransform_ExplicitQuaternion() //DEPRECATED
    {
        if (!parentTransform) return;
 
        baseRot = Quaternion.identity;
        basePos = Vector3.zero; 

        //Rotation
        if(aimPivot) // Isolate pivot rotation to not double count
        {
            Quaternion localPivot = aimPivot.transform != null ? Quaternion.Inverse(aimPivot.transform.parent.rotation) : Quaternion.identity;
            Quaternion offsetPivot = initialLocalRotationPivot * localPivot;

            baseRot *= aimPivot.transform.rotation * offsetPivot;
        }

        baseRot *= rotationOffset; // Custom offset applied after pivot, used for weapon types with different forwards
        baseRot *= parentTransform.rotation;

        transform.rotation = baseRot;

        // Position
        Vector3 proceduralLocalPos = positionOffset + positionMod;
        Vector3 animLocalPos = parentTransform.position - aimPivot.transform.position;

        basePos = proceduralPosition ? proceduralLocalPos : animLocalPos;

        if(aimPivot)
        {
            transform.position = aimPivot.transform.TransformPoint(basePos);
        }
        else
        {
            transform.position = basePos;
        }
    }
    */

    //Post Processing
    public void InitializePostProcessingCache()
    {
        hasInitializedPostProcessingCache = true;

        //Cache
        CacheForwardOffset();
    }

    public void PostProcessingPass()
    {
        //Post-Process Rotation
        if(!hasInitializedPostProcessingCache) InitializePostProcessingCache();

        if (CanAutoCalculateForward())
        {
            AutoCalculateForward();
        }

        if (CanAutoCalculateForwardProcedural())
        {
            AutoCalculateForwardProcedural();
        }

        if(autoCalculateRoll)
        {
            AutoCalculateRoll();
        }
        
        //Mods (staged after post processing for better behavior)
        baseRot *= rotationMod;

        transform.rotation = baseRot; 
    }

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
