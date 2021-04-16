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
using RelaperCommons.FirstResponse;

namespace Arrest_Manager
{
    internal class VehicleManager
    {
        internal static Model TowtruckModel { get; set; } = "TOWTRUCK";
        internal static Model FlatbedModel { get; set; } = "FLATBED";
        internal static bool AlwaysFlatbed { get; set; }
        internal static Vector3 FlatbedModifier { get; set; } = new Vector3(-0.5f, -5.75f, 1.005f);
        internal static System.Drawing.Color TowTruckColor { get; set; }
        internal static bool OverrideTowTruckColour { get; set; }
        internal static bool RecruitNearbyTowTrucks { get; set; }
        internal static Random SharedRandom { get; } = new Random();
        private Blip towblip;
        private Blip carblip;
        private Vehicle towTruck;
        private Ped driver;
        private Vehicle car;
        private static List<Vehicle> TowTrucksBeingUsed = new List<Vehicle>();
        private string modelName;

        internal bool RecruitNearbyTowtruck(out Ped TowDriver, out Vehicle TowTruck)
        {
            if (RecruitNearbyTowTrucks)
            {
                Entity[] nearbypeds = World.GetEntities(Game.LocalPlayer.Character.Position, EntryPoint.SceneManagementSpawnDistance * 0.75f, GetEntitiesFlags.ConsiderHumanPeds | GetEntitiesFlags.ExcludePlayerPed);
                nearbypeds = (from x in nearbypeds orderby (Game.LocalPlayer.Character.DistanceTo(x.Position)) select x).ToArray();
                foreach (Entity nearent in nearbypeds)
                {
                    if (nearent.Exists())
                    {
                        Ped nearped = (Ped)nearent;
                        if (nearped.IsInAnyVehicle(false) && nearped.CurrentVehicle.HasTowArm && !nearped.CurrentVehicle.TowedVehicle.Exists() && !TowTrucksBeingUsed.Contains(nearped.CurrentVehicle))
                        {
                            TowDriver = nearped;
                            TowDriver.MakeMissionPed();
                            TowTruck = TowDriver.CurrentVehicle;
                            TowTruck.IsPersistent = true;
                            return true;
                        }
                    }
                }
            }
            TowDriver = null;
            TowTruck = null;
            return false;
        }
        internal static void SmartRadioTow()
        {
            new VehicleManager().TowVehicle(false);
        }

        internal void TowVehicle(bool playanims = true)
        {
            Vehicle[] nearbyvehs = Game.LocalPlayer.Character.GetNearbyVehicles(2);
            if (nearbyvehs.Length == 0)
            {
                Game.DisplayHelp("There was no vehicle to tow.");
                return;
            }
            var towingCar = nearbyvehs[0];
            if (Vector3.Distance(Game.LocalPlayer.Character.Position, towingCar.Position) > 6f)
            {
                Game.DisplayHelp("Nearest vehicle is too far away. Get closer.");
                return;
            }
            if (towingCar.HasOccupants)
            {
                if (nearbyvehs.Length == 2)
                {
                    towingCar = nearbyvehs[1];
                    if (towingCar.HasOccupants)
                    {
                        Game.DisplayHelp("Remove all occupants from vehicle and try again.");
                        return;
                    }
                }
                else
                {
                    Game.DisplayHelp("Remove all occupants from vehicle and try again.");
                    return;
                }

            }
            
            if (towingCar.Model.IsHelicopter)
            {
                // Callouts+ Towing service :)
                RadioUtil.DisplayRadioQuote("Dispatch", "WE CAN'T TOW A HELICOPTER!");
            }

            if (!towingCar.Model.IsCar && !towingCar.Model.IsBike && !towingCar.Model.IsQuadBike)
            {
                Game.DisplayHelp("Only cars and bikes can be towed.");
                return;
            }

            TowVehicle(towingCar, playanims);
        }

