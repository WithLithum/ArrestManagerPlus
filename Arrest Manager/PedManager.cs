using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rage;
using System.Windows.Forms;
using RAGENativeUI.Elements;
using RAGENativeUI;
using LSPD_First_Response.Mod.API;
using Rage.Native;
using static Arrest_Manager.SceneManager;
using Albo1125.Common.CommonLibrary;

namespace Arrest_Manager
{
    internal class PedManager
    {
        private static bool grabShortcutMessageShown;
        private static bool enableGrab;
        private static bool enableFollow;
        public static Ped pedfollowing { get; private set; }

        private Vehicle taxi;
        private Ped taxidriver;
        private Ped pedtobepickedup;
        private static readonly List<Ped> pedsBeingPickedUp = new List<Ped>();

        internal static Keys GrabPedKey { get; set; } = Keys.T;
        internal static Keys GrabPedModifierKey { get; set; } = Keys.LShiftKey;
        internal static Keys TackleKey { get; set; } = Keys.E;
        internal static ControllerButtons TackleButton { get; set; } = ControllerButtons.A;

        internal static Keys PlacePedInVehicleKey { get; set; } = Keys.G;

        public static Ped GetNearestValidPed(float radius = 2.5f, bool allowPursuitPeds = false, int subtitleDisplayTime = 8000)
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

            if (Functions.IsPedStoppedByPlayer(nearestPed)) 
            {
                Game.DisplayHelp("To grab this subject, you must dismiss them from stop (on foot) first.", subtitleDisplayTime);
                return null;
            }

            if (nearestPed.IsInAnyVehicle(false))
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

        public static void RequestTransportToHospitalForNearestPed()
        {
            Game.DisplayHelp("This function is deprecated.");
        }

        public static void GrabPed()
        {
            Blip pedBlip;
            GameFiber.StartNew(() =>
            {
                pedfollowing = GetNearestValidPed();
                if (!pedfollowing) return;

                enableGrab = true;
                GrabItem.Text = "Let go";
                CallTaxiItem.Enabled = false;
                FollowItem.Enabled = false;
                pedBlip = pedfollowing.AttachBlip();
                pedBlip.Color = System.Drawing.Color.Yellow;
                grabShortcutMessageShown = true;
                pedBlip.Flash(400, -1);
                pedfollowing.Rotation = Game.LocalPlayer.Character.Rotation;
                Game.LocalPlayer.Character.Tasks.PlayAnimation("doors@", "door_sweep_r_hand_medium", 9f, AnimationFlags.StayInEndFrame | AnimationFlags.SecondaryTask | AnimationFlags.UpperBodyOnly).WaitForCompletion(2000);

                if (EntryPoint.IsLSPDFRPlusRunning)
                {
                    API.LSPDFRPlusFuncs.AddCountToStatistic(Main.PluginName, "People grabbed");
                }
                pedfollowing.Tasks.ClearImmediately();

                NativeFunction.Natives.ATTACH_ENTITY_TO_ENTITY(pedfollowing, Game.LocalPlayer.Character, (int)PedBoneId.RightHand, 0.2f, 0.4f, 0f, 0f, 0f, 0f, true, true, false, false, 2, true);
                API.Functions.OnPlayerGrabbedPed(pedfollowing);
                while (true)
                {
                    GameFiber.Yield();
                    if (!pedfollowing.Exists()) break;
                    NativeFunction.Natives.ATTACH_ENTITY_TO_ENTITY(pedfollowing, Game.LocalPlayer.Character, (int)PedBoneId.RightHand, 0.2f, 0.4f, 0f, 0f, 0f, 0f, true, true, false, false, 2, true);
                    if (Game.LocalPlayer.Character.GetNearbyVehicles(1).Length > 0 && Functions.IsPedArrested(pedfollowing))
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
                                Game.DisplayHelp("Press ~b~" + EntryPoint.kc.ConvertToString(PlacePedInVehicleKey) + "~s~ to place the suspect in the vehicle.");
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

                                            if (nearestveh.Doors[SeatToPutInto + 1].IsOpen || waitCount >= 6 || pedfollowing.IsInVehicle(nearestveh, false))
                                            {
                                                pedfollowing.Detach();
                                                GameFiber.Sleep(500);
                                                break;
                                            }
                                            if (pedfollowing.Exists() && !pedfollowing.IsDead)
                                            {
                                                NativeFunction.Natives.TASK_OPEN_VEHICLE_DOOR(Game.LocalPlayer.Character, nearestveh, 6000f, SeatToPutInto, 1.47f);
                                            }
                                        }
                                    }

                                    pedfollowing.Detach();
                                    pedfollowing.Tasks.EnterVehicle(nearestveh, 4000, SeatToPutInto).WaitForCompletion();
                                    if (!pedfollowing.IsInVehicle(nearestveh, false))
                                    {
                                        if (Game.LocalPlayer.Character.IsInVehicle(nearestveh, false) && Game.LocalPlayer.Character.SeatIndex == SeatToPutInto)
                                        {
                                            Game.LocalPlayer.Character.Tasks.ClearImmediately();
                                        }
                                        pedfollowing.WarpIntoVehicle(nearestveh, SeatToPutInto);
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
                    if (!enableGrab || Game.LocalPlayer.Character.IsInAnyVehicle(false) || pedfollowing.IsInAnyVehicle(true) || Game.LocalPlayer.Character.DistanceTo(pedfollowing) > 4f)
                    {
                        
                        break;
                    }

                }

                if (pedfollowing.Exists() && !pedfollowing.IsInAnyVehicle(false))
                {
                    pedfollowing.Detach();
                    pedfollowing.Tasks.StandStill(7000);
                }

                Game.LocalPlayer.Character.Tasks.ClearSecondary();
                enableGrab = false;
                GrabItem.Text = "Grab";
                if (pedBlip.Exists()) { pedBlip.Delete(); }
                CallTaxiItem.Enabled = true;
                FollowItem.Enabled = true;
            });
        }

