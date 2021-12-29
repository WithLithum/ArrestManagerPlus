using System;
using System.Linq;
using Rage;
using System.Windows.Forms;
using LSPD_First_Response.Mod.API;
using Rage.Native;
using Arrest_Manager.Services;
using RelaperCommons.FirstResponse;
using LemonUI.Menus;
using static Arrest_Manager.SceneManager;

namespace Arrest_Manager
{
    internal static class PedManager
    {
        private static bool grabShortcutMessageShown;
        internal static bool IsGrabEnabled { get; set; }
        internal static bool IsFollowingEnabled { get; set; }
        internal static Ped FollowingPed { get; private set; }

        internal static Keys GrabPedKey { get; set; } = Keys.T;
        internal static Keys GrabPedModifierKey { get; set; } = Keys.LShiftKey;
        internal static Keys TackleKey { get; set; } = Keys.E;
        internal static ControllerButtons TackleButton { get; set; } = ControllerButtons.A;

        internal static Keys PlacePedInVehicleKey { get; set; } = Keys.G;

        public static Ped GetNearestValidPed(float radius = 2.5f, bool allowPursuitPeds = false, bool allowStopped = false, bool allowInVehicle = false, int subtitleDisplayTime = 8000)
        {
            if (Game.LocalPlayer.Character.GetNearbyPeds(1).Length == 0 || Game.LocalPlayer.Character.IsInAnyVehicle(false)) { return null; }
            var nearestPed = Game.LocalPlayer.Character.GetNearbyPeds(1)[0];

            if (Functions.IsPedACop(nearestPed))
            {
                if (Game.LocalPlayer.Character.GetNearbyPeds(2).Length >= 2) { nearestPed = Game.LocalPlayer.Character.GetNearbyPeds(2)[1]; }
                if (Functions.IsPedACop(nearestPed))
                {
                    return null;
                }
            }

            if (Vector3.Distance(Game.LocalPlayer.Character.Position, nearestPed.Position) > radius) { Game.DisplaySubtitle("Get closer to the ped", subtitleDisplayTime); return null; }
            if (!allowPursuitPeds && Functions.GetActivePursuit() != null && Functions.GetPursuitPeds(Functions.GetActivePursuit()).Contains(nearestPed))
            {
                return null;
            }

            if (Functions.IsPedStoppedByPlayer(nearestPed) && !allowStopped)
            {
                Game.DisplayHelp("To grab this subject, you must dismiss them from stop (on foot) first.", subtitleDisplayTime);
                return null;
            }

            if (nearestPed.IsInAnyVehicle(false) && !allowInVehicle)
            {
                Game.DisplayHelp("Suspects in vehicle cannot be grabbed.", subtitleDisplayTime);
                return null;
            }

            if (!nearestPed.IsHuman)
            {
                Game.DisplayHelp("Animals cannot be grabbed.", subtitleDisplayTime);
            }

            if (Functions.IsPedGettingArrested(nearestPed) && !Functions.IsPedArrested(nearestPed)) { return null; }
            return nearestPed;
        }

        // TODO: Remove
        [Obsolete("Request transport to hospital is deprecated. Call via BetterEMS directly.")]
        public static void RequestTransportToHospitalForNearestPed()
        {
            Game.DisplayHelp("This function is deprecated.");
        }

