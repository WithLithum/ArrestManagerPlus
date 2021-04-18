using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Albo1125.Common.CommonLibrary;
using LSPD_First_Response.Mod.API;
using Rage;
using Rage.Native;
using RAGENativeUI;
using RAGENativeUI.Elements;
using static Arrest_Manager.PedManager;
using static Arrest_Manager.VehicleManager;

namespace Arrest_Manager
{
    internal static class SceneManager
    {
        public static readonly System.Media.SoundPlayer BleepPlayer = new System.Media.SoundPlayer("LSPDFR/audio/scanner/Arrest Manager Audio/RADIO_BLIP.wav");
        private static Rage.Object MobilePhone;

        public static void ToggleMobilePhone(Ped ped, bool toggle)
        {
            if (toggle)
            {
                if (MobilePhone.Exists()) { MobilePhone.Delete(); }
                NativeFunction.Natives.SET_PED_CAN_SWITCH_WEAPON(ped, false);
                ped.Inventory.GiveNewWeapon(new WeaponAsset("WEAPON_UNARMED"), -1, true);
                MobilePhone = new Rage.Object(new Model("prop_police_phone"), new Vector3(0, 0, 0));
                int boneIndex = NativeFunction.Natives.GET_PED_BONE_INDEX<int>(ped, (int)PedBoneId.RightPhHand);
                NativeFunction.Natives.ATTACH_ENTITY_TO_ENTITY(MobilePhone, ped, boneIndex, 0f, 0f, 0f, 0f, 0f, 0f, true, true, false, false, 2, 1);
                ped.Tasks.PlayAnimation("cellphone@", "cellphone_call_listen_base", 1.45f, AnimationFlags.Loop | AnimationFlags.UpperBodyOnly | AnimationFlags.SecondaryTask);
            }
            else
            {
                NativeFunction.Natives.SET_PED_CAN_SWITCH_WEAPON(ped, true);
                ped.Tasks.Clear();
                if (GameFiber.CanSleepNow)
                {
                    GameFiber.Wait(800);
                }
                if (MobilePhone.Exists()) { MobilePhone.Delete(); }
            }
        }

