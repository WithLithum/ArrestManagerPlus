using System;
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
