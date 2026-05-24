using UnityEngine;
using System;

using Mandible.PlayerController;

#if UNITY_EDITOR
using UnityEditor;
#endif

using Controller = Mandible.PlayerController.PlayerController;

namespace Mandible.FPSController
{
    [DefaultExecutionOrder(0)]
    public class HumanoidProceduralRig : MonoBehaviour
    {
        [SerializeField] Controller controller;

        [Header("General")]
        [Range(0f, 1f)] public float weight = 1f;
        public bool disableIK = false;

        [Header("Spine")]
        [SerializeField] Transform spine;
        public Vector2 pitchLimits = new Vector2(-90f, 90f);

        [Header("Left Arm")]
        [SerializeField] Transform leftUpperArm;
        [SerializeField] Transform leftLowerArm;
        [SerializeField] Transform leftHand;
        [Space(8)]
        [SerializeField] Transform leftElbowHint;
        [SerializeField] Transform leftPalmHint;
        [Space(8)]
        [SerializeField] Transform leftArmTarget;
        [Range(0f, 1f)] [SerializeField] float leftArmWeight = 1f;
        [Range(0f, 1f)] [SerializeField] float leftGripWeight = 1f;

        [Header("Right Arm")]
        [SerializeField] Transform rightUpperArm;
        [SerializeField] Transform rightLowerArm;
        [SerializeField] Transform rightHand;
        [Space(8)]
        [SerializeField] Transform rightElbowHint;
        [SerializeField] Transform rightPalmHint;
        [Space(8)]
        [SerializeField] Transform rightArmTarget;
        [Range(0f, 1f)] [SerializeField] float rightArmWeight = 1f;
        [Range(0f, 1f)] [SerializeField] float rightGripWeight = 1f;

        //[Header("Advanced")]
        //[SerializeField] float armWeightInterpolationSpeed = 0f;

        //Events
        [HideInInspector] public Action onPostProcessCompleted;

        //Cache
        TwoBoneIKContext leftArmContext, rightArmContext;
        float lenA, lenB;
        Vector3 palmOffsetHandLocal;

        //Previous Values
        float previousWeight;
        Transform previousLeftArmTarget, previousRightArmTarget;
        float previousLeftGripWeight, previousRightGripWeight;

        void Awake()
        {
            Initialize();
            CreateContext();
        }

        void Start()
        {

        }

        void LateUpdate()
        {
            if (!IsValid()) return;

            //Spine
            ApplySpineConstraint();

            //IK
            if (!disableIK){
                //Arms
                SolveTwoBoneIK(leftArmContext);
                SolveTwoBoneIK(rightArmContext);
            }

            //Post Process
            onPostProcessCompleted.Invoke();
        }

        void Initialize()
        {
            previousLeftGripWeight = leftGripWeight;
            previousRightGripWeight = rightGripWeight;
        }

        void OnValidate()
        {
            if(!Application.isPlaying) return;
            CreateContext(suppressWarnings: true);
        }

        // Constraints
        void ApplySpineConstraint()
        {
            Transform parent = spine.parent;

            Quaternion cameraWorld = controller.camera.transform.rotation;

            spine.localRotation = Quaternion.Inverse(parent.rotation) * cameraWorld;
        }

        /*
        void ApplySpineConstraint()
        {
            spine.rotation = controller.camera.transform.rotation;
        }
        */

