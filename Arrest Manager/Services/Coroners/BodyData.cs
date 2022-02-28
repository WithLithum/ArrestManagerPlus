using LSPD_First_Response;
using Rage;
using System;

using VFuncs = LSPD_First_Response.Mod.API.Functions;

namespace Arrest_Manager.Services.Coroners
{
    internal class BodyData
    {
        internal BodyData(Ped p, string cause)
        {
            IsCop = VFuncs.IsPedACop(p);

            var persona = VFuncs.GetPersonaForPed(p);
            Name = persona.FullName;
            Gender = persona.Gender;
            DateOfBirth = persona.Birthday;
            CauseOfDeath = cause;
        }

        internal bool IsCop { get; }

        internal string Name { get; }

        internal Gender Gender { get; }

        internal DateTime DateOfBirth { get; }

        internal string CauseOfDeath { get; }

        internal void DisplayNotification()
        {
            Game.DisplayNotification("mpinventory", "mp_specitem_keycard",
                "Coroner Report",
                Name,
                $"~b~{Gender}~s~, born ~y~{DateOfBirth.ToShortDateString()}~n~~b~Is Cop: ~y~{IsCop}~n~~b~Cause: ~c~{CauseOfDeath}");
        }
    }
}