        public static void TaskDriveToEntity(Ped driver, Vehicle vehicle, Entity target, bool getClose)
        {
            int drivingLoopCount = 0;
            bool transportVanTeleported = false;
            int waitCount = 0;
            bool forceCloseSpawn = false;

            // Get close to player with various checks
            try
            {
                GameFiber.StartNew(delegate
                {
                    while (!forceCloseSpawn)
                    {
                        GameFiber.Yield();
                        if (Albo1125.Common.CommonLibrary.ExtensionMethods.IsKeyDownComputerCheck(EntryPoint.SceneManagementKey))
                        {
                            GameFiber.Sleep(300);
                            if (Albo1125.Common.CommonLibrary.ExtensionMethods.IsKeyDownRightNowComputerCheck(EntryPoint.SceneManagementKey))
                            {
                                GameFiber.Sleep(1000);
                                if (Albo1125.Common.CommonLibrary.ExtensionMethods.IsKeyDownRightNowComputerCheck(EntryPoint.SceneManagementKey))
                                {
                                    forceCloseSpawn = true;
                                }
                                else
                                {
                                    Game.DisplayNotification("Hold down the ~b~Scene Management Key ~s~to force a close spawn.");
                                }
                            }
                        }
                    }
                });

                Task driveToPed = null;
                driver.Tasks.PerformDrivingManeuver(VehicleManeuver.GoForwardStraight).WaitForCompletion(500);

                while (Vector3.Distance(vehicle.Position, target.Position) > 35f)
                {
                    if (!target.Exists() || !target.IsValid())
                    {
                        return;
                    }

                    vehicle.Repair();
                    if (driveToPed?.IsActive != true)
                    {
                        driver.Tasks.DriveToPosition(target.Position, 15f, VehicleDrivingFlags.FollowTraffic | VehicleDrivingFlags.DriveAroundVehicles | VehicleDrivingFlags.DriveAroundObjects | VehicleDrivingFlags.AllowMedianCrossing | VehicleDrivingFlags.YieldToCrossingPedestrians);
                    }

                    NativeFunction.Natives.SET_DRIVE_TASK_DRIVING_STYLE(driver, 786607);
                    NativeFunction.Natives.SET_DRIVER_AGGRESSIVENESS(driver, 0f);
                    NativeFunction.Natives.SET_DRIVER_ABILITY(driver, 1f);
                    GameFiber.Wait(600);
                    waitCount++;
                    if (waitCount == 55)
                    {
                        Game.DisplayHelp("Service taking too long? Hold down ~b~" + EntryPoint.KeyConvert.ConvertToString(EntryPoint.SceneManagementKey) + " ~s~to speed it up.", 5000);
                    }

                    //If van isn't moving
                    if (vehicle.Speed < 2f)
                    {
                        drivingLoopCount++;
                    }

                    //if van is very far away
                    if (Vector3.Distance(target.Position, vehicle.Position) > EntryPoint.SceneManagementSpawnDistance + 65f)
                    {
                        drivingLoopCount++;
                    }

                    //If Van is stuck, relocate it
                    if (drivingLoopCount >= 33 && drivingLoopCount <= 38 && EntryPoint.AllowWarping)
                    {
                        Vector3 SpawnPoint;
                        float Heading;
                        bool UseSpecialID = true;
                        float travelDistance;
                        int wC = 0;
                        while (true)
                        {
                            GetSpawnPoint(target.Position, out SpawnPoint, out Heading, UseSpecialID);
                            travelDistance = Rage.Native.NativeFunction.Natives.CALCULATE_TRAVEL_DISTANCE_BETWEEN_POINTS<float>(SpawnPoint.X, SpawnPoint.Y, SpawnPoint.Z, target.Position.X, target.Position.Y, target.Position.Z);
                            wC++;
                            if (Vector3.Distance(target.Position, SpawnPoint) > EntryPoint.SceneManagementSpawnDistance - 15f && travelDistance < (EntryPoint.SceneManagementSpawnDistance * 4.5f))
                            {
                                var spawnDirection = (target.Position - SpawnPoint);
                                spawnDirection.Normalize();

                                var headingToPlayer = MathHelper.ConvertDirectionToHeading(spawnDirection);

                                if (Math.Abs(MathHelper.NormalizeHeading(Heading) - MathHelper.NormalizeHeading(headingToPlayer)) < 150f)
                                {
                                    break;
                                }
                            }
                            if (wC >= 400)
                            {
                                UseSpecialID = false;
                            }
                            GameFiber.Yield();
                        }

                        Game.Console.Print("Relocating because service was stuck...");
                        vehicle.Position = SpawnPoint;

                        vehicle.Heading = Heading;
                        drivingLoopCount = 39;
                    }
                    // if van is stuck for a 2nd time or takes too long, spawn it very near to the car
                    else if (((drivingLoopCount >= 70 || waitCount >= 110) && EntryPoint.AllowWarping) || forceCloseSpawn)
                    {
                        Game.Console.Print("Relocating service to a close position");

                        Vector3 SpawnPoint = World.GetNextPositionOnStreet(target.Position.Around2D(15f));

                        int waitCounter = 0;
                        while ((SpawnPoint.Z - target.Position.Z < -3f) || (SpawnPoint.Z - target.Position.Z > 3f) || (Vector3.Distance(SpawnPoint, target.Position) > 26f))
                        {
                            waitCounter++;
                            SpawnPoint = World.GetNextPositionOnStreet(target.Position.Around(20f));
                            GameFiber.Yield();
                            if (waitCounter >= 500)
                            {
                                SpawnPoint = target.Position.Around(20f);
                                break;
                            }
                        }
                        Vector3 directionFromVehicleToPed = (target.Position - SpawnPoint);
                        directionFromVehicleToPed.Normalize();

                        float vehicleHeading = MathHelper.ConvertDirectionToHeading(directionFromVehicleToPed);
                        vehicle.Heading = vehicleHeading + 180f;
                        vehicle.Position = SpawnPoint;

                        transportVanTeleported = true;

                        break;
                    }
                }

                forceCloseSpawn = true;
                //park the van
                Game.HideHelp();
                if (!getClose)
                {
                    while (((Vector3.Distance(target.Position, vehicle.Position) > 19f && (vehicle.Position.Z - target.Position.Z < -2.5f)) || (vehicle.Position.Z - target.Position.Z > 2.5f)) && !transportVanTeleported)
                    {
                        if (!target.Exists() || !target.IsValid())
                        {
                            return;
                        }

                        Rage.Task parkNearcar = driver.Tasks.DriveToPosition(target.Position, 6f, VehicleDrivingFlags.DriveAroundVehicles | VehicleDrivingFlags.DriveAroundObjects | VehicleDrivingFlags.AllowMedianCrossing | VehicleDrivingFlags.YieldToCrossingPedestrians);
                        parkNearcar.WaitForCompletion(900);

                        if (Vector3.Distance(target.Position, vehicle.Position) > 60f)
                        {
                            Vector3 SpawnPoint = World.GetNextPositionOnStreet(target.Position.Around(10f));

                            int waitCounter = 0;
                            while ((SpawnPoint.Z - target.Position.Z < -3f) || (SpawnPoint.Z - target.Position.Z > 3f) || (Vector3.Distance(SpawnPoint, target.Position) > 26f))
                            {
                                waitCounter++;
                                SpawnPoint = World.GetNextPositionOnStreet(target.Position.Around(20f));
                                GameFiber.Yield();
                                if (waitCounter >= 500)
                                {
                                    SpawnPoint = target.Position.Around(20f);
                                    break;
                                }
                            }
                            Vector3 directionFromVehicleToPed = (target.Position - SpawnPoint);
                            directionFromVehicleToPed.Normalize();

                            float vehicleHeading = MathHelper.ConvertDirectionToHeading(directionFromVehicleToPed);
                            vehicle.Heading = vehicleHeading + 180f;
                            vehicle.Position = SpawnPoint;

                            transportVanTeleported = true;
                        }
                    }
                }
                else
                {
                    while ((Vector3.Distance(target.Position, vehicle.Position) > 17f) || transportVanTeleported)
                    {
                        if (!target.Exists() || !target.IsValid())
                        {
                            return;
                        }
                        Rage.Task parkNearSuspect = driver.Tasks.DriveToPosition(target.Position, 6f, VehicleDrivingFlags.FollowTraffic | VehicleDrivingFlags.DriveAroundVehicles | VehicleDrivingFlags.DriveAroundObjects | VehicleDrivingFlags.AllowMedianCrossing | VehicleDrivingFlags.YieldToCrossingPedestrians);
                        parkNearSuspect.WaitForCompletion(800);
                        transportVanTeleported = false;
                        if (Vector3.Distance(target.Position, vehicle.Position) > 50f)
                        {
                            vehicle.Position = World.GetNextPositionOnStreet(target.Position.Around(12f));
                        }
                    }
                    GameFiber.Sleep(600);
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch
            {
                // If we don't catch everything we got, we will crash the plug-in
                // That is what analyzers would not understand
            }
#pragma warning restore CA1031 // Do not catch general exception types
        }
        public static void GetSpawnPoint(Vector3 StartPoint, out Vector3 SpawnPoint1, out float Heading1, bool UseSpecialID)
        {
            Vector3 tempspawn = World.GetNextPositionOnStreet(StartPoint.Around2D(EntryPoint.SceneManagementSpawnDistance + 5f));
#pragma warning disable S1854 // Unused assignments should be removed
            Vector3 SpawnPoint = Vector3.Zero;
#pragma warning restore S1854 // Unused assignments should be removed
            float Heading = 0;

            if (!UseSpecialID || !NativeFunction.Natives.GET_NTH_CLOSEST_VEHICLE_NODE_FAVOUR_DIRECTION<bool>(tempspawn.X, tempspawn.Y, tempspawn.Z, StartPoint.X, StartPoint.Y, StartPoint.Z, 0, out SpawnPoint, out Heading, 0, 0x40400000, 0) || !SpawnPoint.IsNodeSafe())
            {
                Game.LogTrivial("AM+: Unsuccessful specialID");
                SpawnPoint = World.GetNextPositionOnStreet(StartPoint.Around2D(EntryPoint.SceneManagementSpawnDistance + 5f));
                var spawnDirection = StartPoint - SpawnPoint;
                spawnDirection.Normalize();

                Heading = MathHelper.ConvertDirectionToHeading(spawnDirection);
            }
            SpawnPoint1 = SpawnPoint;
            Heading1 = Heading;
        }

        private static readonly TimerBarPool timerBarPool = new TimerBarPool();
        private static readonly BarTimerBar arrestBar = new BarTimerBar("Arresting...");
        private static bool arrestBarInPool;

        private static MenuPool _menuPool;
        private static UIMenu ActiveMenu = PedManagementMenu;
        internal static UIMenuListItem MenuSwitchListItem { get; private set; }
        public static void CreateMenus()
        {
            arrestBar.ForegroundColor = System.Drawing.Color.DarkBlue;
            arrestBar.BackgroundColor = ControlPaint.Dark(arrestBar.ForegroundColor);

            _menuPool = new MenuPool();
            List<dynamic> menus = new List<dynamic>() { "Ped Manager", "Vehicle Manager" };
            MenuSwitchListItem = new UIMenuListItem("Scene Management", "", menus);
            CreatePedManagementMenu();

            _menuPool.Add(PedManagementMenu);
            PedManagementMenu.OnListChange += OnListChange;
            CreateVehicleManagementMenu();
            _menuPool.Add(VehicleManagementMenu);
            VehicleManagementMenu.OnListChange += OnListChange;
            Game.FrameRender += Process;
            MainLogic();
        }
        public static void OnListChange(UIMenu sender, UIMenuListItem list, int index)
        {
            if ((sender != PedManagementMenu && sender != VehicleManagementMenu) || list != MenuSwitchListItem) { return; }

            string selectedmenustring = list.Collection[list.Index].ToString();

            UIMenu selectedmenu;
            if (selectedmenustring == "Ped Manager")
            {
                selectedmenu = PedManagementMenu;
            }
            else
            {
                selectedmenu = VehicleManagementMenu;
            }

            if (selectedmenu != sender)
            {
                sender.Visible = false;
                selectedmenu.Visible = true;
                ActiveMenu = selectedmenu;
                list.Selected = false;
            }
        }

        private static Ped nearestWaterPed;

        internal static bool CallCoronerTime { get; set; }
        private static void MainLogic()
        {
            GameFiber.StartNew(() =>
            {
                while (true)
                {
                    GameFiber.Yield();

                    if ((ExtensionMethods.IsKeyDownRightNowComputerCheck(EntryPoint.SceneManagementModifierKey) || (EntryPoint.SceneManagementModifierKey == Keys.None)) && ExtensionMethods.IsKeyDownComputerCheck(EntryPoint.SceneManagementKey))
                    {
                        if (ActiveMenu != null)
                        {
                            ActiveMenu.Visible = !ActiveMenu.Visible;
                        }
                        else
                        {
                            PedManagementMenu.Visible = !PedManagementMenu.Visible;
                        }
                    }

                    if (_menuPool.IsAnyMenuOpen())
                    {
                        NativeFunction.Natives.SET_PED_STEALTH_MOVEMENT(Game.LocalPlayer.Character, 0, 0);
                    }
                    else if ((ExtensionMethods.IsKeyDownRightNowComputerCheck(GrabPedModifierKey) || GrabPedModifierKey == Keys.None) && ExtensionMethods.IsKeyDownComputerCheck(GrabPedKey))
                    {
                        if (!IsGrabEnabled)
                        {
                            GrabPed();
                        }
                        else
                        {
                            IsGrabEnabled = false;
                        }
                    }

                    if (Game.LocalPlayer.Character.SubmersionLevel < 0.2 && (ExtensionMethods.IsKeyDownComputerCheck(TackleKey) || Game.IsControllerButtonDown(TackleButton)) && Game.LocalPlayer.Character.Speed >= 5.3f)
                    {
                        var nearestPed = GetNearestValidPed(2f, true, false, false, -1);
                        if (nearestPed && !Functions.IsPedArrested(nearestPed) && !Functions.IsPedGettingArrested(nearestPed))
                        {
                            Game.LocalPlayer.Character.IsRagdoll = true;
                            nearestPed.IsRagdoll = true;
                            GameFiber.Sleep(500);
                            Game.LocalPlayer.Character.IsRagdoll = false;
                            GameFiber.Wait(2000);
                            nearestPed.IsRagdoll = false;
                        }
                    }

                    if (CallCoronerTime)
                    {
                        Coroner.Main();
                        CallCoronerTime = false;
                    }
                }
            });
        }

        public static void Process(object sender, GraphicsEventArgs e)
        {
            _menuPool.ProcessMenus();
        }
    }
}
