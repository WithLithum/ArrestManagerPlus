using System;
using System.Windows.Forms;
using Albo1125.Common.CommonLibrary;
using LemonUI;
using LemonUI.Menus;
using LSPD_First_Response.Mod.API;
using Rage;
using Rage.Native;
using static Arrest_Manager.PedManager;
using static Arrest_Manager.VehicleManager;

namespace Arrest_Manager
{
    internal static class SceneManager
    {
        public static readonly System.Media.SoundPlayer BleepPlayer = new System.Media.SoundPlayer("LSPDFR/audio/scanner/Arrest Manager Audio/RADIO_BLIP.wav");
        private static Rage.Object MobilePhone;
        internal static NativeMenu ManagementMenu { get; private set; }

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

                if (MobilePhone)
                {
                    MobilePhone.Delete();
                }
            }
        }

        public static void TaskDriveToEntity(Ped driver, Vehicle vehicle, Entity target, bool getClose)
        {
            var drivingLoopCount = 0;
            var transportVanTeleported = false;
            var waitCount = 0;
            var forceCloseSpawn = false;

            // Get close to player with various checks
            try
            {
                _ = GameFiber.StartNew(() =>
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

                    // Manipulate some driving stuff
                    NativeFunction.Natives.SET_DRIVE_TASK_DRIVING_STYLE(driver, 786607);
                    NativeFunction.Natives.SET_DRIVER_AGGRESSIVENESS(driver, 0f);
                    NativeFunction.Natives.SET_DRIVER_ABILITY(driver, 1f);

                    GameFiber.Wait(600);
                    waitCount++;
                    if (waitCount == 55)
                    {
                        Game.DisplayHelp("Service taking too long? Hold down ~b~" + EntryPoint.KeyConvert.ConvertToString(EntryPoint.SceneManagementKey) + " ~s~to speed it up.", 5000);
                    }

                    // If van isn't moving
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
                        Vector3 sp;
                        float head;
                        bool specId = true;
                        float travelDistance;
                        int wC = 0;
                        while (true)
                        {
                            GetSpawnPoint(target.Position, out sp, out head, specId);
                            travelDistance = Rage.Native.NativeFunction.Natives.CALCULATE_TRAVEL_DISTANCE_BETWEEN_POINTS<float>(sp.X, sp.Y, sp.Z, target.Position.X, target.Position.Y, target.Position.Z);
                            wC++;
                            if (Vector3.Distance(target.Position, sp) > EntryPoint.SceneManagementSpawnDistance - 15f && travelDistance < (EntryPoint.SceneManagementSpawnDistance * 4.5f))
                            {
                                var spawnDirection = (target.Position - sp);
                                spawnDirection.Normalize();

                                var headingToPlayer = MathHelper.ConvertDirectionToHeading(spawnDirection);

                                if (Math.Abs(MathHelper.NormalizeHeading(head) - MathHelper.NormalizeHeading(headingToPlayer)) < 150f)
                                {
                                    break;
                                }
                            }
                            if (wC >= 400)
                            {
                                specId = false;
                            }
                            GameFiber.Yield();
                        }

                        Game.Console.Print("Relocating because service was stuck...");
                        vehicle.Position = sp;

                        vehicle.Heading = head;
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
                        if (!target)
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

                            var directionFromVehicleToPed = (target.Position - SpawnPoint);
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

                        var parkNearSuspect = driver.Tasks.DriveToPosition(target.Position, 6f, VehicleDrivingFlags.FollowTraffic | VehicleDrivingFlags.DriveAroundVehicles | VehicleDrivingFlags.DriveAroundObjects | VehicleDrivingFlags.AllowMedianCrossing | VehicleDrivingFlags.YieldToCrossingPedestrians);
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
            catch
            {
                // If we don't catch everything we got, we will crash the plug-in
                // That is what analyzers would not understand
            }
        }

        public static void GetSpawnPoint(Vector3 start, out Vector3 spawn, out float heading, bool useSpecialId)
        {
            Vector3 tempspawn = World.GetNextPositionOnStreet(start.Around2D(EntryPoint.SceneManagementSpawnDistance + 5f));
            var sp = Vector3.Zero;
            var head = 0f;

            if (!useSpecialId || !NativeFunction.Natives.GET_NTH_CLOSEST_VEHICLE_NODE_FAVOUR_DIRECTION<bool>(tempspawn.X, tempspawn.Y, tempspawn.Z, start.X, start.Y, start.Z, 0, out sp, out head, 0, 0x40400000, 0) || !sp.IsNodeSafe())
            {
                Game.LogTrivial("AM+: Unsuccessful specialID");
                sp = World.GetNextPositionOnStreet(start.Around2D(EntryPoint.SceneManagementSpawnDistance + 5f));
                var spawnDirection = start - sp;
                spawnDirection.Normalize();

                head = MathHelper.ConvertDirectionToHeading(spawnDirection);
            }
            spawn = sp;
            heading = head;
        }

        private static ObjectPool _menuPool;

        public static void CreateMenus()
        {
            _menuPool = new ObjectPool();
            ManagementMenu = new NativeMenu("ArrestManager+", "Scene Manager");

            CreatePedManagementMenu();
            CreateVehicleManagementMenu();
            Game.FrameRender += Process;

            _menuPool.Add(ManagementMenu);
            MainLogic();
        }

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
                        ManagementMenu.Visible = !ManagementMenu.Visible;
                    }

                    if (_menuPool.AreAnyVisible)
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
            _menuPool.Process();
        }
    }
}