        public void SolveTwoBoneIK(TwoBoneIKContext context)
        {
            // Set References
            Transform upper = context.upper;
            Transform lower = context.lower;
            Transform end = context.end;
            Transform target = context.target;

            if (!upper || !lower || !end || !target)
                return;

            // Solver
            float a = context.upperLength;
            float b = context.lowerLength;
            float w = Mathf.Clamp01(context.armWeight) * Mathf.Clamp01(context.globalWeight);

            Vector3 shoulderPos = upper.position;
            Vector3 wristTargetPos = target.position;

            if (context.palmHint != null)
                wristTargetPos -= target.rotation * context.palmOffset;

            Vector3 toWrist = wristTargetPos - shoulderPos;
            float dist = toWrist.magnitude;
            if (dist < 1e-6f)
                return;

            dist = Mathf.Clamp(dist, Mathf.Abs(a - b) + 1e-5f, a + b - 1e-5f);
            Vector3 dir = toWrist / dist;

            float cosAngle = Mathf.Clamp((a * a + dist * dist - b * b) / (2f * a * dist), -1f, 1f);
            float elbowAlong = a * cosAngle;
            float elbowHeight = Mathf.Sqrt(Mathf.Max(0f, a * a - elbowAlong * elbowAlong));

            //Handle Pole
            Vector3 poleDir = context.elbowHint
                ? (context.elbowHint.position - shoulderPos)
                : upper.up;

            poleDir -= Vector3.Dot(poleDir, dir) * dir;

            if (poleDir.sqrMagnitude < 1e-6f)
                poleDir = Vector3.Cross(dir, upper.right);

            poleDir.Normalize();

            Vector3 elbowPos =
                shoulderPos +
                dir * elbowAlong +
                poleDir * elbowHeight;

            //Rotate Joints
            Quaternion upperRot =
                Quaternion.FromToRotation(
                    lower.position - upper.position,
                    elbowPos - shoulderPos
                ) * upper.rotation;

            Quaternion lowerRot =
                Quaternion.FromToRotation(
                    end.position - lower.position,
                    wristTargetPos - elbowPos
                ) * lower.rotation;

            upper.rotation = Quaternion.Slerp(upper.rotation, upperRot, w);
            lower.rotation = Quaternion.Slerp(lower.rotation, lowerRot, w);

            //Solve Grip
            if (context.palmHint != null)
            {
                SolveGrip(context);
            }
        }

        void SolveGrip(TwoBoneIKContext context)
        {
            float w = Mathf.Clamp01(context.armWeight) * Mathf.Clamp01(context.globalWeight);
            float gripW = Mathf.Clamp01(context.gripWeight);

            Quaternion handRot = SolveHandRotation(context);
            Quaternion gripRot = SolveGripRotation(context);

            context.end.rotation = Quaternion.Slerp(context.end.rotation, Quaternion.Slerp(handRot, gripRot, gripW), w);
        }

        Quaternion SolveHandRotation(TwoBoneIKContext context)
        {
            Transform end = context.end;
            Transform lower = context.lower;
            Transform upper = context.upper;
            Transform target = context.target;
            Transform palm = context.palmHint;

            if (!end || !lower || !upper || !target || !palm)
                return end.rotation;

            Vector3 forearmDir =
                (end.position - lower.position).normalized;

            Vector3 currentDir =
                (palm.position - end.position).normalized;

            Vector3 desiredDir =
                (target.position - end.position).normalized;

            Vector3 outward =
                Vector3.Cross(forearmDir, upper.forward).normalized;

            desiredDir = (desiredDir + outward * 0.25f).normalized;

            Quaternion delta =
                Quaternion.FromToRotation(currentDir, desiredDir);

            return delta * end.rotation;
        }
        
        Quaternion SolveGripRotation(TwoBoneIKContext context)
        {
            return context.target.rotation;
        }

        // Setters

        public void SetWeight(float newWeight)
        {
            weight = newWeight;
            CreateContext();
        }

        public void SetTargets(Transform leftTarget, Transform rightTarget)
        {
            leftArmTarget = leftTarget;
            rightArmTarget = rightTarget;

            CreateContext();
        }

        public void SetLeftArmTarget(Transform newTarget)
        {
            leftArmTarget = newTarget;
            CreateContext();
        }

        public void SetRightArmTarget(Transform newTarget)
        {
            rightArmTarget = newTarget;
            CreateContext();
        }

        public void SetArmWeight(float leftWeight, float rightWeight)
        {
            this.leftArmWeight = leftWeight;
            this.rightArmWeight = rightWeight;
            CreateContext();
        }

        public void SetGripWeight(float leftGripWeight, float rightGripWeight)
        {
            this.leftGripWeight = leftGripWeight;
            this.rightGripWeight = rightGripWeight;
            CreateContext();
        }

        public void SetLeftArmWeight(float armWeight)
        {
            leftArmWeight = armWeight;
            CreateContext();
        }

        public void SetRightArmWeight(float armWeight)
        {
            rightArmWeight = armWeight;
            CreateContext();
        }

        public void SetLeftGripWeight(float gripWeight)
        {
            leftGripWeight = gripWeight;
            CreateContext();
        }

        public void SetRightGripWeight(float gripWeight)
        {
            rightGripWeight = gripWeight;
            CreateContext();
        }

        // Getters

        public float GetLeftArmWeight()
        {
            return leftArmWeight;
        }

        public float GetRightArmWeight()
        {
            return rightArmWeight;
        }

        // Helpers
        float NormalizeAngle(float angle)
        {
            if (angle > 180f) angle -= 360f;
            return angle;
        }

