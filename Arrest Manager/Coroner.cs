using Albo1125.Common.CommonLibrary;
using LSPD_First_Response.Mod.API;
using Rage;
using Rage.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Media;
using System.Windows.Forms;

namespace Arrest_Manager
{

    internal class Coroner
    {
        internal static Model CoronerVehicleModel { get; set; } = new Model("SPEEDO");
        internal static Model CoronerModel { get; set; } = new Model("S_M_M_Doctor_01");

        private static readonly SoundPlayer cameraSound = new SoundPlayer("LSPDFR/audio/scanner/Arrest Manager Audio/Camera.wav");       

        private static readonly List<Ped> bodiesBeingHandled = new List<Ped>();

        private readonly List<Ped> deadBodies;
        private Vehicle coronerVeh;
        private Ped driver;
        private Ped passenger;
        private Vector3 destination;
        private readonly bool anims;
        private readonly List<Rage.Object> bodyBags = new List<Rage.Object>();

        public static bool CanBeCalled(Vector3 destination)
        {
            return GetNearbyDeadPeds(destination).Count != 0;
        }

        public static bool CanBeCalled()
        {
            return CanBeCalled(Game.LocalPlayer.Character.Position);
        }

#pragma warning disable S4210 // Windows Forms entry points should be marked with STAThread
        public static void Main()
#pragma warning restore S4210 // Windows Forms entry points should be marked with STAThread
        {
            if (GetNearbyDeadPeds(Game.LocalPlayer.Character.Position).Count == 0) { Game.DisplaySubtitle("No nearby dead people were found, sorry!"); return; }
            new Coroner(Game.LocalPlayer.Character.Position).InitCoronerThread();
        }

        public static void CallFromSmartRadio()
        {
            if (GetNearbyDeadPeds(Game.LocalPlayer.Character.Position).Count == 0) { Game.DisplaySubtitle("No nearby dead people were found, sorry!"); return; }
            new Coroner(Game.LocalPlayer.Character.Position, false).InitCoronerThread();
        }

        public Coroner(Vector3 destination, bool anims = true)
        {
            this.destination = destination;
            this.deadBodies = GetNearbyDeadPeds(destination);
            this.anims = anims;
        }

