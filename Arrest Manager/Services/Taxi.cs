using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using LSPD_First_Response.Mod.API;
using Rage;
using Rage.Native;
using static Arrest_Manager.SceneManager;

namespace Arrest_Manager.Services
{
    internal class Taxi
    {
        private Vehicle taxi;
        private Ped taxidriver;
        private Ped pedtobepickedup;
        private static readonly List<Ped> pedsBeingPickedUp = new List<Ped>();

        public void CallTaxi()
        {
            GameFiber.StartNew(() =>
            {
                try
                {
                    pedtobepickedup = PedManager.GetNearestValidPed();
                    if (!pedtobepickedup) return;

                    if (Functions.IsPedArrested(pedtobepickedup)) 
                    {
                        return;
                    }

                    if (pedtobepickedup.IsInAnyVehicle(false)) 
                    {
                        Game.DisplayHelp("You cannot call taxi for a ped in a vehicle.");
                        return;
                    }

                    if (pedsBeingPickedUp.Contains(pedtobepickedup)) { Game.DisplayHelp("Taxi is already assigned to this suspect."); return; }
                    ToggleMobilePhone(Game.LocalPlayer.Character, true);
                    pedsBeingPickedUp.Add(pedtobepickedup);
                    pedtobepickedup.IsPersistent = true;
                    pedtobepickedup.BlockPermanentEvents = true;
                    pedtobepickedup.Tasks.StandStill(-1);
                    Functions.SetPedCantBeArrestedByPlayer(pedtobepickedup, true);
                    if (EntryPoint.IsLSPDFRPlusRunning)
                    {
                        API.LspdfrPlusFunctions.AddCountToStatistic(Main.PluginName, "Taxis called");
                    }
                    float Heading;
                    bool UseSpecialID = true;
                    Vector3 SpawnPoint;
                    float travelDistance;
                    int waitCount = 0;
                    while (true)
                    {
                        GetSpawnPoint(pedtobepickedup.Position, out SpawnPoint, out Heading, UseSpecialID);
                        travelDistance = NativeFunction.Natives.CALCULATE_TRAVEL_DISTANCE_BETWEEN_POINTS<float>(SpawnPoint.X, SpawnPoint.Y, SpawnPoint.Z, pedtobepickedup.Position.X, pedtobepickedup.Position.Y, pedtobepickedup.Position.Z);
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
                    TaskDriveToEntity(taxidriver, taxi, pedtobepickedup, true);
                    NativeFunction.Natives.START_VEHICLE_HORN(taxi, 5000, 0, true);
                    if (taxi.Speed > 15f)
                    {
                        NativeFunction.Natives.SET_VEHICLE_FORWARD_SPEED(taxi, 15f);
                    }

                    taxidriver.Tasks.PerformDrivingManeuver(VehicleManeuver.GoForwardStraightBraking);
                    GameFiber.Sleep(600);
                    taxidriver.Tasks.PerformDrivingManeuver(VehicleManeuver.Wait);
                    if (taxiblip.Exists()) { taxiblip.Delete(); }
#pragma warning disable S2696 // Instance members should not write to "static" fields
                    if (PedManager.FollowingPed == pedtobepickedup) PedManager.IsFollowingEnabled = false;
#pragma warning restore S2696 // Instance members should not write to "static" fields
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
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception e)
                {
                    Game.LogTrivial(e.ToString());
                    Game.DisplayNotification("The taxi pickup service was interrupted");
                    if (taxi.Exists())
                    {
                        taxi.Delete();
                    }
                    if (taxidriver.Exists()) { taxidriver.Delete(); }
                    if (pedtobepickedup.Exists()) { pedtobepickedup.Delete(); }
                }
#pragma warning restore CA1031 // Do not catch general exception types
            });
        }
    }
}