        void CreateContext(bool suppressWarnings = false)
        {
            //Left Arm
            if (!leftUpperArm || !leftLowerArm || !leftHand){
                if (!suppressWarnings) Debug.LogWarning("HumanoidProceduralRig: Missing left arm references.");
                return;
            }
            else
            {
                leftArmContext = new TwoBoneIKContext(
                    leftUpperArm,
                    leftLowerArm,
                    leftHand,
                    leftArmTarget,
                    weight,
                    leftArmWeight
                );

                if(leftElbowHint != null) leftArmContext.elbowHint = leftElbowHint;
                if(leftPalmHint != null) leftArmContext.palmHint = leftPalmHint;
                if(leftGripWeight != 0f) leftArmContext.gripWeight = leftGripWeight;

                leftArmContext.CalculatePalmOffset();
                leftArmContext.CalculateBendPlane();

                leftArmContext.SetGripAxis("Left");
            }

            //Right Arm
            if(!rightUpperArm || !rightLowerArm || !rightHand){
                Debug.LogWarning("HumanoidProceduralRig: Missing right arm references.");
                return;
            }
            else
            {
                rightArmContext = new TwoBoneIKContext(
                    rightUpperArm,
                    rightLowerArm,
                    rightHand,
                    rightArmTarget,
                    weight,
                    rightArmWeight
                );     

                if(rightElbowHint != null) rightArmContext.elbowHint = rightElbowHint;
                if(rightPalmHint != null) rightArmContext.palmHint = rightPalmHint;  
                if(rightGripWeight != 0f) rightArmContext.gripWeight = rightGripWeight;
                  
                rightArmContext.CalculatePalmOffset();
                rightArmContext.CalculateBendPlane();

                rightArmContext.SetGripAxis("Right");
            }
        }

        // Validation

        bool IsValid()
        {
            if (!spine) return false;
            return true;
        }

    }

    public struct TwoBoneIKContext
    {
        public Transform upper;
        public Transform lower;
        public Transform end;

        public Transform target;
        public float upperLength;
        public float lowerLength;

        public float globalWeight;
        public float armWeight;
        public float gripWeight;

        public Transform elbowHint;
        public Transform palmHint;

        // Advanced
        public Vector3 palmOffset;
        public Vector3 gripAxis;
        public Vector3 bendNormalLocal;
        
        public TwoBoneIKContext(
            Transform upper,
            Transform lower,
            Transform end,
            Transform target,
            float globalWeight,
            float armWeight)
        {
            this.upper = upper;
            this.lower = lower;
            this.end = end;
            this.target = target;

            this.upperLength = Vector3.Distance(upper.position, lower.position);
            this.lowerLength = Vector3.Distance(lower.position, end.position);

            this.globalWeight = globalWeight;
            this.armWeight = armWeight;

            this.elbowHint = null;
            this.palmHint = null;

            //Grip
            this.gripWeight = 0f;
            this.gripAxis = Vector3.forward;

            //Advanced
            this.palmOffset = Vector3.zero;
            this.bendNormalLocal = Vector3.up;
        }

        //Helpers

        public void CalculatePalmOffset()
        {
            if (end != null && palmHint != null)
                palmOffset = end.InverseTransformPoint(palmHint.position);
            else
                palmOffset = Vector3.zero;
        }

        public void CalculateBendPlane()
        {
            if (upper == null || lower == null || end == null)
            {
                bendNormalLocal = Vector3.up;
                return;
            }

            Vector3 upperDir = (lower.position - upper.position).normalized;
            Vector3 lowerDir = (end.position - lower.position).normalized;

            Vector3 normalWorld = Vector3.Cross(upperDir, lowerDir);

            if (normalWorld.sqrMagnitude < 1e-6f)
                normalWorld = Vector3.up;

            bendNormalLocal =
                upper.InverseTransformDirection(normalWorld.normalized);
        }

        //Setters

        public void SetGripAxis(string axis = "Left")
        {
            if(axis.ToLower() == "left")
                gripAxis = Vector3.left;
            else if(axis.ToLower() == "right")
                gripAxis = Vector3.right;
            else if (axis.ToLower() == "up")
                gripAxis = Vector3.up;
            else if (axis.ToLower() == "down")
                gripAxis = Vector3.down;
            else
            {
                Debug.LogError("TwoBoneIKContext: Invalid axis for SetGripAxis. Use 'Left' or 'Right'.");
            }
        }
    }

}