        internal void TowVehicle(Vehicle car, bool playanims = true)
        {

            _ = GameFiber.StartNew(delegate
              {
                  if (!car.Exists()) { return; }
                  try
                  {
                      bool flatbed = true;
                      if (car.HasOccupants)
                      {

                          Game.DisplayNotification("Vehicle has occupants. Aborting tow.");
                          return;

                      }
                      if (car.IsPoliceVehicle)
                      {
                          uint noti = Game.DisplayNotification("Are you sure you want to tow the police vehicle? ~h~~b~Y/N");
                          while (true)
                          {
                              GameFiber.Yield();
                              if (ExtensionMethods.IsKeyDownComputerCheck(System.Windows.Forms.Keys.Y))
                              {
                                  Game.RemoveNotification(noti);
                                  break;
                              }
                              if (ExtensionMethods.IsKeyDownComputerCheck(System.Windows.Forms.Keys.N))
                              {
                                  Game.RemoveNotification(noti);
                                  return;
                              }
                          }
                          if (!car.Exists()) { return; }
                      }

                      if (car.Model.IsHelicopter)
                      {
                          RadioUtil.DisplayRadioQuote("Dispatch", "WE CAN'T TOW A HELICOPTER!");
                      }

                      if (!car.Model.IsCar && !car.Model.IsBike && !car.Model.IsQuadBike && !car.Model.IsBoat && !car.Model.IsJetski)
                      {
                          Game.DisplayHelp("This vehicle cannot be towed");
                          return;
                      }

                      car.IsPersistent = true;
                      if (playanims)
                      {
                          Game.LocalPlayer.Character.Tasks.PlayAnimation("random@arrests", "generic_radio_chatter", 1.5f, AnimationFlags.UpperBodyOnly | AnimationFlags.SecondaryTask);
                          GameFiber.Wait(1000);

                          BleepPlayer.Play();
                          GameFiber.Wait(500);
                      }

                      carblip = car.AttachBlip();
                      carblip.Color = System.Drawing.Color.Black;
                      carblip.Scale = 0.7f;
                      if (EntryPoint.IsLSPDFRPlusRunning)
                      {
                          API.LspdfrPlusFunctions.AddCountToStatistic(Main.PluginName, "Vehicles towed");
                      }
                      _ = Game.LocalPlayer.Character;
                      if (car.Model.IsCar && RecruitNearbyTowtruck(out driver, out towTruck))
                      {
                          Game.LogTrivial("Recruited nearby tow truck.");
                      }
                      else
                      {
                          float Heading;
                          bool UseSpecialID = true;
                          Vector3 SpawnPoint;
                          float travelDistance;
                          int waitCount = 0;
                          while (true)
                          {
                              GetSpawnPoint(car.Position, out SpawnPoint, out Heading, UseSpecialID);
                              travelDistance = NativeFunction.Natives.CALCULATE_TRAVEL_DISTANCE_BETWEEN_POINTS<float>(SpawnPoint.X, SpawnPoint.Y, SpawnPoint.Z, car.Position.X, car.Position.Y, car.Position.Z);
                              waitCount++;
                              if (Vector3.Distance(car.Position, SpawnPoint) > EntryPoint.SceneManagementSpawnDistance - 15f && travelDistance < (EntryPoint.SceneManagementSpawnDistance * 4.5f))
                              {

                                  Vector3 directionFromVehicleToPed1 = (car.Position - SpawnPoint);
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
                                  Game.DisplayNotification("Take the car ~s~to a more reachable location.");
                                  Game.DisplayNotification("Alternatively, press ~b~Y ~s~to force a spawn in the ~g~wilderness.");
                              }
                              if ((waitCount >= 600) && Albo1125.Common.CommonLibrary.ExtensionMethods.IsKeyDownComputerCheck(Keys.Y))
                              {
                                  SpawnPoint = Game.LocalPlayer.Character.Position.Around(15f);
                                  break;
                              }
                              GameFiber.Yield();

                          }

                          var displayName = NativeFunction.Natives.GET_DISPLAY_NAME_FROM_VEHICLE_MODEL<string>(car.Model.Hash);
                          if (EntryPoint.UseDisplayNameForVehicle && !string.IsNullOrWhiteSpace(displayName) && displayName != "CARNOTFOUND")
                          {
                              modelName = Game.GetLocalizedString(displayName);
                          }
                          else
                          {
                              modelName = car.Model.Name.ToLower();
                              modelName = char.ToUpper(modelName[0]) + modelName.Substring(1);
                          }

                          if (car.Model.IsCar && !car.IsDead && car.EngineHealth > 100f && car.FuelTankHealth > 750f && !AlwaysFlatbed)
                          {
                              Game.DisplayNotification("~b~Dispatch~w~: Sending a tow truck to pickup " + modelName + ".");
                              towTruck = new Vehicle(TowtruckModel, SpawnPoint, Heading);
                              Game.DisplayHelp("If you want to attach the vehicle yourself, get in now.");
                              flatbed = false;
                          }
                          else
                          {
                              Game.DisplayNotification("~b~Dispatch~w~: Sending a flatbed to pickup " + modelName + ".");
                              towTruck = new Vehicle(FlatbedModel, SpawnPoint, Heading);
                          }
                      }
                      TowTrucksBeingUsed.Add(towTruck);
                      towTruck.IsPersistent = true;
                      towTruck.CanTiresBurst = false;
                      towTruck.IsInvincible = true;
                      if (OverrideTowTruckColour)
                      {
                          towTruck.PrimaryColor = TowTruckColor;
                          towTruck.SecondaryColor = TowTruckColor;
                          towTruck.PearlescentColor = TowTruckColor;
                      }


                      towblip = towTruck.AttachBlip();
                      towblip.Color = System.Drawing.Color.Blue;

                      if (!driver.Exists())
                      {
                          driver = towTruck.CreateRandomDriver();
                      }
                      driver.MakePersistent();
                      driver.BlockPermanentEvents = true;
                      driver.IsInvincible = true;
                      driver.Money = 1233;

                      driveToEntity(driver, towTruck, car, false);
                      NativeFunction.Natives.START_VEHICLE_HORN(towTruck, 5000, 0, true);

                      if (towTruck.Speed > 15f)
                      {
                          NativeFunction.Natives.SET_VEHICLE_FORWARD_SPEED(towTruck, 15f);
                      }
                      driver.Tasks.PerformDrivingManeuver(VehicleManeuver.GoForwardStraightBraking);
                      GameFiber.Sleep(600);
                      driver.Tasks.PerformDrivingManeuver(VehicleManeuver.Wait);
                      towTruck.IsSirenOn = true;
                      GameFiber.Wait(2000);
                      bool automaticallyAttach = false;
                      bool showImpoundMsg = true;
                      if (flatbed)
                      {
                          while (car && car.HasOccupants)
                          {
                              GameFiber.Yield();
                              Game.DisplaySubtitle("~r~Please remove all occupants from the vehicle.", 1);
                          }
                          if (car)
                          {
                              car.AttachTo(towTruck, 20, FlatbedModifier, Rotator.Zero);
                          }
                      }
                      else
                      {
                          if (!Game.LocalPlayer.Character.IsInVehicle(car, true))
                          {
                              automaticallyAttach = true;
                          }

                          while (true)
                          {
                              GameFiber.Sleep(1);
                              driver.Money = 1233;
                              if (!car.Exists()) { break; }
                              if (ExtensionMethods.IsKeyDownRightNowComputerCheck(Keys.D0) || automaticallyAttach)
                              {
                                  if (Game.LocalPlayer.Character.IsInVehicle(car, false))
                                  {
                                      Game.DisplaySubtitle("~r~Get out of the vehicle first.", 5000);

                                  }
                                  else
                                  {
                                      car.Position = towTruck.GetOffsetPosition(Vector3.RelativeBack * 7f);
                                      car.Heading = towTruck.Heading;
                                      if (towTruck.HasTowArm)
                                      {
                                          towTruck.TowVehicle(car, true);
                                      }
                                      else
                                      {
                                          car.Delete();
                                          Game.LogTrivial("AM+: Towing vehicle lacks tow arm");
                                          Game.DisplayNotification("~r~~h~AM+ WARNING~n~~w~The tow truck model does not have tow arms. Contact the vehicle author if it is a custom tow truck, or correct the model. The vehicle is deleted.");
                                      }
                                      Game.HideHelp();
                                      break;
                                  }

                              }
                              else if (Vector3.Distance(towTruck.GetOffsetPosition(Vector3.RelativeBack * 7f), car.Position) < 2.1f)
                              {
                                  if ((towTruck.Heading - car.Heading < 30f) && (towTruck.Heading - car.Heading > -30f))
                                  {
                                      Game.DisplaySubtitle("~b~Exit the vehicle", 1);
                                      if (!Game.LocalPlayer.Character.IsInVehicle(car, true))
                                      {
                                          GameFiber.Sleep(1000);
                                          towTruck.TowVehicle(car, true);
                                          break;
                                      }
                                  }
                                  else if (((towTruck.Heading - car.Heading < -155f) && (towTruck.Heading - car.Heading > -205f)) || ((towTruck.Heading - car.Heading > 155f) && (towTruck.Heading - car.Heading < 205f)))
                                  {
                                      Game.DisplaySubtitle("~b~Exit the vehicle", 1);
                                      if (!Game.LocalPlayer.Character.IsInVehicle(car, true))
                                      {
                                          GameFiber.Sleep(1000);
                                          if (towTruck.HasTowArm)
                                          {
                                              towTruck.TowVehicle(car, false);
                                          }
                                          else
                                          {
                                              car.Delete();
                                              Game.LogTrivial("Tow truck model is not registered as a tow truck ingame - if this is a custom vehicle, contact the vehicle author.");
                                              Game.DisplayNotification("Tow truck model is not registered as a tow truck ingame - if this is a custom vehicle, contact the vehicle author.");
                                          }
                                          break;
                                      }
                                  }
                                  else
                                  {
                                      Game.DisplaySubtitle("~b~Align the ~b~vehicle~s~ with the ~g~tow truck.", 1);

                                  }
                              }
                              else
                              {
                                  Game.DisplaySubtitle("Drive the vehicle behind the tow truck.", 1);
                              }

                              if (Vector3.Distance(Game.LocalPlayer.Character.Position, car.Position) > 70f)
                              {
                                  car.Position = towTruck.GetOffsetPosition(Vector3.RelativeBack * 7f);
                                  car.Heading = towTruck.Heading;

                                  if (towTruck.HasTowArm)
                                  {
                                      towTruck.TowVehicle(car, true);
                                  }
                                  else
                                  {
                                      car.Delete();
                                      Game.LogTrivial("Tow truck model is not registered as a tow truck ingame - if this is a custom vehicle, contact the vehicle author.");
                                      Game.DisplayNotification("Tow truck model is not registered as a tow truck ingame - if this is a custom vehicle, contact the vehicle author.");
                                  }
                                  break;
                              }
                              if (Vector3.Distance(car.Position, towTruck.Position) > 80f)
                              {
                                  Game.DisplaySubtitle("Towing service cancelled", 5000);
                                  showImpoundMsg = false;
                                  break;
                              }
                          }
                      }

                      Game.HideHelp();
                      if (showImpoundMsg)
                      {
                          Game.DisplayNotification("The target ~r~" + modelName + " ~s~has been impounded!");
                      }
                      driver.Tasks.PerformDrivingManeuver(VehicleManeuver.GoForwardStraight).WaitForCompletion(600);
                      driver.Tasks.CruiseWithVehicle(25f);
                      GameFiber.Wait(1000);
                      if (car.Exists() && towTruck.Exists() && !flatbed && !car.FindTowTruck().Exists())
                      {

                          car.Position = towTruck.GetOffsetPosition(Vector3.RelativeBack * 7f);
                          car.Heading = towTruck.Heading;

                          if (towTruck.HasTowArm)
                          {
                              towTruck.TowVehicle(car, true);
                          }
                          else
                          {
                              car.Delete();
                              Game.LogTrivial("Tow truck model is not registered as a tow truck ingame - if this is a custom vehicle, contact the vehicle author.");
                              Game.DisplayNotification("Tow truck model is not registered as a tow truck ingame - if this is a custom vehicle, contact the vehicle author.");
                          }
                      }
                      if (driver.Exists()) { driver.Dismiss(); }
                      if (car.Exists()) { car.Dismiss(); }

                      if (towTruck.Exists()) { towTruck.Dismiss(); }
                      if (towblip.Exists()) { towblip.Delete(); }
                      if (carblip.Exists()) { carblip.Delete(); }

                      while (towTruck.Exists() && car.Exists())
                      {
                          GameFiber.Sleep(1000);
                      }
                      if (car.Exists())
                      {
                          car.Delete();
                      }

                  }
                  catch (Exception e)
                  {
                      Game.LogTrivial(e.ToString());
                      Game.LogTrivial("Tow Truck Crashed");
                      Game.DisplayNotification("The towing service was interrupted.");
                      if (towblip.Exists()) { towblip.Delete(); }
                      if (carblip.Exists()) { carblip.Delete(); }
                      if (driver.Exists()) { driver.Delete(); }
                      if (car.Exists()) { car.Delete(); }
                      if (towTruck.Exists()) { towTruck.Delete(); }
                  }
              });
        }