        public static void MakePedFollowPlayer()
        {
            Blip PedBlip;
            GameFiber.StartNew(delegate
            {
                pedfollowing = GetNearestValidPed();
                if (!pedfollowing) { return; }
                PedBlip = pedfollowing.AttachBlip();
                PedBlip.Color = System.Drawing.Color.Yellow;
                
                PedBlip.Flash(400, -1);
                enableFollow = true;
                FollowItem.Text = "Stop follow";
                CallTaxiItem.Enabled = false;
                GrabItem.Enabled = false;
                if (TransportToHospitalItem != null)
                {
                    TransportToHospitalItem.Enabled = false;
                }
                if (EntryPoint.IsLSPDFRPlusRunning)
                {
                    API.LSPDFRPlusFuncs.AddCountToStatistic(Main.PluginName, "People made to follow you");
                }
                while (pedfollowing.Exists())
                {
                    GameFiber.Yield();
                    if (!pedfollowing.Exists()) { break; }
                    if (enableFollow)
                    {
                        if (Vector3.Distance(Game.LocalPlayer.Character.Position, pedfollowing.Position) > 2.3f)
                        {
                            var follow = pedfollowing.Tasks.FollowNavigationMeshToPosition(Game.LocalPlayer.Character.GetOffsetPosition(Vector3.RelativeBack * 1.5f), pedfollowing.Heading, 1.6f);
                            follow.WaitForCompletion(600);
                        }
                        
                    }
                    else
                    {
                        
                        break;
                    }
                }
                if (pedfollowing.Exists())
                {
                    
                    pedfollowing.Tasks.StandStill(7000);
                }
                enableFollow = false;
                FollowItem.Text = "Follow";
                if (PedBlip.Exists()) { PedBlip.Delete(); }
                CallTaxiItem.Enabled = true;
                GrabItem.Enabled = true;
                if (TransportToHospitalItem != null)
                {
                    TransportToHospitalItem.Enabled = true;
                }
            });
            
        }
        