        public void InitCoronerThread()
        {
            GameFiber.StartNew(() =>
            {
                try
                {
                    float Heading;
                    bool UseSpecialID = true;
                    Vector3 SpawnPoint;
                    float travelDistance;
                    int waitCount = 0;
                    while (true)
                    {
                        SceneManager.GetSpawnPoint(destination, out SpawnPoint, out Heading, UseSpecialID);
                        travelDistance = NativeFunction.Natives.CALCULATE_TRAVEL_DISTANCE_BETWEEN_POINTS<float>(SpawnPoint.X, SpawnPoint.Y, SpawnPoint.Z, destination.X, destination.Y, destination.Z);
                        waitCount++;
                        if (Vector3.Distance(destination, SpawnPoint) > EntryPoint.SceneManagementSpawnDistance - 15f && travelDistance < (EntryPoint.SceneManagementSpawnDistance * 4.5f))
                        {
                            Vector3 directionFromVehicleToPed1 = destination - SpawnPoint;
                            directionFromVehicleToPed1.Normalize();

                            float HeadingToPlayer = MathHelper.ConvertDirectionToHeading(directionFromVehicleToPed1);

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
                            Game.DisplayNotification("Press ~b~Y ~s~to force a spawn in the ~g~wilderness.");
                        }
                        if ((waitCount >= 600) && ExtensionMethods.IsKeyDownComputerCheck(Keys.Y))
                        {
                            SpawnPoint = destination.Around(15f);
                            break;
                        }
                        GameFiber.Yield();
                    }
                    coronerVeh = new Vehicle(CoronerVehicleModel, SpawnPoint, Heading)
                    {
                        IsPersistent = true
                    };
                    if (coronerVeh.HasSiren)
                    {
                        coronerVeh.IsSirenOn = true;
                    }
                    var coronerBlip = coronerVeh.AttachBlip();
                    coronerBlip.Color = System.Drawing.Color.Black;
                    coronerBlip.Scale = 0.80f;
                    coronerBlip.Sprite = BlipSprite.Friend;
                    coronerBlip.Flash(1000, 30000);
                    driver = new Ped(CoronerModel, Vector3.Zero, 0);
                    driver.MakeMissionPed();
                    driver.IsInvincible = true;
                    driver.WarpIntoVehicle(coronerVeh, -1);
                    Functions.SetPedCantBeArrestedByPlayer(driver, true);

                    passenger = new Ped(CoronerModel, Vector3.Zero, 0);
                    passenger.MakeMissionPed();
                    passenger.IsInvincible = true;
                    passenger.WarpIntoVehicle(coronerVeh, 0);
                    Functions.SetPedCantBeArrestedByPlayer(passenger, true);
                    Functions.SetPedCanBePulledOver(driver, false);
                    Game.DisplayNotification("~b~Dispatch~w~: Requesting a coroner team to " + World.GetStreetName(World.GetStreetHash(Game.LocalPlayer.Character.Position)));
                    if (anims)
                    {
                        Functions.PlayPlayerRadioAction(Functions.GetPlayerRadioAction(), 3000);
                        GameFiber.Wait(1000);
                        SceneManager.BleepPlayer.Play();
                    }
                    TaskCoronerDriveToPosition(driver, coronerVeh, destination);
                    coronerBlip.Delete();

                    while (deadBodies.Count > 0)
                    {
                        foreach (Ped body in deadBodies.OrderBy(x => x.DistanceTo(driver.Position)))
                        {
                            if (body.Exists() && !bodiesBeingHandled.Contains(body))
                            {
                                DealWithBody(body);
                            }
                            else
                            {
                                deadBodies.Remove(body);
                            }
                        }
                        deadBodies.AddRange(GetNearbyDeadPeds(driver.Position));
                    }
                    LeaveScene();
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception e)
                {
                    Game.LogTrivial(e.ToString());
                    if (driver.Exists()) { driver.Delete(); }
                    if (passenger.Exists()) { passenger.Delete(); }
                    if (coronerVeh.Exists()) { coronerVeh.Delete(); }
                    foreach (Entity ent in deadBodies)
                    {
                        if (ent.Exists()) { ent.Delete(); }
                    }
                    deadBodies.Clear();
                    foreach (Entity ent in bodyBags)
                    {
                        if (ent.Exists()) { ent.Delete(); }
                    }
                }
#pragma warning restore CA1031 // Do not catch general exception types
            });
        }

