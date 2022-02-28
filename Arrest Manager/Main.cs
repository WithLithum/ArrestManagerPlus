using System;
using LSPD_First_Response.Mod.API;
using Rage;
using System.Reflection;

namespace Arrest_Manager
{
    internal class Main : Plugin
    {
        public override void Finally()
        {
        }

        public override void Initialize()
        {
            //Event handler for detecting if the player goes on duty
            Game.LogTrivial("Arrest Manager " + Assembly.GetExecutingAssembly().GetName().Version.ToString() + ", developed by Albo1125, loaded successfully!");

            Game.LogTrivial("Please go on duty to start Arrest Manager.");

            Functions.OnOnDutyStateChanged += Functions_OnOnDutyStateChanged;
        }

        public static void Functions_OnOnDutyStateChanged(bool onDuty)
        {
            if (onDuty)
            {
                EntryPoint.Initialize();
            }
        }
    }
}