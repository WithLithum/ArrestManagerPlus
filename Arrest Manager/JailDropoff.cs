using Rage;
using Rage.Native;
using System;
using System.Xml.Serialization;

namespace Arrest_Manager
{
    [Obsolete("No jail dropoffs.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S1104:Fields should not have public accessibility")]
    public class JailDropoff
    {
        [XmlIgnore]
        public Vector3 Position
        {
            get
            {
                return new Vector3(X, Y, Z);
            }
        }
        [XmlIgnore]
        public Blip blip;

        /// <summary>
        /// The x component of the vector.
        /// </summary>
        public float X;

        /// <summary>
        /// The y component of the vector.
        /// </summary>
        public float Y;

        /// <summary>
        /// The z component of the vector.
        /// </summary>
        public float Z;

        /// <summary>
        /// The heading.
        /// </summary>
        public float Heading;

        /// <summary>
        /// Whether this drop-off has cells.
        /// </summary>
        public bool HasCells;

        /// <summary>
        /// Whether this drop-off has officer cut-scene.
        /// </summary>
        public bool OfficerCutscene;

        /// <summary>
        /// Whether this drop-off is available to ground vehicles.
        /// </summary>
        public bool GroundVehicles;

        /// <summary>
        /// Whether this drop-off is available to air vehicles.
        /// </summary>
        public bool AirVehicles;

        /// <summary>
        /// Whether this drop-off is available to water vehicles.
        /// </summary>
        public bool WaterVehicles;

        /// <summary>
        /// Whether this drop-off is available to AI.
        /// </summary>
        public bool AIDropoff;


        /// <summary>Creates the blip.</summary>
        public void CreateBlip()
        {
            blip = new Blip(Position);
            blip.Sprite = BlipSprite.PlayerstateCustody;
            blip.Order = 11;

            NativeFunction.Natives.SET_BLIP_DISPLAY(blip, 3);
        }

        /// <summary>
        /// Determines whether the specified vehicle is suitable for dropoff.
        /// </summary>
        /// <param name="vehicle">The vehicle.</param>
        /// <returns>
        ///   <c>true</c> if the vehicle suitable for dropoff; otherwise, <c>false</c>.</returns>
        internal bool IsVehicleSuitableForDropoff(Vehicle vehicle)
        {
            if (vehicle)
            {
                if (vehicle.IsBoat)
                {
                    return WaterVehicles;
                }
                else if (vehicle.IsHelicopter || vehicle.IsPlane)
                {
                    return AirVehicles;
                }
                else
                {
                    return GroundVehicles;
                }
            }
            return false;
        }
    }
}