        public string[] insurancevehicles = new string[] { "JACKAL", "ASTEROPE", "TAILGATER", "PREMIER", "FUSILADE" };
        private Blip businesscarblip;
        private Ped passenger;
        private Vehicle businessCar;
        internal void RequestInsurance()
        {

            _ = GameFiber.StartNew(delegate
              {

                  try
                  {
                      Vehicle[] nearbyvehs = Game.LocalPlayer.Character.GetNearbyVehicles(2);
                      if (nearbyvehs.Length == 0)
                      {
                          Game.DisplayNotification("~r~Couldn't detect a close enough vehicle.");
                          return;
                      }

                      car = nearbyvehs[0];
                      if (Vector3.Distance(Game.LocalPlayer.Character.Position, car.Position) > 6f)
                      {
                          Game.DisplayNotification("~r~Couldn't detect a close enough vehicle.");
                          return;
                      }
                      if (car.HasOccupants)
                      {
                          if (nearbyvehs.Length == 2)
                          {
                              car = nearbyvehs[1];
                              if (car.HasOccupants)
                              {
                                  Game.DisplayNotification("~r~Couldn't detect a close enough vehicle without occupants.");
                                  return;
                              }
                          }
                          else
                          {
                              Game.DisplayNotification("~r~Couldn't detect a close enough vehicle without occupants.");
                              return;
                          }

                      }
                      if (car.IsPoliceVehicle)
                      {
                          Game.DisplayNotification("Are you sure you want to remove the police vehicle? ~h~~b~Y/N");
                          while (true)
                          {
                              GameFiber.Yield();
                              if (Albo1125.Common.CommonLibrary.ExtensionMethods.IsKeyDownComputerCheck(System.Windows.Forms.Keys.Y))
                              {
                                  break;
                              }
                              if (Albo1125.Common.CommonLibrary.ExtensionMethods.IsKeyDownComputerCheck(System.Windows.Forms.Keys.N))
                              {
                                  return;
                              }
                          }
                      }
                      ToggleMobilePhone(Game.LocalPlayer.Character, true);
                      GameFiber.Sleep(3000);
                      ToggleMobilePhone(Game.LocalPlayer.Character, false);
                      car.IsPersistent = true;
                      carblip = car.AttachBlip();
                      carblip.Color = System.Drawing.Color.Black;
                      carblip.Scale = 0.7f;
                      _ = car.Model.Name.ToLower();
                      modelName = char.ToUpper(modelName[0]) + modelName.Substring(1);
                      Ped playerPed = Game.LocalPlayer.Character;
                      if (EntryPoint.IsLSPDFRPlusRunning)
                      {
                          API.LspdfrPlusFunctions.AddCountToStatistic(Main.PluginName, "Insurance pickups");
                      }
                      Vector3 SpawnPoint = World.GetNextPositionOnStreet(playerPed.Position.Around(EntryPoint.SceneManagementSpawnDistance));
                      float travelDistance;
                      int waitCount = 0;
                      while (true)
                      {
                          SpawnPoint = World.GetNextPositionOnStreet(playerPed.Position.Around(EntryPoint.SceneManagementSpawnDistance));
                          travelDistance = NativeFunction.Natives.CALCULATE_TRAVEL_DISTANCE_BETWEEN_POINTS<float>(SpawnPoint.X, SpawnPoint.Y, SpawnPoint.Z, playerPed.Position.X, playerPed.Position.Y, playerPed.Position.Z);
                          waitCount++;
                          if (Vector3.Distance(playerPed.Position, SpawnPoint) > EntryPoint.SceneManagementSpawnDistance - 10f && travelDistance < (EntryPoint.SceneManagementSpawnDistance * 4.5f))
                          {
                              break;
                          }
                          if (waitCount == 600)
                          {
                              Game.DisplayNotification("Take the car ~s~to a more reachable location.");
                              Game.DisplayNotification("Alternatively, press ~b~Y ~s~to force a spawn in the ~g~wilderness.");
                          }
                          if ((waitCount >= 600) && ExtensionMethods.IsKeyDownComputerCheck(Keys.Y))
                          {
                              SpawnPoint = Game.LocalPlayer.Character.Position.Around(15f);
                              break;
                          }
                          GameFiber.Yield();

                      }
                      car.LockStatus = VehicleLockStatus.Unlocked;
                      car.MustBeHotwired = false;
                      Game.DisplayNotification("mphud", "mp_player_ready", "~h~Mors Mutual Insurance", "~b~Vehicle Pickup Status Update", "Two of our employees are en route to pick up our client's ~h~" + modelName + ".");
                      businessCar = new Vehicle(insurancevehicles[SharedRandom.Next(insurancevehicles.Length)], SpawnPoint);
                      businesscarblip = businessCar.AttachBlip();
                      businesscarblip.Color = System.Drawing.Color.Blue;
                      businessCar.IsPersistent = true;
                      Vector3 directionFromVehicleToPed = (car.Position - businessCar.Position);
                      directionFromVehicleToPed.Normalize();
                      businessCar.Heading = MathHelper.ConvertDirectionToHeading(directionFromVehicleToPed);
                      driver = new Ped("a_m_y_business_02", businessCar.Position, businessCar.Heading);
                      driver.BlockPermanentEvents = true;
                      driver.WarpIntoVehicle(businessCar, -1);
                      driver.Money = 1;

                      passenger = new Ped("a_m_y_business_02", businessCar.Position, businessCar.Heading);
                      passenger.BlockPermanentEvents = true;
                      passenger.WarpIntoVehicle(businessCar, 0);
                      passenger.Money = 1;

                      driveToEntity(driver, businessCar, car, true);
                      Rage.Native.NativeFunction.Natives.START_VEHICLE_HORN(businessCar, 3000, 0, true);
                      while (true)
                      {
                          GameFiber.Yield();
                          driver.Tasks.DriveToPosition(car.GetOffsetPosition(Vector3.RelativeFront * 2f), 10f, VehicleDrivingFlags.DriveAroundVehicles | VehicleDrivingFlags.DriveAroundObjects | VehicleDrivingFlags.AllowMedianCrossing | VehicleDrivingFlags.YieldToCrossingPedestrians).WaitForCompletion(500);
                          if (Vector3.Distance(businessCar.Position, car.Position) < 15f)
                          {


                              driver.Tasks.PerformDrivingManeuver(VehicleManeuver.Wait);
                              break;
                          }
                          if (Vector3.Distance(car.Position, businessCar.Position) > 50f)
                          {
                              SpawnPoint = World.GetNextPositionOnStreet(car.Position);
                              businessCar.Position = SpawnPoint;
                              directionFromVehicleToPed = (car.Position - SpawnPoint);
                              directionFromVehicleToPed.Normalize();

                              float vehicleHeading = MathHelper.ConvertDirectionToHeading(directionFromVehicleToPed);
                              businessCar.Heading = vehicleHeading;
                          }
                      }

                      driver.PlayAmbientSpeech("GENERIC_HOWS_IT_GOING", true);
                      passenger.Tasks.LeaveVehicle(LeaveVehicleFlags.None).WaitForCompletion();
                      Rage.Native.NativeFunction.Natives.SET_PED_CAN_RAGDOLL(passenger, false);
                      passenger.Tasks.FollowNavigationMeshToPosition(car.GetOffsetPosition(Vector3.RelativeLeft * 2f), car.Heading, 2f).WaitForCompletion(2000);
                      driver.Dismiss();
                      passenger.Tasks.FollowNavigationMeshToPosition(car.GetOffsetPosition(Vector3.RelativeLeft * 2f), car.Heading, 2f).WaitForCompletion(3000);

                      passenger.Tasks.EnterVehicle(car, 9000, -1).WaitForCompletion();
                      if (car.HasDriver && car.Driver != passenger)
                      {
                          car.Driver.Tasks.LeaveVehicle(LeaveVehicleFlags.WarpOut).WaitForCompletion();
                      }
                      passenger.WarpIntoVehicle(car, -1);
                      GameFiber.Sleep(2000);
                      passenger.PlayAmbientSpeech("GENERIC_THANKS", true);
                      passenger.Dismiss();
                      car.Dismiss();
                      carblip.Delete();
                      businesscarblip.Delete();
                      GameFiber.Sleep(9000);
                      Game.DisplayNotification("mphud", "mp_player_ready", "~h~Mors Mutual Insurance", "~b~Vehicle Pickup Status Update", "Thank you for letting us collect our client's ~h~" + modelName + "!");
                  }

                  catch (Exception e)
                  {
                      Game.LogTrivial(e.ToString());
                      Game.LogTrivial("Insurance company Crashed");
                      Game.DisplayNotification("The insurance pickup service was interrupted.");
                      if (businesscarblip.Exists()) { businesscarblip.Delete(); }
                      if (carblip.Exists()) { carblip.Delete(); }
                      if (driver.Exists()) { driver.Delete(); }
                      if (car.Exists()) { car.Delete(); }
                      if (businessCar.Exists()) { businessCar.Delete(); }
                      if (passenger.Exists()) { passenger.Delete(); }
                  }
              });
        }

