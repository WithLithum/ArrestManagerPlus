using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rage;
using Rage.Native;
using System.IO;
using LSPD_First_Response;
using LSPD_First_Response.Mod.API;

using System.Windows.Forms;
using Arrest_Manager;
using System.Reflection;
using RAGENativeUI;
using RAGENativeUI.Elements;
using Albo1125.Common.CommonLibrary;
using System.Diagnostics;
using System.Threading;
using System.Management;
using System.Net;

namespace Arrest_Manager
{
    internal static class EntryPoint
    {

        //PROPERTIES FOR CHOICE & KEYS
        private static bool MessageReceived { get; set; }
        internal static bool CanChoose { get; set; }
        private static bool CheckForJail { get; set; }
        private static bool autoDoorEnabled { get; set; }
        public static Keys SceneManagementKey { get; set; }
        public static Keys SceneManagementModifierKey { get; set; }
        public static float SceneManagementSpawnDistance { get; set; }

        internal static bool UseDisplayNameForVehicle { get; set; }

        //INI

        public static InitializationFile InitializeFile()
        {
            InitializationFile ini = new InitializationFile("Plugins/LSPDFR/Arrest Manager.ini");
            ini.Create();
            return ini;
        }

        private static string getAutoDoorEnabled()
        {
            InitializationFile ini = InitializeFile();
            string enabled = ini.ReadString("General", "AutoDoorShutEnabled", "true");
            return enabled;
        }

        private static void CalculateDistanceOfSceneManagement()
        {
            InitializationFile ini = InitializeFile();
            SceneManagementSpawnDistance = ini.ReadSingle("Misc", "SceneManagementSpawnDistance", 70f);
            if (SceneManagementSpawnDistance < 50f)
            {
                SceneManagementSpawnDistance = 50f;
            }
            else if (SceneManagementSpawnDistance > 250f)
            {
                SceneManagementSpawnDistance = 250f;
            }
        }

        internal static Vector3 NearestDrivingNodePosition(Vector3 pos, int Nth, int nodeType = 1)
        {
            int node_id = GetNearestDrivingNodeID(pos, Nth, nodeType);
            if (IsNodeIDValid(node_id))
            {
                Vector3 out_position;

                NativeFunction.Natives.GET_VEHICLE_NODE_POSITION(node_id, out out_position);

                return out_position;
            }
            else
            {
                return Vector3.Zero;
            }
        }

        internal static int GetNearestDrivingNodeID(Vector3 pos, int Nth = 0, int nodeType = 1)
        {
            int node_id = NativeFunction.Natives.GET_NTH_CLOSEST_VEHICLE_NODE_ID<int>(pos.X, pos.Y, pos.Z, Nth, nodeType, 0x40400000, 100f);
            return node_id;
        }

        private static bool IsNodeIDValid(int nodeID)
        {
            return NativeFunction.Natives.IS_VEHICLE_NODE_ID_VALID<bool>(nodeID);
        }

        internal static Vector3 GetBoatSpawnPoint(Vector3 destination, float distance)
        {
            distance += (float)MathHelper.GetRandomDouble(0, 10);
            Vector3 spawnpoint = destination;
            int n = 0;
            while (spawnpoint.DistanceTo(destination) < distance - 5f)
            {
                spawnpoint = NearestDrivingNodePosition(destination, n, 3);
                n += 10;
            }

            return spawnpoint;
        }


        internal static List<Ped> suspectsArrestedByPlayer { get; set; }
        private static List<Ped> SuspectsNotArrestedByPlayer = new List<Ped>();
        private static void isSomeoneGettingArrestedByPlayer()
        {

            GameFiber.StartNew(delegate
            {
                while (true)
                {
                    GameFiber.Yield();

                    try
                    {
                        Ped playerPed = Game.LocalPlayer.Character;
                        if (playerPed.Exists() && playerPed.IsValid() && playerPed.GetNearbyPeds(1).Length == 1)
                        {
                            Ped nearestPed = playerPed.GetNearbyPeds(1)[0];
                            if (Functions.IsPedArrested(nearestPed))
                            {
                                arrestingOfficer = Functions.GetPedArrestingOfficer(nearestPed);
                                if ((arrestingOfficer == playerPed) && !suspectsArrestedByPlayer.Contains(nearestPed))
                                {
                                    suspectsArrestedByPlayer.Add(nearestPed);

                                    suspectsWithVehicles.Add(nearestPed, nearestPed.LastVehicle);
                                    if (suspectsWithVehicles.ContainsKey(nearestPed)) { Game.LogTrivial("Contains key after add"); }
                                    NativeFunction.Natives.SET_PED_DROPS_WEAPON(nearestPed); Game.LogTrivial("Weapon Dropped");
                                    API.Functions.OnPlayerArrestedPed(nearestPed);



                                }

                                else if (!SuspectsNotArrestedByPlayer.Contains(nearestPed) && !suspectsArrestedByPlayer.Contains(nearestPed))
                                {


                                    Game.LogTrivial("Adding suspect not arrested by player");
                                    SuspectsNotArrestedByPlayer.Add(nearestPed);


                                }
                            }
                        }

                    }
                    catch (ThreadAbortException) { break; }
                    catch (Exception e)
                    {
                        Game.LogTrivial(e.ToString());
                    }
                }
            });
        }

