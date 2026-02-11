using UnityEngine;
using Mandible.PlayerController;
using Mandible.Entities;
using Mandible.Core;

namespace Mandible.FPSController
{
    public class Player : Mandible.Entities.Entity, Mandible.PlayerController.IPlayer
    {
        [Header("Components")]
        [SerializeField] private Mandible.PlayerController.PlayerController controller;
        [SerializeField] private new Mandible.PlayerController.CameraController camera;
        public Mandible.PlayerController.PlayerController Controller => controller;
        public Mandible.PlayerController.CameraController Camera => camera;

        public IInputSystem Input { get; private set; }
        
        public override void Awake()
        {
            base.Awake();

            controller = GetComponent<Mandible.PlayerController.PlayerController>();

            Input = new PlayerInputSystem();
        }

        public override void Update()
        {
            base.Update();
            
            Input?.Update();
        }
    }
}