        internal static UIMenu VehicleManagementMenu { get; private set; }
        private static UIMenuItem callForTowTruckItem;
        private static UIMenuItem callForInsuranceItem;
        public static void createVehicleManagementMenu()
        {
            VehicleManagementMenu = new UIMenu("ArrestManager+", "VEHICLE MANAGEMENT");
            VehicleManagementMenu.AddItem(MenuSwitchListItem);
            callForTowTruckItem = new UIMenuItem("Tow truck");
            VehicleManagementMenu.AddItem(callForTowTruckItem);
            callForInsuranceItem = new UIMenuItem("Insurance company");
            VehicleManagementMenu.AddItem(callForInsuranceItem);
            VehicleManagementMenu.OnItemSelect += OnItemSelect;
            VehicleManagementMenu.MouseControlsEnabled = false;
            VehicleManagementMenu.AllowCameraMovement = true;


        }
        public static void OnItemSelect(UIMenu sender, UIMenuItem selectedItem, int index)
        {
            if (sender != VehicleManagementMenu) { return; }

            if (selectedItem == callForTowTruckItem)
            {
                NativeFunction.Natives.SET_PED_STEALTH_MOVEMENT(Game.LocalPlayer.Character, 0, 0);
                new VehicleManager().TowVehicle();
                VehicleManagementMenu.Visible = false;
            }
            else if (selectedItem == callForInsuranceItem)
            {
                NativeFunction.Natives.SET_PED_STEALTH_MOVEMENT(Game.LocalPlayer.Character, 0, 0);
                new VehicleManager().RequestInsurance();
                VehicleManagementMenu.Visible = false;
            }
        }
    }
}