        public static void ArrestPed(Ped suspect = null)
        {
            GameFiber.StartNew(delegate
            {
                if (!suspect)
                {
                    suspect = GetNearestValidPed(3.5f);
                    if (!suspect) { return; }
                }
                if (Functions.IsPedArrested(suspect)) { Game.DisplaySubtitle("Ped is already arrested", 3000); return; }
                if (EntryPoint.suspectsPendingTransport.Contains(suspect)) { Game.DisplaySubtitle("Transport already pending for ped", 3000); return; }
                if (suspect.IsInAnyVehicle(false)) { Game.DisplaySubtitle("Remove ped from vehicle", 3000); return; }
                if (pedsBeingPickedUp.Contains(suspect)) 
                {
                    Game.DisplayHelp("A taxi is already assigned.");
                    return;
                }

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
                suspect.Tasks.ClearImmediately();
                suspect.Tasks.StandStill(-1);
                suspect.Tasks.PlayAnimation("mp_arresting", "idle", 8f, AnimationFlags.UpperBodyOnly | AnimationFlags.SecondaryTask | AnimationFlags.Loop);
                
                EntryPoint.suspectsArrestedByPlayer.Add(suspect);
                API.Functions.OnPlayerArrestedPed(suspect);
            });
        }

        public void CallTaxi()
        {
            GameFiber.StartNew(() =>
            {
                try {
                    
                    pedtobepickedup = GetNearestValidPed();
                    if (!pedtobepickedup) { return; }
                    
                    if (Functions.IsPedArrested(pedtobepickedup)) { return; }
                    if (EntryPoint.suspectsPendingTransport.Contains(pedtobepickedup)) { return; }
                    if (pedtobepickedup.IsInAnyVehicle(false)) { Game.DisplaySubtitle("Remove ped from vehicle", 3000); return; }
                    if (pedsBeingPickedUp.Contains(pedtobepickedup)) { Game.DisplaySubtitle("Taxi already enroute", 3000); return; }
                    ToggleMobilePhone(Game.LocalPlayer.Character, true);
                    pedsBeingPickedUp.Add(pedtobepickedup);
                    pedtobepickedup.IsPersistent = true;
                    pedtobepickedup.BlockPermanentEvents = true;
                    pedtobepickedup.Tasks.StandStill(-1);
                    Functions.SetPedCantBeArrestedByPlayer(pedtobepickedup, true);
                    if (EntryPoint.IsLSPDFRPlusRunning)
                    {
                        API.LSPDFRPlusFuncs.AddCountToStatistic(Main.PluginName, "Taxis called");
                    }
                    float Heading;
                    bool UseSpecialID = true;
                    Vector3 SpawnPoint;
                    float travelDistance;
                    int waitCount = 0;
                    while (true)
                    {
                        GetSpawnPoint(pedtobepickedup.Position, out SpawnPoint, out Heading, UseSpecialID);
                        travelDistance = NativeFunction.Natives.CALCULATE_TRAVEL_DISTANCE_BETWEEN_POINTS<float>( SpawnPoint.X, SpawnPoint.Y, SpawnPoint.Z, pedtobepickedup.Position.X, pedtobepickedup.Position.Y, pedtobepickedup.Position.Z);
                        waitCount++;
                        if (Vector3.Distance(pedtobepickedup.Position, SpawnPoint) > EntryPoint.SceneManagementSpawnDistance - 15f && travelDistance < (EntryPoint.SceneManagementSpawnDistance * 4.5f))
                        {
                            var direction = pedtobepickedup.Position - SpawnPoint;
                            direction.Normalize();

                            float HeadingToPlayer = MathHelper.ConvertDirectionToHeading(direction);

                            if (Math.Abs(MathHelper.NormalizeHeading(Heading) - MathHelper.NormalizeHeading(HeadingToPlayer)) < 150f)
                            {
                                break;
                            }
                        }

                        if (waitCount >= 400)
                        {
                            UseSpecialID = false;
                        }
                        if (waitCount == 600)
                        {
                            Game.DisplayNotification("Take the suspect ~s~to a more reachable location.");
                            Game.DisplayNotification("Alternatively, press ~b~Y ~s~to force a spawn in the ~g~wilderness.");
                        }
                        if ((waitCount >= 600) && Albo1125.Common.CommonLibrary.ExtensionMethods.IsKeyDownComputerCheck(Keys.Y))
                        {
                            SpawnPoint = Game.LocalPlayer.Character.Position.Around(15f);
                            break;
                        }
                        GameFiber.Yield();

                    }



                    GameFiber.Wait(3000);
                    ToggleMobilePhone(Game.LocalPlayer.Character, false);
                    taxi = new Vehicle("TAXI", SpawnPoint, Heading);
                    taxi.IsPersistent = true;
                    taxi.IsTaxiLightOn = false;
                    var taxiblip = taxi.AttachBlip();
                    taxiblip.Color = System.Drawing.Color.Blue;
                    taxiblip.Flash(500, -1);
                    taxidriver = taxi.CreateRandomDriver();
                    taxidriver.IsPersistent = true;
                    taxidriver.BlockPermanentEvents = true;
                    taxidriver.Money = 1233;
                    
                    Game.DisplayNotification("~b~Taxi Control~w~: Dispatching taxi to your location.");
                    driveToEntity(taxidriver, taxi, pedtobepickedup, true);
                    NativeFunction.Natives.START_VEHICLE_HORN(taxi, 5000, 0, true);
                    if (taxi.Speed > 15f)
                    {
                        NativeFunction.Natives.SET_VEHICLE_FORWARD_SPEED(taxi, 15f);
                    }

                    taxidriver.Tasks.PerformDrivingManeuver(VehicleManeuver.GoForwardStraightBraking);
                    GameFiber.Sleep(600);
                    taxidriver.Tasks.PerformDrivingManeuver(VehicleManeuver.Wait);
                    if (taxiblip.Exists()) { taxiblip.Delete(); }
                    if (pedfollowing == pedtobepickedup) enableFollow = false;
                    NativeFunction.Natives.SET_PED_CAN_RAGDOLL(pedtobepickedup, false);
                    pedtobepickedup.Tasks.Clear();
                    pedtobepickedup.Tasks.FollowNavigationMeshToPosition(taxi.GetOffsetPosition(Vector3.RelativeLeft * 2f), taxi.Heading, 1.65f).WaitForCompletion(12000);
                    pedtobepickedup.Tasks.EnterVehicle(taxi, 8000, 1).WaitForCompletion();
                    
                    taxidriver.Dismiss();
                    taxi.Dismiss();
                    
                    while (true)
                    {
                        GameFiber.Yield();
                        try
                        {
                            if (pedtobepickedup.Exists())
                            {
                                if (!taxi.Exists())
                                {
                                    pedtobepickedup.Delete();
                                }
                                if (!pedtobepickedup.IsDead)
                                {
                                    if (Vector3.Distance(Game.LocalPlayer.Character.Position, pedtobepickedup.Position) > 80f)
                                    {
                                        pedtobepickedup.Delete();
                                        break;
                                    }
                                    if (!pedtobepickedup.IsInVehicle(taxi, false))
                                    {
                                        pedtobepickedup.Delete();
                                        break;
                                    }
                                }
                                else
                                {
                                    pedtobepickedup.Delete();
                                    break;
                                }
                            }
                            else
                            {
                                break;
                            }

                        }
                        catch (Exception e)
                        {
                            Game.LogTrivial(e.ToString());
                            if (pedtobepickedup.Exists())
                            {
                                pedtobepickedup.Delete();
                            }
                            break;
                        }
                    }
                    //Game.DisplayNotification("Cleaned up");
                }
                catch(Exception e)
                {
                    Game.LogTrivial(e.ToString());
                    Game.DisplayNotification("The taxi pickup service was interrupted");
                    if (taxi.Exists()) { taxi.Delete(); }
                    if (taxidriver.Exists()) { taxidriver.Delete(); }
                    if (pedtobepickedup.Exists()) { pedtobepickedup.Delete(); }
                }



            });
        }



