using Rage;
using BetterEMS.API;

namespace Arrest_Manager.API
{
    internal static class BetterEmsFunctions
    {
        public static uint GetOriginalDeathWeaponAssetHash(Ped p)
        {
            if (p && p.IsDead)
            {
                return EMSFunctions.GetOriginalDeathWeaponAsset(p).Hash;
            }
            else
            {
                return 0;
            }
            
        }

        public static bool HasBeenTreated(Ped p)
        {
            return EMSFunctions.DidEMSRevivePed(p) != null;
        }
    }
}