        public static void GrabPed()
        {
            Blip pedBlip;
            GameFiber.StartNew(() =>
            {
                FollowingPed = GetNearestValidPed();
                if (!FollowingPed) return;

                IsGrabEnabled = true;
                itemGrab.Title = "Release Grabbed Ped";
                itemCallTaxi.Enabled = false;
                itemFollow.Enabled = false;
                pedBlip = FollowingPed.AttachBlip();
                pedBlip.Color = System.Drawing.Color.Yellow;
                grabShortcutMessageShown = true;
                pedBlip.Flash(400, -1);
                FollowingPed.Rotation = Game.LocalPlayer.Character.Rotation;
                Game.LocalPlayer.Character.Tasks.PlayAnimation("doors@", "door_sweep_r_hand_medium", 9f, AnimationFlags.StayInEndFrame | AnimationFlags.SecondaryTask | AnimationFlags.UpperBodyOnly).WaitForCompletion(2000);

                FollowingPed.Tasks.ClearImmediately();

                NativeFunction.Natives.ATTACH_ENTITY_TO_ENTITY(FollowingPed, Game.LocalPlayer.Character, (int)PedBoneId.RightHand, 0.2f, 0.4f, 0f, 0f, 0f, 0f, true, true, false, false, 2, true);
                API.Functions.OnPlayerGrabbedPed(FollowingPed);
                while (true)
                {
                    GameFiber.Yield();
                    if (!FollowingPed.Exists()) break;
                    NativeFunction.Natives.ATTACH_ENTITY_TO_ENTITY(FollowingPed, Game.LocalPlayer.Character, (int)PedBoneId.RightHand, 0.2f, 0.4f, 0f, 0f, 0f, 0f, true, true, false, false, 2, true);
                    if (Game.LocalPlayer.Character.GetNearbyVehicles(1).Length > 0 && Functions.IsPedArrested(FollowingPed))
                    {
                        var nearestveh = Game.LocalPlayer.Character.GetNearbyVehicles(1)[0];

                        if (Game.LocalPlayer.Character.DistanceTo(nearestveh.Position) < 3.9f && nearestveh.PassengerCapacity >= 3)
                        {

                            int SeatToPutInto = 1;
                            if (Game.LocalPlayer.Character.DistanceTo(nearestveh.GetOffsetPosition(Vector3.RelativeLeft * 1.5f)) > Game.LocalPlayer.Character.DistanceTo(nearestveh.GetOffsetPosition(Vector3.RelativeRight * 1.5f)))
                            {
                                SeatToPutInto = 2;
                            }
                            if (nearestveh.IsSeatFree(SeatToPutInto))
                            {
                                Game.DisplayHelp("Press ~b~" + EntryPoint.KeyConvert.ConvertToString(PlacePedInVehicleKey) + "~s~ to place the suspect in the vehicle.");
                                if (Game.IsKeyDown(PlacePedInVehicleKey))
                                {
                                    if (nearestveh.GetDoors().Length > SeatToPutInto + 1)
                                    {
                                        NativeFunction.Natives.TASK_OPEN_VEHICLE_DOOR( Game.LocalPlayer.Character, nearestveh, 6000f, SeatToPutInto, 1.47f);
                                        int waitCount = 0;
                                        while (true)
                                        {
                                            GameFiber.Wait(1000);
                                            waitCount++;

                                            if (nearestveh.Doors[SeatToPutInto + 1].IsOpen || waitCount >= 6 || FollowingPed.IsInVehicle(nearestveh, false))
                                            {
                                                FollowingPed.Detach();
                                                GameFiber.Sleep(500);
                                                break;
                                            }
                                            if (FollowingPed.Exists() && !FollowingPed.IsDead)
                                            {
                                                NativeFunction.Natives.TASK_OPEN_VEHICLE_DOOR(Game.LocalPlayer.Character, nearestveh, 6000f, SeatToPutInto, 1.47f);
                                            }
                                        }
                                    }

                                    FollowingPed.Detach();
                                    FollowingPed.Tasks.EnterVehicle(nearestveh, 4000, SeatToPutInto).WaitForCompletion();
                                    if (!FollowingPed.IsInVehicle(nearestveh, false))
                                    {
                                        if (Game.LocalPlayer.Character.IsInVehicle(nearestveh, false) && Game.LocalPlayer.Character.SeatIndex == SeatToPutInto)
                                        {
                                            Game.LocalPlayer.Character.Tasks.ClearImmediately();
                                        }
                                        FollowingPed.WarpIntoVehicle(nearestveh, SeatToPutInto);
                                    }
                                    break;
                                }
                            }
                        }
                    }

                    if (!NativeFunction.Natives.IS_ENTITY_PLAYING_ANIM<bool>(Game.LocalPlayer.Character, "doors@", "door_sweep_r_hand_medium", 3))
                    {
                        Game.LocalPlayer.Character.Tasks.PlayAnimation("doors@", "door_sweep_r_hand_medium", 9f, AnimationFlags.StayInEndFrame | AnimationFlags.SecondaryTask | AnimationFlags.UpperBodyOnly);
                    }
                    if (!IsGrabEnabled || Game.LocalPlayer.Character.IsInAnyVehicle(false) || FollowingPed.IsInAnyVehicle(true) || Game.LocalPlayer.Character.DistanceTo(FollowingPed) > 4f)
                    {
                        
                        break;
                    }

                }

                if (FollowingPed.Exists() && !FollowingPed.IsInAnyVehicle(false))
                {
                    FollowingPed.Detach();
                    FollowingPed.Tasks.StandStill(7000);
                }

                Game.LocalPlayer.Character.Tasks.ClearSecondary();
                IsGrabEnabled = false;
                itemGrab.Title = "Grab Nearest Ped";
                if (pedBlip.Exists()) { pedBlip.Delete(); }
                itemCallTaxi.Enabled = true;
                itemFollow.Enabled = true;
            });
        }