        public static UIMenu PedManagementMenu;
        public static UIMenuItem FollowItem;
        public static UIMenuItem GrabItem;
        public static UIMenuItem CallTaxiItem;
        public static UIMenuItem TransportToHospitalItem;
        public static UIMenuItem ArrestItem;
        public static UIMenuItem CallCoronerItem;

        public static void CreatePedManagementMenu()
        {
            PedManagementMenu = new UIMenu("Ped Manager", "");
            PedManagementMenu.AddItem(SceneManager.MenuSwitchListItem);
            PedManagementMenu.AddItem(FollowItem = new UIMenuItem("Follow"));
            PedManagementMenu.AddItem(GrabItem = new UIMenuItem("Grab"));
            PedManagementMenu.AddItem(CallTaxiItem = new UIMenuItem("Call taxi"));
            PedManagementMenu.AddItem(ArrestItem = new UIMenuItem("Arrest", "Instantly arrests the ped. Move the ped around and place them in your vehicle using the grab/follow features."));
            PedManagementMenu.AddItem(CallCoronerItem = new UIMenuItem("Coroner", "Calls a coroner to deal with all nearby dead people."));
            if (EntryPoint.IsLSPDFRPluginRunning("BetterEMS", new Version("1.0.0.0")))
            {
                PedManagementMenu.AddItem(TransportToHospitalItem = new UIMenuItem("Transport to hospital"));
            }
            PedManagementMenu.RefreshIndex();
            PedManagementMenu.MouseControlsEnabled = false;
            PedManagementMenu.AllowCameraMovement = true;
            PedManagementMenu.OnItemSelect += OnItemSelect;
            

        }

