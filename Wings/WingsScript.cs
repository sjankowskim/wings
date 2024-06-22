using UnityEngine;
using ThunderRoad;

namespace Wings
{
    public class WingsScript : ThunderScript
    {
        // Source: @Wully on BaS Discord
        // Big help in getting this ready for U12
        public static ModOptionFloat[] zeroToOneHundered()
        {
            ModOptionFloat[] options = new ModOptionFloat[101];
            float val = 0;
            for (int i = 0; i < options.Length; i++)
            {
                options[i] = new ModOptionFloat(val.ToString("0.0"), val);
                val += 1f;
            }
            return options;
        }

        [ModOption(name: "Use Wings Mod", tooltip: "Turns on/off the Wings mod.", defaultValueIndex = 1, order = 0)]
        public static bool useWingsMod;

        [ModOption(name: "Vertical Force", tooltip: "Determines how fast the player can fly vertically.", valueSourceName = nameof(zeroToOneHundered), defaultValueIndex = 13, order = 1)]
        public static float verticalForce;

        [ModOption(name: "Horizontal Speed", tooltip: "Determines how fast the player can fly horizontally.", valueSourceName = nameof(zeroToOneHundered), defaultValueIndex = 13, order = 2)]
        public static float horizontalSpeed;

        // PRE-FLIGHT DATA
        private float oldDrag;
        private float oldMass;
        private float oldSpeed;
        private float oldMaxAngle;
        private bool orgFallDamage;
        private bool orgCrouchOnJump;
        private bool orgStickJump;

        // FLIGHT DATA
        private Locomotion loco;
        private bool isFlying;
        private bool pressedIn, previousPressedIn;

        public override void ScriptLoaded(ModManager.ModData modData)
        {
            base.ScriptLoaded(modData);
            if (PlayerControl.loader == PlayerControl.Loader.OpenVR)
            {
                ((InputSteamVR)PlayerControl.input).jumpAction.onStateDown += ((fromAction, fromSource) =>
                {
                    if (!Player.local.locomotion.isGrounded)
                    {
                        if (isFlying)
                            DeactivateFly();
                        else
                            ActivateFly();
                    }
                });
            }
        }

        private void ActivateFly()
        {
            loco = Player.local.locomotion;

            // STORE ORIGINAL STATS
            oldSpeed = loco.horizontalAirSpeed;
            oldMaxAngle = loco.groundAngle;
            oldDrag = loco.physicBody.drag;
            oldMass = loco.physicBody.mass;
            orgFallDamage = Player.fallDamage;
            orgCrouchOnJump = Player.crouchOnJump;
            orgStickJump = GameManager.options.allowStickJump;

            if (useWingsMod)
            {
                // ENABLE FLIGHT STATS
                loco.groundAngle = -359f;
                loco.physicBody.useGravity = false;
                loco.physicBody.mass = 100000f;
                loco.physicBody.drag = 0.9f;
                loco.velocity = Vector3.zero;
                Player.fallDamage = false;
                Player.crouchOnJump = false;
                GameManager.options.allowStickJump = false;
                isFlying = true;
            }
        }

        private void DeactivateFly()
        {
            loco.groundAngle = oldMaxAngle;
            isFlying = false;
            loco.physicBody.drag = oldDrag;
            loco.physicBody.useGravity = true;
            loco.physicBody.mass = oldMass;
            loco.horizontalAirSpeed = oldSpeed;
            Player.fallDamage = orgFallDamage;
            Player.crouchOnJump = orgCrouchOnJump;
            GameManager.options.allowStickJump = orgStickJump;
        }

        public override void ScriptUpdate()
        {
            base.ScriptUpdate();
            if (PlayerControl.loader == PlayerControl.Loader.Oculus)
                pressedIn = ((InputXR_Oculus)PlayerControl.input).rightController.thumbstickClick.GetDown();

            if (Player.currentCreature)
            {
                if (!Player.local.locomotion.isGrounded)
                {
                    if (PlayerControl.loader == PlayerControl.Loader.Oculus && pressedIn && !previousPressedIn)
                    {
                        if (isFlying)
                            DeactivateFly();
                        else
                            ActivateFly();
                    }
                    if (isFlying)
                    {
                        loco.horizontalAirSpeed = horizontalSpeed / 100f;
                        DestabilizeHeldNPC(Player.local.handLeft);
                        DestabilizeHeldNPC(Player.local.handRight);

                        if (PlayerControl.loader == PlayerControl.Loader.Oculus)
                            TryFlyUp(((InputXR_Oculus)PlayerControl.input).rightController.thumbstick.GetValue());
                        else
                            TryFlyUp(((InputSteamVR)PlayerControl.input).turnAction.axis);
                    }
                }
            }
            else
                isFlying = false;

            previousPressedIn = pressedIn;
        }

        private void TryFlyUp(Vector2 axis)
        {
            if (axis.y != 0.0 && (!Pointer.GetActive() || !Pointer.GetActive().isPointingUI))
                loco.physicBody.AddForce(Vector3.up * verticalForce * axis.y, ForceMode.Acceleration);
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