        public static void MakePedFollowPlayer()
        {
            Blip PedBlip;
            GameFiber.StartNew(delegate
            {
                FollowingPed = GetNearestValidPed();
                if (!FollowingPed) { return; }
                PedBlip = FollowingPed.AttachBlip();
                PedBlip.Color = System.Drawing.Color.Yellow;
                
                PedBlip.Flash(400, -1);
                IsFollowingEnabled = true;
                itemFollow.Title = "Ask to Stop Following";
                itemCallTaxi.Enabled = false;
                itemGrab.Enabled = false;

                while (FollowingPed.Exists())
                {
                    GameFiber.Yield();
                    if (!FollowingPed.Exists()) { break; }
                    if (IsFollowingEnabled)
                    {
                        if (Vector3.Distance(Game.LocalPlayer.Character.Position, FollowingPed.Position) > 2.3f)
                        {
                            var follow = FollowingPed.Tasks.FollowNavigationMeshToPosition(Game.LocalPlayer.Character.GetOffsetPosition(Vector3.RelativeBack * 1.5f), FollowingPed.Heading, 1.6f);
                            follow.WaitForCompletion(600);
                        }
                        
                    }
                    else
                    {
                        
                        break;
                    }
                }
                if (FollowingPed.Exists())
                {
                    
                    FollowingPed.Tasks.StandStill(7000);
                }
                IsFollowingEnabled = false;
                itemFollow.Title = "Ask to Follow";
                if (PedBlip.Exists()) { PedBlip.Delete(); }
                itemCallTaxi.Enabled = true;
                itemGrab.Enabled = true;
            });
        }

        public static void ArrestPed(Ped suspect = null)
        {
            GameFiber.StartNew(() =>
            {
                if (!suspect)
                {
                    suspect = GetNearestValidPed(3.5f);
                    if (!suspect) { return; }
                }
                if (Functions.IsPedArrested(suspect)) { Game.DisplaySubtitle("Ped is already arrested", 3000); return; }
                if (suspect.IsInAnyVehicle(false)) { Game.DisplaySubtitle("Remove ped from vehicle", 3000); return; }

                var pursuitPeds = Functions.GetPursuitPeds(Functions.GetActivePursuit()).ToList();
                if (Functions.GetActivePursuit() != null && (pursuitPeds.Contains(suspect)))
                {
                    if (pursuitPeds.Count == 1) 
                    { 
                        Functions.ForceEndPursuit(Functions.GetActivePursuit()); 
                    }
                    else
                    {
                        suspect.Kill();
                        suspect.MakePersistent();
                        GameFiber.Yield();
                        suspect.Resurrect();
                        suspect.Tasks.ClearImmediately();
                    }
                }

                Functions.SetPedAsArrested(suspect, true);
                suspect.MakePersistent();
                suspect.BlockPermanentEvents = true;
                suspect.Tasks.ClearImmediately();
                suspect.Tasks.StandStill(-1);
                suspect.Tasks.PlayAnimation("mp_arresting", "idle", 8f, AnimationFlags.UpperBodyOnly | AnimationFlags.SecondaryTask | AnimationFlags.Loop);
                
                EntryPoint.suspectsArrestedByPlayer.Add(suspect);
                API.Functions.OnPlayerArrestedPed(suspect);
            });
        }