        private void LeaveScene()
        {
            GameFiber.Wait(2500);
            foreach (Rage.Object obj in bodyBags)
            {
                if (obj.Exists())
                {
                    obj.Delete();
                }
            }
            GameFiber.Wait(2500);
            int randomRoll = EntryPoint.SharedRandomInstance.Next(1, 23);

            string msg = "";
            switch (randomRoll)
            {
                case 1:
                    msg = "All done here - I wonder if FinKone'll ever touch some code again.";
                    break;
                case 2:
                    msg = "Let's roll, we've got another call. Los Santos never stops!";
                    break;
                case 3:
                    msg = "This is not nearly as bad as the last call, the poor guy was stuck in a toilet.";
                    break;
                case 4:
                    msg = "I love the vanilla experience with this toolkit. Bejoljo's toolkit is just gone too much.";
                    break;
                case 5:
                    msg = "Albo1125 shouldn't promote himself like this. It's disgusting...";
                    break;
                case 6:
                    msg = "All I need now is a holiday to the PNW parks.";
                    break;
                case 7:
                    msg = "It's a bloody shame this had to happen.";
                    break;
                case 8:
                    msg = "I'm feeling hungry now. Let's get a bite to eat.";
                    break;
                case 9:
                    msg = "It is so sad that Albo1125 will now only work for role-plays for nonsense.";
                    break;
                case 10:
                    msg = "Can you believe you aren't using Albo1125's Arrest Manager?";
                    break;
                case 11:
                    msg = "I'm done once they have these young buddies with those fancy EUP clothes appears on this job.";
                    break;
                case 12:
                    msg = "It's back to watching San Andreas's CCTV stream now, then.";
                    break;
                case 13:
                    msg = "Bejoljo's server goes down again. Why he implemented that dumb update system?";
                    break;
                case 14:
                    msg = "I like how people remained calm there. I wonder how they learned to stop shitting bricks...";
                    break;
                case 15:
                    msg = "With these budget cuts my only contact method will soon be LSCoroner@Idontcare.com...";
                    break;
                case 16:
                    msg = "Heard about the new glasses they're selling? Apparently they make visuals great again.";
                    break;
                case 17:
                    msg = "I hope these medics don't get any better or we'll be out of a job!";
                    break;
                case 18:
                    msg = "These new emergency lights the police are using are so damn bright.";
                    break;
                case 19:
                    msg = "Could've been worse. I got a call in the ocean once, had to swim a mile!";
                    break;
                case 20:
                    msg = "My stupidest call was the rednecks who blew themselves up fishing with grenades.";
                    break;
                case 21:
                    msg = "This still doesn't top the guy who somehow electrocuted himself with a toaster.";
                    break;
                case 22:
                    msg = "Can you believe the coast guard dispatched me once for a dead whale? It doesn't even fit!";
                    break;
            }

            if (driver.Exists() && Vector3.Distance(driver.Position, Game.LocalPlayer.Character.Position) < 60f)
            {
                Game.DisplaySubtitle("~b~Driver~w~: " + msg, 7000);
            }

            passenger.Tasks.FollowNavigationMeshToPosition(coronerVeh.GetOffsetPositionRight(2), coronerVeh.Heading, 1.7f);
            driver.Tasks.FollowNavigationMeshToPosition(coronerVeh.GetOffsetPositionRight(-2), coronerVeh.Heading, 1.7f).WaitForCompletion(8000);
            passenger.Tasks.EnterVehicle(coronerVeh, 7000, 0);
            driver.Tasks.EnterVehicle(coronerVeh, 7000, -1).WaitForCompletion();
            GameFiber.Wait(3000);
            driver.Tasks.CruiseWithVehicle(coronerVeh, 15.0f, VehicleDrivingFlags.DriveAroundVehicles | VehicleDrivingFlags.DriveAroundObjects | VehicleDrivingFlags.AllowMedianCrossing | VehicleDrivingFlags.YieldToCrossingPedestrians);


            driver.Dismiss();
            coronerVeh.Dismiss();
        }