        private static string suspectName = "";

        internal static readonly Random SharedRandomInstance = new Random();
        private static bool checkingForArrestedByPlayer { get; set; }

        private static Ped arrestingOfficer { get; set; }
        private static bool canIWarpToJail { get; set; }
        internal static Ped suspectAPI { get; set; }

        internal static Ped suspectFromVehicle { get; set; }
        private static bool releaseMessageReceived { get; set; }

        private static bool OfficerAudio { get; set; }

        private static bool DispatchVoice { get; set; }
        public static KeysConverter KeyConvert { get; } = new KeysConverter();
        private static List<string> namesUsed { get; set; }
        private static Dictionary<Ped, Vehicle> suspectsWithVehicles = new Dictionary<Ped, Vehicle>();
        internal static bool IsLSPDFRPlusRunning { get; set; }
        internal static bool AllowWarping { get; set; } = true;

        public static void Choice()
        {
            try
            {
                SceneManagementKey = (Keys)KeyConvert.ConvertFromString(InitializeFile().ReadString("Keybindings", "SceneManagementKey", "H"));
                SceneManagementModifierKey = (Keys)KeyConvert.ConvertFromString(InitializeFile().ReadString("Keybindings", "SceneManagementModifierKey", "LControlKey"));
                PedManager.GrabPedKey = (Keys)KeyConvert.ConvertFromString(InitializeFile().ReadString("Keybindings", "GrabPedKey", "T"));
                PedManager.GrabPedModifierKey = (Keys)KeyConvert.ConvertFromString(InitializeFile().ReadString("Keybindings", "GrabPedModifierKey", "LShiftKey"));
                PedManager.PlacePedInVehicleKey = (Keys)KeyConvert.ConvertFromString(InitializeFile().ReadString("Keybindings", "PlacePedInVehicleKey"));
                PedManager.TackleKey = (Keys)KeyConvert.ConvertFromString(InitializeFile().ReadString("Keybindings", "TackleKey", "E"));
                PedManager.TackleButton = InitializeFile().ReadEnum<ControllerButtons>("Keybindings", "TackleButton", ControllerButtons.A);

                autoDoorEnabled = bool.Parse(getAutoDoorEnabled());
                OfficerAudio = InitializeFile().ReadBoolean("Misc", "OfficerAudio", true);
                DispatchVoice = InitializeFile().ReadBoolean("Misc", "DispatchAudio", true);

                string Towtruckcolor = InitializeFile().ReadString("Misc", "TowTruckColourOverride", "");
                if (!string.IsNullOrWhiteSpace(Towtruckcolor))
                {
                    VehicleManager.TowTruckColor = System.Drawing.Color.FromName(Towtruckcolor);
                    VehicleManager.OverrideTowTruckColour = true;
                }
                VehicleManager.TowtruckModel = InitializeFile().ReadString("Misc", "TowTruckModel", "TOWTRUCK");
                if (!VehicleManager.TowtruckModel.IsValid)
                {
                    VehicleManager.TowtruckModel = "TOWTRUCK";
                    Game.LogTrivial("Invalid Tow Truck Model in Arrest Manager.ini. Setting to TOWTRUCK.");
                }

                VehicleManager.FlatbedModel = InitializeFile().ReadString("Misc", "FlatbedModel", "FLATBED");
                if (!VehicleManager.FlatbedModel.IsValid)
                {
                    VehicleManager.FlatbedModel = "FLATBED";
                    Game.LogTrivial("Invalid Flatbed Model in Arrest Manager.ini. Setting to FLATBED.");
                }
                VehicleManager.AlwaysFlatbed = InitializeFile().ReadBoolean("Misc", "AlwaysUseFlatbed", false);
                VehicleManager.FlatbedModifier = new Vector3(InitializeFile().ReadSingle("Misc", "FlatbedX", -0.5f), InitializeFile().ReadSingle("Misc", "FlatbedY", -5.75f),
                    InitializeFile().ReadSingle("Misc", "FlatbedZ", 1.005f));
                AllowWarping = InitializeFile().ReadBoolean("Misc", "AllowWarping", true);

                CalculateDistanceOfSceneManagement();
                VehicleManager.RecruitNearbyTowTrucks = InitializeFile().ReadBoolean("Misc", "RecruitNearbyTowTrucks");
                Coroner.CoronerModel = InitializeFile().ReadString("Misc", "CoronerPedModel", "S_M_M_DOCTOR_01");
                if (!Coroner.CoronerModel.IsValid)
                {
                    Coroner.CoronerModel = "S_M_M_DOCTOR_01";
                }

                Coroner.CoronerVehicleModel = InitializeFile().ReadString("Misc", "CoronerVehicleModel", "SPEEDO");
                if (!Coroner.CoronerVehicleModel.IsValid || !Coroner.CoronerVehicleModel.IsVehicle)
                {
                    Game.LogTrivial("Arrest Manager: The specified coroner vehicle is either invalid or not a vehicle. Use at own risk! " + Coroner.CoronerVehicleModel.Name);

                }

                UseDisplayNameForVehicle = InitializeFile().ReadBoolean("Misc", "UseDisplayNameForVehicle", true);
            }
            catch
            {
                SceneManagementKey = Keys.H;
                SceneManagementModifierKey = Keys.LControlKey;
                SceneManagementSpawnDistance = 70f;
                DispatchVoice = true;
                OfficerAudio = true;
                autoDoorEnabled = true;
                Game.DisplayNotification("~r~~h~Error while reading Arrest Manager.ini. Replace with default from download! Loading default settings...");
            }

            GameFiber.Wait(4000);

            Game.LogTrivial("AM+: Loaded ArrestManager+ phase 1");
            IsLSPDFRPlusRunning = IsLSPDFRPluginRunning("LSPDFR+", new Version("1.7.0.0"));

            if (IsLSPDFRPluginRunning("PoliceSmartRadio"))
            {
                API.SmartRadioFuncs.AddActionToButton(Coroner.CallFromSmartRadio, Coroner.CanBeCalled, "coroner");
                API.SmartRadioFuncs.AddActionToButton(VehicleManager.SmartRadioTow, "tow");
            }
            MessageReceived = false;
            CanChoose = true;
            CheckForJail = true;

            namesUsed = new List<string>();
            arrestingOfficer = null;
            suspectsArrestedByPlayer = new List<Ped>();
            canIWarpToJail = true;

            checkingForArrestedByPlayer = false;
            releaseMessageReceived = false;
            bool doorsClosed = false;

            SceneManager.CreateMenus();

            isSomeoneGettingArrestedByPlayer();

            //Listens for key input and calls appropriate method

            GameFiber.StartNew(() =>
            {
                while (true)
                {
                    Game.LocalPlayer.WantedLevel = 0;
                    var playerPed = Game.LocalPlayer.Character;
                    GameFiber.Yield();
                    Game.SetRelationshipBetweenRelationshipGroups("PLAYER", "ARRESTEDSUSPECTS", Relationship.Respect);
                    Game.SetRelationshipBetweenRelationshipGroups("ARRESTEDSUSPECTS", "PLAYER", Relationship.Respect);
                    Game.SetRelationshipBetweenRelationshipGroups("COP", "ARRESTEDSUSPECTS", Relationship.Respect);
                    Game.SetRelationshipBetweenRelationshipGroups("ARRESTEDSUSPECTS", "COP", Relationship.Respect);
                    Game.LogTrivial("AM+: Relationship - set");
                    //while you have the option to choose, listen for input
                    while (CanChoose)
                    {
                        GameFiber.Yield();
                        playerPed = Game.LocalPlayer.Character;


                        //Check: first for warp, release, then for multi, then for single

                    }

                    if (autoDoorEnabled)
                    {
                        if (playerPed.IsInAnyVehicle(false))
                        {
                            if (playerPed.CurrentVehicle.Driver == playerPed && playerPed.CurrentVehicle.Speed > 3f && !doorsClosed)
                            {
                                doorsClosed = true;
                                NativeFunction.Natives.SET_VEHICLE_DOORS_SHUT(playerPed.CurrentVehicle, true);
                            }
                        }
                        else
                        {

                            doorsClosed = false;
                        }
                    }
                    Game.LocalPlayer.WantedLevel = 0;
                }
            }, "Choice");
            Game.LogTrivial("AM+: Done starting fiber");
        }
        //Eventhandlers for on/off duty


        public static bool IsLSPDFRPluginRunning(string Plugin, Version minversion = null)
        {
            foreach (Assembly assembly in Functions.GetAllUserPlugins())
            {
                AssemblyName an = assembly.GetName();
                if (string.Equals(an.Name, Plugin, StringComparison.OrdinalIgnoreCase) && (minversion == null || an.Version.CompareTo(minversion) >= 0))
                { return true; }
            }
            return false;
        }
        public static Assembly LSPDFRResolveEventHandler(object sender, ResolveEventArgs args) { foreach (Assembly assembly in Functions.GetAllUserPlugins()) { if (args.Name.IndexOf(assembly.GetName().Name, StringComparison.OrdinalIgnoreCase) >= 0) { return assembly; } } return null; }

        internal static void Initialize()
        {
            GameFiber.StartNew(delegate
            {
                Game.LogTrivial("AM+: Loaded ArrestManager+, phase 2");
                GameFiber.Wait(6000);
                Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept", "ArrestManager+", "Loaded", "");
            });
            Game.LogTrivial("AM+: ArrestManager+ done loading");
            Choice();
        }
    }
}