        private static NativeItem itemCheckId;
        private static NativeItem itemFollow;
        private static NativeItem itemGrab;
        private static NativeItem itemCallTaxi;
        private static NativeItem itemRequestCoroner;

        public static void CreatePedManagementMenu()
        {
            itemCheckId = new NativeItem("Request Ped Status Check", "Requests status check of the nearest ped from dispatch.");
            itemFollow = new NativeItem("Ask to Follow", "Asks the nearest ped to follow the player.");
            itemGrab = new NativeItem("Grab Nearest Ped", "Grabs the nearest ped.");
            itemCallTaxi = new NativeItem("Request Taxi Escort", "Requests a taxi to pick up the target.");
            itemRequestCoroner = new NativeItem("Request Coroner Unit", "Calls a coroner to deal with all nearby dead people.");

            ManagementMenu.Add(itemCheckId);
            ManagementMenu.Add(itemCallTaxi);
            ManagementMenu.Add(itemRequestCoroner);
            ManagementMenu.Add(itemGrab);
            ManagementMenu.Add(itemFollow);

            itemCheckId.Activated += ItemCheckId_Activated;
            itemCallTaxi.Activated += ItemCallTaxi_Activated;
            itemRequestCoroner.Activated += ItemRequestCoroner_Activated;
            itemGrab.Activated += ItemGrab_Activated;
            itemFollow.Activated += ItemFollow_Activated;
        }

        private static void ItemFollow_Activated(object sender, EventArgs e)
        {
            NativeFunction.Natives.SET_PED_STEALTH_MOVEMENT(Game.LocalPlayer.Character, 0, 0);

            if (!IsFollowingEnabled)
            {
                MakePedFollowPlayer();
            }
            else
            {
                IsFollowingEnabled = false;
            }
        }

        private static void ItemGrab_Activated(object sender, EventArgs e)
        {
            NativeFunction.Natives.SET_PED_STEALTH_MOVEMENT(Game.LocalPlayer.Character, 0, 0);

            if (!IsGrabEnabled)
            {
                if (!grabShortcutMessageShown)
                {
                    Game.DisplayNotification("You can also grab suspects by pressing ~b~" + EntryPoint.KeyConvert.ConvertToString(GrabPedKey) + " " + EntryPoint.KeyConvert.ConvertToString(GrabPedModifierKey));
                }
                GrabPed();
            }
            else
            {
                IsGrabEnabled = false;
            }
        }

        private static void ItemRequestCoroner_Activated(object sender, EventArgs e)
        {
            NativeFunction.Natives.SET_PED_STEALTH_MOVEMENT(Game.LocalPlayer.Character, 0, 0);

            SceneManager.CallCoronerTime = true;
            ManagementMenu.Visible = false;
        }

        private static void ItemCallTaxi_Activated(object sender, EventArgs e)
        {
            NativeFunction.Natives.SET_PED_STEALTH_MOVEMENT(Game.LocalPlayer.Character, 0, 0);

            new Taxi().CallTaxi();
            ManagementMenu.Visible = false;
        }

        private static void ItemCheckId_Activated(object sender, EventArgs e)
        {
            NativeFunction.Natives.SET_PED_STEALTH_MOVEMENT(Game.LocalPlayer.Character, 0, 0);

            var ped = GetNearestValidPed(allowStopped: true, subtitleDisplayTime: 3000);
            if (!ped)
            {
                return;
            }

            GameFiber.StartNew(() =>
            {
                var persona = Functions.GetPersonaForPed(ped);
                Functions.PlayPlayerRadioAction(Functions.GetPlayerRadioAction(), 3000);
                RadioUtil.DisplayRadioQuote(Functions.GetPersonaForPed(Game.LocalPlayer.Character).FullName, $"Requesting status check for ~y~{persona.FullName}~w~, born on ~b~{persona.Birthday}");
                SceneManager.BleepPlayer.Play();
                GameFiber.Sleep(3500);
                RadioUtil.DisplayRadioQuote("Dispatch", "10-4, stand by...");
                GameFiber.Sleep(1000);

                Functions.DisplayPedId(ped, false);
            });
        }
    }
}