        private void DealWithBody(Ped body)
        {
            bodiesBeingHandled.Add(body);
            passenger.Tasks.GoToOffsetFromEntity(body, 10000, -2.0f, -1.0f, 8.0f);
            driver.Tasks.GoToOffsetFromEntity(body, 10000, 2.4f, 1.0f, 8.0f).WaitForCompletion();
            if (Vector3.Distance(driver.Position, Game.LocalPlayer.Character.Position) < 60f)
            {
                Rage.Object camera = new Rage.Object("prop_ing_camera_01", driver.GetOffsetPosition(Vector3.RelativeTop * 30));
                driver.Tasks.PlayAnimation("anim@mp_player_intupperphotography", "idle_a_fp", 8.0F, AnimationFlags.None);
                camera.Heading = driver.Heading - 180;
                camera.Position = driver.GetOffsetPosition(Vector3.RelativeTop * 0.68f + Vector3.RelativeFront * 0.33f);
                camera.IsPositionFrozen = true;

                Vector3 dirVect = body.Position - driver.Position;
                dirVect.Normalize();

                GameFiber.Wait(900);
                NativeFunction.Natives.DRAW_SPOT_LIGHT(driver.GetOffsetPosition(Vector3.RelativeFront * 0.5f).X, driver.GetOffsetPosition(Vector3.RelativeFront * 0.5f).Y,
                    driver.GetOffsetPosition(Vector3.RelativeFront * 0.5f).Z, dirVect.X, dirVect.Y, dirVect.Z, 100, 100, 100, 90.0f, 50.0f, 90.0f, 80.0f, 90.0f);
                cameraSound.Play();
                GameFiber.Wait(1500);
                NativeFunction.Natives.DRAW_SPOT_LIGHT(driver.GetOffsetPosition(Vector3.RelativeFront * 0.5f).X, driver.GetOffsetPosition(Vector3.RelativeFront * 0.5f).Y,
                    driver.GetOffsetPosition(Vector3.RelativeFront * 0.5f).Z, dirVect.X, dirVect.Y, dirVect.Z, 100, 100, 100, 90.0f, 50.0f, 90.0f, 80.0f, 90.0f);
                cameraSound.Play();
                GameFiber.Wait(1500);
                NativeFunction.Natives.DRAW_SPOT_LIGHT(driver.GetOffsetPosition(Vector3.RelativeFront * 0.5f).X, driver.GetOffsetPosition(Vector3.RelativeFront * 0.5f).Y,
                    driver.GetOffsetPosition(Vector3.RelativeFront * 0.5f).Z, dirVect.X, dirVect.Y, dirVect.Z, 100, 100, 100, 90.0f, 50.0f, 90.0f, 80.0f, 90.0f);
                cameraSound.Play();

                GameFiber.Wait(1000);
                camera.Delete();
                Game.DisplaySubtitle("~b~Driver~w~: I've got enough pictures, I'll time stamp them.", 4000);

                passenger.Tasks.PlayAnimation("amb@medic@standing@tendtodead@enter", "enter", 8.0F, AnimationFlags.None);
                GameFiber.Wait(1000);
                passenger.Tasks.PlayAnimation("amb@medic@standing@tendtodead@base", "base", 8.0F, AnimationFlags.None);
                GameFiber.Wait(1000);
                passenger.Tasks.PlayAnimation("amb@medic@standing@tendtodead@exit", "exit", 8.0F, AnimationFlags.None).WaitForCompletion();
                GameFiber.Wait(1000);
            }


            Game.DisplaySubtitle("~b~Passenger~w~: " + GetCauseOfDeathPrelude() + GetCauseOfDeathString(body) + "~b~.", 6000);
            if (body.Exists())
            {
                if (deadBodies.Contains(body))
                {
                    deadBodies.Remove(body);
                }

                if (bodiesBeingHandled.Contains(body))
                {
                    bodiesBeingHandled.Remove(body);
                }

                if (!body.IsInAnyVehicle(true))
                {
                    bodyBags.Add(new Rage.Object("xm_prop_body_bag", body.Position)
                    {
                        IsPositionFrozen = false,
                    });

                }
                if (body.Exists())
                {
                    body.Delete();
                }
            }
            GameFiber.Wait(2500);
            
        }

        private static string GetCauseOfDeathPrelude()
        {
            switch (EntryPoint.SharedRandomInstance.Next(3))
            {
                case 0:
                    return "It seems like this one died from ~r~";
                case 1:
                    return "Seems the cause of death on this one was ~r~";
                default:
                    return "This one appears to have died from ~r~";
            }
        }

        private static string GetCauseOfDeathString(Ped body)
        {
            var causeModel = NativeFunction.Natives.GET_PED_CAUSE_OF_DEATH<Model>(body);
            var cause = EntryPoint.IsLSPDFRPluginRunning("BetterEMS", new Version("3.0.0.0")) && API.BetterEmsFunctions.HasBeenTreated(body) ? API.BetterEmsFunctions.GetOriginalDeathWeaponAssetHash(body) : causeModel.Hash;
            if (causeModel.IsVehicle || cause == 0x07FC7D7A || cause == 0xA36D413E)
            {
                return "a collision with a vehicle";
            }
            if (cause == 0xA2719263)
            {
                return "a fist fight";
            }
            else if (cause == 0xF9FBAEBE)
            {
                return "an animal's bite";
            }
            else if (cause == 0x99B507EA)
            {
                return "a knife stab wound";
            }
            else if (cause == 0xCDC174B0)
            {
                return "a high fall";
            }
            else if (cause == 0xDF8E89EB)
            {
                return "a fire";
            }
            else if (cause == 0x2024F4E8)
            {
                return "an explosion";
            }
            else if (cause == 0x8B7333FB)
            {
                return "a wound bleeding out";
            }
            else
            {
                return "a weapon";
            }
        }

