using UnityEngine;
using ThunderRoad;
using System.Collections;
using HarmonyLib;

namespace Wings
{
    public class WingsData : MonoBehaviour
    {
        public float verticalForce;
    }

    public class WingsLevelModule : LevelModule
    {
        public float verticalForce = 13.0f;
        public float horizontalSpeed = 13.0f;
        public static WingsData data;

        // PRE-FLIGHT DATA
        private float oldDrag;
        private float oldMass;
        private float oldSpeed;
        private float oldMaxAngle;
        private bool orgFallDamage;
        private bool orgCrouchOnJump;
        private bool orgStickJump;

        // FLIGHT DATA
        private static Locomotion loco;
        private static bool isFlying;

        public override IEnumerator OnLoadCoroutine()
        {
            data = GameManager.local.gameObject.AddComponent<WingsData>();
            PlayerControl.local.OnJumpButtonEvent += OnJumpEvent;
            new Harmony("Turn").PatchAll();
            return base.OnLoadCoroutine();
        }

        private void InitValues()
        {
            data.verticalForce = verticalForce;
        }

        private void OnJumpEvent(bool active, EventTime eventTime)
        {
            if (eventTime == EventTime.OnEnd && active && !Player.local.locomotion.isGrounded)
            {
                if (isFlying)
                    DeactivateFly();
                else
                    ActivateFly();
            }
        }

        private void ActivateFly()
        {
            loco = Player.local.locomotion;

            // STORE ORIGINAL STATS
            oldSpeed = loco.airSpeed;
            oldMaxAngle = loco.groundAngle;
            oldDrag = loco.rb.drag;
            oldMass = loco.rb.mass;
            orgFallDamage = Player.fallDamage;
            orgCrouchOnJump = Player.crouchOnJump;
            orgStickJump = GameManager.options.allowStickJump;

            // ENABLE FLIGHT STATS
            loco.groundAngle = -359f;
            loco.rb.useGravity = false;
            loco.rb.mass = 100000f;
            loco.rb.drag = 0.9f;
            loco.velocity = Vector3.zero;
            Player.fallDamage = false;
            Player.crouchOnJump = false;
            GameManager.options.allowStickJump = false;
            isFlying = true;
        }

        private void DeactivateFly()
        {
            loco.groundAngle = oldMaxAngle;
            isFlying = false;
            loco.rb.drag = oldDrag;
            loco.rb.useGravity = true;
            loco.rb.mass = oldMass;
            loco.airSpeed = oldSpeed;
            Player.fallDamage = orgFallDamage;
            Player.crouchOnJump = orgCrouchOnJump;
            GameManager.options.allowStickJump = orgStickJump;
        }

        [HarmonyPatch(typeof(PlayerControl), "Turn")]
        class TurnFix
        {
            public static void Postfix(Side side, Vector2 axis)
            {
                if (isFlying && axis.y != 0.0)
                    if (!Pointer.GetActive() || !Pointer.GetActive().isPointingUI)
                        loco.rb.AddForce(Vector3.up * data.verticalForce * axis.y, ForceMode.Acceleration);
            }
        }

        public override void Update()
        {
            base.Update();
            InitValues();

            if (isFlying)
            {
                if (Player.local.creature)
                {
                    loco.airSpeed = horizontalSpeed / 100f; // Made horizontalSpeed bigger to allow MCM to give more granularity
                    DestabilizeHeldNPC(Player.local.handLeft);
                    DestabilizeHeldNPC(Player.local.handRight);
                }
                else
                    isFlying = false;
            }

        }

        private static void DestabilizeHeldNPC(PlayerHand side)
        {
            if (side.ragdollHand.grabbedHandle)
            {
                Creature grabbedCreature = side.ragdollHand.grabbedHandle.gameObject.GetComponentInParent<Creature>();
                if (grabbedCreature)
                {
                    if (grabbedCreature.ragdoll.state != Ragdoll.State.Inert)
                        grabbedCreature.ragdoll.SetState(Ragdoll.State.Destabilized);
                }
                else
                {
                    foreach (RagdollHand ragdollHand in side.ragdollHand.grabbedHandle.handlers)
                    {
                        Creature creature = ragdollHand.gameObject.GetComponentInParent<Creature>();
                        if (creature && creature != Player.currentCreature)
                            ragdollHand.TryRelease();
                    }
                }
            }
        }
    }
}