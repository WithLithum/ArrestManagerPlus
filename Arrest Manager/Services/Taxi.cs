using System;
using System.Collections.Generic;
using System.Windows.Forms;
using LSPD_First_Response.Mod.API;
using Rage;
using Rage.Native;
using static Arrest_Manager.SceneManager;

namespace Arrest_Manager.Services
{
    internal class Taxi
    {
        private Vehicle _taxi;
        private Ped _taxiDriver;
        private Ped _currentSubject;
        private static readonly List<Ped> _subjects = new List<Ped>();

        public void CallTaxi()
        {
            GameFiber.StartNew(() =>
            {
                try
                {
                    _currentSubject = PedManager.GetNearestValidPed();
                    if (!_currentSubject) return;

                    if (Functions.IsPedArrested(_currentSubject))
                    {
                        return;
                    }

                    if (_currentSubject.IsInAnyVehicle(false))
                    {
                        Game.DisplayHelp("You cannot call taxi for a ped in a vehicle.");
                        return;
                    }

                    if (_subjects.Contains(_currentSubject)) { Game.DisplayHelp("Taxi is already assigned to this suspect."); return; }
                    ToggleMobilePhone(Game.LocalPlayer.Character, true);
                    _subjects.Add(_currentSubject);
                    _currentSubject.IsPersistent = true;
                    _currentSubject.BlockPermanentEvents = true;
                    _currentSubject.Tasks.StandStill(-1);
                    Functions.SetPedCantBeArrestedByPlayer(_currentSubject, true);

                    float Heading;
                    bool UseSpecialID = true;
                    Vector3 SpawnPoint;
                    float travelDistance;
                    int waitCount = 0;
                    while (true)
                    {
                        GetSpawnPoint(_currentSubject.Position, out SpawnPoint, out Heading, UseSpecialID);
                        travelDistance = NativeFunction.Natives.CALCULATE_TRAVEL_DISTANCE_BETWEEN_POINTS<float>(SpawnPoint.X, SpawnPoint.Y, SpawnPoint.Z, _currentSubject.Position.X, _currentSubject.Position.Y, _currentSubject.Position.Z);
                        waitCount++;
                        if (Vector3.Distance(_currentSubject.Position, SpawnPoint) > EntryPoint.SceneManagementSpawnDistance - 15f && travelDistance < (EntryPoint.SceneManagementSpawnDistance * 4.5f))
                        {
                            var direction = _currentSubject.Position - SpawnPoint;
                            direction.Normalize();

                            var heading = MathHelper.ConvertDirectionToHeading(direction);

                            if (Math.Abs(MathHelper.NormalizeHeading(Heading) - MathHelper.NormalizeHeading(heading)) < 150f)
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
                    _taxi = new Vehicle("TAXI", SpawnPoint, Heading)
                    {
                        IsPersistent = true,
                        IsTaxiLightOn = false
                    };
                    var taxiblip = _taxi.AttachBlip();
                    taxiblip.Color = System.Drawing.Color.Blue;
                    taxiblip.Flash(500, -1);
                    _taxiDriver = _taxi.CreateRandomDriver();
                    _taxiDriver.IsPersistent = true;
                    _taxiDriver.BlockPermanentEvents = true;
                    _taxiDriver.Money = 1233;

                    Game.DisplayNotification("~b~Taxi Control~w~: Dispatching taxi to your location.");
                    TaskDriveToEntity(_taxiDriver, _taxi, _currentSubject, true);
                    NativeFunction.Natives.START_VEHICLE_HORN(_taxi, 5000, 0, true);
                    if (_taxi.Speed > 15f)
                    {
                        NativeFunction.Natives.SET_VEHICLE_FORWARD_SPEED(_taxi, 15f);
                    }

                    _taxiDriver.Tasks.PerformDrivingManeuver(VehicleManeuver.GoForwardStraightBraking);
                    GameFiber.Sleep(600);
                    _taxiDriver.Tasks.PerformDrivingManeuver(VehicleManeuver.Wait);
                    if (taxiblip.Exists()) { taxiblip.Delete(); }

                    if (PedManager.FollowingPed == _currentSubject) PedManager.IsFollowingEnabled = false;
                    NativeFunction.Natives.SET_PED_CAN_RAGDOLL(_currentSubject, false);
                    _currentSubject.Tasks.Clear();
                    _currentSubject.Tasks.FollowNavigationMeshToPosition(_taxi.GetOffsetPosition(Vector3.RelativeLeft * 2f), _taxi.Heading, 1.65f).WaitForCompletion(12000);
                    _currentSubject.Tasks.EnterVehicle(_taxi, 8000, 1).WaitForCompletion();

                    _taxiDriver.Dismiss();
                    _taxi.Dismiss();

                    while (true)
                    {
                        GameFiber.Yield();
                        try
                        {
                            if (_currentSubject.Exists())
                            {
                                if (!_taxi.Exists())
                                {
                                    _currentSubject.Delete();
                                }
                                if (!_currentSubject.IsDead)
                                {
                                    if (Vector3.Distance(Game.LocalPlayer.Character.Position, _currentSubject.Position) > 80f)
                                    {
                                        _currentSubject.Delete();
                                        break;
                                    }
                                    if (!_currentSubject.IsInVehicle(_taxi, false))
                                    {
                                        _currentSubject.Delete();
                                        break;
                                    }
                                }
                                else
                                {
                                    _currentSubject.Delete();
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
                            if (_currentSubject.Exists())
                            {
                                _currentSubject.Delete();
                            }
                            break;
                        }
                    }
                }
                catch (Exception e)
                {
                    Game.LogTrivial(e.ToString());
                    Game.DisplayNotification("The taxi pickup service was interrupted");
                    if (_taxi.Exists())
                    {
                        _taxi.Delete();
                    }
                    if (_taxiDriver.Exists()) { _taxiDriver.Delete(); }
                    if (_currentSubject.Exists()) { _currentSubject.Delete(); }
                }
            });
        }
    }
}