        public static void OnItemSelect(UIMenu sender, UIMenuItem selectedItem, int index)
        {
            if (sender != PedManagementMenu) { return; }
            Rage.Native.NativeFunction.Natives.SET_PED_STEALTH_MOVEMENT(Game.LocalPlayer.Character, 0, 0);
            if (selectedItem == FollowItem)
            {
                
                if (!enableFollow)
                {
                    MakePedFollowPlayer();
                    
                }
                else
                {
                    enableFollow = false;
                    
                }
            }
            else if (selectedItem == GrabItem)
            {
                
                if (!enableGrab)
                {
                    if (!grabShortcutMessageShown)
                    {
                        Game.DisplaySubtitle("You can also grab peds by pressing ~b~" + EntryPoint.kc.ConvertToString(GrabPedKey) + " " + EntryPoint.kc.ConvertToString(GrabPedModifierKey), 4000);
                    }
                    GrabPed();
                }
                else
                {
                    enableGrab = false;
                }
            }
            else if (selectedItem == CallTaxiItem)
            {
                
                new PedManager().CallTaxi();
                PedManagementMenu.Visible = false;
                //taxi
            }
            else if (selectedItem == CallCoronerItem)
            {
                SceneManager.callCoronerTime = true;
                sender.Visible = false;
            }
            else if (selectedItem == TransportToHospitalItem)
            {
                if (EntryPoint.IsLSPDFRPluginRunning("BetterEMS", new Version("0.5.0.0")))
                {
                    RequestTransportToHospitalForNearestPed();
                    sender.Visible = false;
                }
            }
            else if (selectedItem == ArrestItem)
            {
                ArrestPed();
            }
        }
    }
}

