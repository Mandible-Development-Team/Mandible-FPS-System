using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Mandible.FPSController
{
    [System.Serializable]
    public class AimSenseData
    {
        public bool autoCalculateForward = false;
        public float forwardCalculationWeight = 0f;
        public bool autoCalculateRoll = false;
        public float rollCalculationWeight = 0f;
        
        //Experimental
        public bool autoCalculateForwardProcedural = true;
        public float calculateForwardStability = 0f;
        public float calculateForwardSpeed = 0f;
    }
}