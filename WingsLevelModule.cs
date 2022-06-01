using UnityEngine;
using ThunderRoad;
using System.Collections;
using HarmonyLib;

namespace Wings
{
    public class WingsLevelModule : LevelModule
    {
        private static float? cd;
        private static WingsData wingData;

        // PRE-FLIGHT DATA
        private static float oldDrag;
        private static float oldMass;
        private static float oldSpeed;
        private static float oldMaxAngle;
        private static bool orgFallDamage;
        private static bool orgCrouchOnJump;
        private static bool orgStickJump;

        // FLIGHT DATA
        private static Locomotion loco;
        private static bool isFlying;
        public float hoizontalSpeedMult;
        public float verticalAcceleration;

        public override IEnumerator OnLoadCoroutine()
        {
            Debug.Log("(Wings) Loaded successfully!");
            wingData = GameManager.local.gameObject.AddComponent<WingsData>();
            new Harmony("Jump").PatchAll();
            new Harmony("Turn").PatchAll();
            return base.OnLoadCoroutine();
        }

        [HarmonyPatch(typeof(PlayerControl), "Jump")]
        class JumpFix
        {
            public static void Postfix(bool active)
            {
                if (active && !Player.local.locomotion.isGrounded)
                {
                    if (cd == null)
                    {
                        if (isFlying)
                            DeactivateFly();
                        else
                            ActivateFly();
                    }
                    cd = 0.1f;
                }
            }

            private static void ActivateFly()
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
                loco.airSpeed = oldSpeed * wingData.hoizontalSpeedMult;
                Player.fallDamage = false;
                Player.crouchOnJump = false;
                GameManager.options.allowStickJump = false;
                isFlying = true;
            }

            private static void DeactivateFly()
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
        }

        [HarmonyPatch(typeof(PlayerControl), "Turn")]
        class TurnFix
        {
            public static void Postfix(Side side, Vector2 axis)
            {
                if (isFlying && axis.y != 0.0)
                    if (!Pointer.GetActive() || !Pointer.GetActive().isPointingUI)
                        loco.rb.AddForce(Vector3.up * wingData.verticalAcceleration * axis.y, ForceMode.Acceleration);
            }
        }

        public override void Update()
        {
            base.Update();

            wingData.verticalAcceleration = verticalAcceleration;
            wingData.hoizontalSpeedMult = hoizontalSpeedMult;

            if (isFlying)
            {
                if (Player.local.creature)
                {
                    DestabilizeHeldNPC(Player.local.handLeft);
                    DestabilizeHeldNPC(Player.local.handRight);
                }
                else
                    isFlying = false;
            }

            if (cd != null)
            {
                cd -= Time.deltaTime;
                if (cd <= 0f)
                    cd = null;
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