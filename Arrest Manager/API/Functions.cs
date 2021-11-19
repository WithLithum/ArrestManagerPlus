using System;
using System.Reflection;
using Albo1125.Common.CommonLibrary;
using Rage;

namespace Arrest_Manager.API
{
    /// <summary>
    /// Handles a single ped.
    /// </summary>
    /// <param name="ped">The ped.</param>
    public delegate void SinglePedEventHandler(Ped ped);

    /// <summary>Provides API functions.</summary>
    public static class Functions
    {
        /// <summary>
        /// Request transport for the specified suspect. Returns a bool indicating whether requesting transport was successful.
        /// </summary>
        /// <param name="suspect">The ped to be transported. Does not necessarily have to be arrested.</param>
        /// <returns>Returns a bool indicating whether requesting transport was successful.</returns>
        [Obsolete("TRANSPORT DEPRECATED")]
        public static bool RequestTransport(Ped suspect)
        {
            RequestTransport();
            return false;
        }

        /// <summary>
        /// Request transport for the nearest suspect that has transport on standby. If multiple suspects are available, requests multi transport automatically. Returns a bool indicating whether requesting transport was successful.
        /// </summary>
        /// <returns>Returns a bool indicating whether requesting transport was successful.</returns>
        [Obsolete("TRANSPORT FEATURE DROPPED")]
        public static bool RequestTransport()
        {
            Game.LogTrivial("AM+: WARNING - Transport has been DEPRECATED");
            Game.DisplayNotification("~r~~h~ARREST MANAGER+ WARNING~n~~w~Police transport feature is deprecated - please contact your call-out/event/feature author to remove call to the transport, use LSPDFR instead");
            
            return false;
        }

        /// <summary>
        /// Requests transport for the nearest ped that has transport on standby. Returns a bool indicating whether requesting transport was successful.
        /// </summary>
        /// <param name="Cop">Cop to drive the pickup vehicle.</param>
        /// <param name="PoliceTransportVehicle">Pickup vehicle to be driven by the cop.</param>
        /// <returns>Returns a bool indicating whether requesting transport was successful.</returns>
        [Obsolete("TRANSPORT DEPRECATED")]
        public static bool RequestTransport(Ped Cop, Vehicle PoliceTransportVehicle)
        {
            RequestTransport();
            return false;
        }


        /// <summary>
        /// Dispatches a tow truck for the target vehicle.
        /// </summary>
        /// <param name="VehicleToTow">Must not have occupants and be a valid model that can be towed (no planes etc.).</param>
        /// <param name="PlayAnims">Determines whether the player performs the radio animation or not.</param>
        public static void RequestTowTruck(Vehicle VehicleToTow, bool PlayAnims = true)
        {
            new VehicleManager().TowVehicle(VehicleToTow, PlayAnims);
        }

        /// <summary>
        /// Dispatches a tow truck for the nearest valid vehicle.
        /// </summary>
        /// <param name="PlayAnims">Determines whether the player performs the radio animation or not.</param>
        public static void RequestTowTruck(bool PlayAnims = true)
        {
            new VehicleManager().TowVehicle(PlayAnims);
        }

        /// <summary>
        /// Requests insurance company pickup for the nearest valid vehicle.
        /// </summary>
        public static void RequestInsurancePickupForNearbyVehicle()
        {
            new VehicleManager().RequestInsurance();
        }

        /// <summary>
        /// Raised whenever the player arrests a ped. Only raised once per arrested ped.
        /// </summary>
        public static event SinglePedEventHandler PlayerArrestedPed;

        internal static void OnPlayerArrestedPed(Ped ped)
        {
            PlayerArrestedPed?.Invoke(ped);
        }

        /// <summary>
        /// Raised whenever the player grabs a ped.
        /// </summary>
        public static event SinglePedEventHandler PlayerGrabbedPed;

        internal static void OnPlayerGrabbedPed(Ped ped)
        {
            if (PlayerGrabbedPed != null)
            {
                PlayerGrabbedPed(ped);
            }
        }

        /// <summary>
        /// Determines whether the specified ped is grabbed.
        /// </summary>
        /// <param name="ped"></param>
        /// <returns><c>true</c> if the ped is grabbed; otherwise, <c>false</c>.</returns>
        public static bool IsPedGrabbed(Ped ped)
        {
            if (PedManager.IsGrabEnabled)
            {
                return ped.Equals(PedManager.FollowingPed);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Determines whether the player is grabbing any ped.
        /// </summary>
        /// <returns><c>true</c> if player is grabbing someone; otherwise, <c>false</c>.</returns>
        public static bool IsPedGrabbed()
        {
            return PedManager.IsGrabEnabled;
        }

        /// <summary>
        /// If a ped is currently grabbed, releases it.
        /// </summary>
        public static void ReleaseGrabbedPed()
        {
            PedManager.IsGrabEnabled = false;
        }

        /// <summary>
        /// Arrests the ped as would happen using the Ped Management menu. Must use Grab feature to move the ped around and place in vehicle.
        /// </summary>
        /// <param name="suspect">The ped to be arrested.</param>
        [Obsolete("You cannot arrest ped with API anymore.")]
        public static void ArrestPed(Ped suspect)
        {
            if (suspect)
            {
                Game.LogTrivial("!!!!! ARREST MANAGER+ WARNING !!!!!");
                Game.LogTrivial("Someone is calling deprecated API function ArrestPed");
                Game.LogTrivial("This is not supported, and will be removed in the future!");
                Game.DisplayNotification("~r~~h~ARREST MANAGER+ WARNING~w~~n~Deprecated API function ArrestPed is being called. Please notify the user plug-in author of the currently executing call-out, event or feature.");
                PedManager.ArrestPed(suspect);
            }
        }

        /// <summary>
        /// Calls a coroner to the player's location if there are dead bodies in the vicinity.
        /// </summary>
        /// <param name="radioAnimation">Determines whether to play a radio animation for the player.</param>
        public static void CallCoroner(bool radioAnimation)
        {
            if (radioAnimation)
            {
                Coroner.Main();
            }
            else
            {
                Coroner.CallFromSmartRadio();
            }
        }

        /// <summary>
        /// Calls a coroner to the specified location even if there are no dead bodies in the vicinity (yet).
        /// </summary>
        /// <param name="destination">The destination for the coroners.</param>
        /// <param name="radioAnimation">Determines whether to play a radio animation for the player.</param>
        public static void CallCoroner(Vector3 destination, bool radioAnimation)
        {
            new Coroner(destination, radioAnimation).InitCoronerThread();
        }
    }
}