        private static List<Ped> GetNearbyDeadPeds(Vector3 pos, float radius = 35)
        {
            List<Ped> nearbyDeads = new List<Ped>();
            foreach (Ped ped in World.EnumeratePeds())
            {
                if (ped.Exists() && ped.IsDead && !bodiesBeingHandled.Contains(ped) && ped.DistanceTo(pos) < radius)
                {
                    nearbyDeads.Add(ped);
                }
            }
            return nearbyDeads;
        }

        private static void TaskCoronerDriveToPosition(Ped driver, Vehicle veh, Vector3 pos)
        {

            Ped playerPed = Game.LocalPlayer.Character;
            int drivingLoopCount = 0;
            bool transportVanTeleported = false;
            int waitCount = 0;
            bool forceCloseSpawn = false;

            GameFiber.StartNew(delegate
            {
                while (!forceCloseSpawn)
                {
                    GameFiber.Yield();
                    if (Albo1125.Common.CommonLibrary.ExtensionMethods.IsKeyDownComputerCheck(EntryPoint.SceneManagementKey)) // || Albo1125.Common.CommonLibrary.ExtensionMethods.IsKeyDownComputerCheck(multiTransportKey))
                    {
                        GameFiber.Sleep(500);
                        if (Albo1125.Common.CommonLibrary.ExtensionMethods.IsKeyDownRightNowComputerCheck(EntryPoint.SceneManagementKey))// || Albo1125.Common.CommonLibrary.ExtensionMethods.IsKeyDownRightNowComputerCheck(multiTransportKey))
                        {
                            GameFiber.Sleep(500);
                            if (Albo1125.Common.CommonLibrary.ExtensionMethods.IsKeyDownRightNowComputerCheck(EntryPoint.SceneManagementKey))// || Albo1125.Common.CommonLibrary.ExtensionMethods.IsKeyDownRightNowComputerCheck(multiTransportKey))
                            {
                                forceCloseSpawn = true;
                            }
                            else
                            {
                                Game.DisplayNotification("Hold down the ~b~" + Albo1125.Common.CommonLibrary.ExtensionMethods.GetKeyString(EntryPoint.SceneManagementKey, Keys.None) + " ~s~to force a close spawn.");
                            }
                        }
                    }
                }
            });
            Task driveToPed = null;
            driver.Tasks.PerformDrivingManeuver(VehicleManeuver.GoForwardStraight).WaitForCompletion(500);
            while (Vector3.Distance(veh.Position, pos) > 35f)
            {

                veh.Repair();
                if (driveToPed == null || !driveToPed.IsActive)
                {
                    driveToPed = driver.Tasks.DriveToPosition(pos, MathHelper.ConvertKilometersPerHourToMetersPerSecond(60f), VehicleDrivingFlags.DriveAroundVehicles | VehicleDrivingFlags.DriveAroundObjects | VehicleDrivingFlags.AllowMedianCrossing | VehicleDrivingFlags.YieldToCrossingPedestrians);
                }
                NativeFunction.Natives.SET_DRIVE_TASK_DRIVING_STYLE(driver, 786607);
                NativeFunction.Natives.SET_DRIVER_AGGRESSIVENESS(driver, 0f);
                NativeFunction.Natives.SET_DRIVER_ABILITY(driver, 1f);
                GameFiber.Wait(600);
                

                waitCount++;
                if (waitCount == 70)
                {
                    Game.DisplayHelp("Service taking too long? Hold down ~b~" + EntryPoint.KeyConvert.ConvertToString(EntryPoint.SceneManagementKey) + " ~s~to speed it up.", 5000);
                }

                if (veh.Speed < 2f)
                {
                    drivingLoopCount++;
                }
                //if van is very far away
                if (Vector3.Distance(pos, veh.Position) > EntryPoint.SceneManagementSpawnDistance + 70f)
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
                    int WaitCount = 0;
                    while (true)
                    {
                        SceneManager.GetSpawnPoint(pos, out SpawnPoint, out Heading, UseSpecialID);
                        travelDistance = NativeFunction.Natives.CALCULATE_TRAVEL_DISTANCE_BETWEEN_POINTS<float>(SpawnPoint.X, SpawnPoint.Y, SpawnPoint.Z, playerPed.Position.X, playerPed.Position.Y, playerPed.Position.Z);

                        if (Vector3.Distance(playerPed.Position, SpawnPoint) > EntryPoint.SceneManagementSpawnDistance - 15f && travelDistance < EntryPoint.SceneManagementSpawnDistance * 4.5f)
                        {
                            Vector3 directionFromVehicleToPed1 = (Game.LocalPlayer.Character.Position - SpawnPoint);
                            directionFromVehicleToPed1.Normalize();

                            float HeadingToPlayer = MathHelper.ConvertDirectionToHeading(directionFromVehicleToPed1);

                            if (Math.Abs(MathHelper.NormalizeHeading(Heading) - MathHelper.NormalizeHeading(HeadingToPlayer)) < 150f)
                            {

                                break;
                            }
                        }
                        WaitCount++;
                        if (WaitCount >= 400)
                        {
                            UseSpecialID = false;
                        }

                        GameFiber.Yield();
                    }

                    Game.Console.Print("Relocating because van was stuck...");
                    veh.Position = SpawnPoint;
                    veh.Heading = Heading;
                    drivingLoopCount = 39;
                    Game.DisplayHelp("Service taking too long? Hold down ~b~" + EntryPoint.KeyConvert.ConvertToString(EntryPoint.SceneManagementKey) + " ~s~to speed it up.", 5000);
                }
                // if van is stuck for a 2nd time or takes too long, spawn it very near to the suspect
                else if (((drivingLoopCount >= 70 || waitCount >= 110) && EntryPoint.AllowWarping) || forceCloseSpawn)
                {
                    Game.Console.Print("Relocating to a close position");

                    Vector3 SpawnPoint = World.GetNextPositionOnStreet(pos.Around2D(15f));

                    int waitCounter = 0;
                    while ((SpawnPoint.Z - pos.Z < -3f) || (SpawnPoint.Z - pos.Z > 3f) || (Vector3.Distance(SpawnPoint, pos) > 25f))
                    {
                        waitCounter++;
                        SpawnPoint = World.GetNextPositionOnStreet(pos.Around2D(15f));
                        GameFiber.Yield();
                        if (waitCounter >= 500)
                        {
                            SpawnPoint = pos.Around2D(15f);
                            break;
                        }
                    }
                    veh.Position = SpawnPoint;
                    Vector3 directionFromVehicleToPed = (pos - SpawnPoint);
                    directionFromVehicleToPed.Normalize();

                    float vehicleHeading = MathHelper.ConvertDirectionToHeading(directionFromVehicleToPed);
                    veh.Heading = vehicleHeading;
                    transportVanTeleported = true;

                    break;
                }
            }

            forceCloseSpawn = true;
            //park the van
            Game.HideHelp();
            while ((Vector3.Distance(pos, veh.Position) > 18f) && !transportVanTeleported)
            {
                var parkNearSuspect = driver.Tasks.DriveToPosition(pos, 6f, VehicleDrivingFlags.FollowTraffic | VehicleDrivingFlags.DriveAroundVehicles | VehicleDrivingFlags.DriveAroundObjects | VehicleDrivingFlags.AllowMedianCrossing | VehicleDrivingFlags.YieldToCrossingPedestrians);
                parkNearSuspect.WaitForCompletion(800);
                transportVanTeleported = false;
                if (Vector3.Distance(pos, veh.Position) > 80f)
                {
                    veh.Position = World.GetNextPositionOnStreet(pos.Around2D(12f));
                }

            }
            GameFiber.Wait(600);
        }
    }
}
