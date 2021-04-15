using System;

namespace Arrest_Manager
{
    using LSPD_First_Response.Mod.API;
    using Rage;
    using System.Reflection;

    internal class Main : Plugin
    {
        
        public Main()
        {
            Albo1125.Common.UpdateChecker.VerifyXmlNodeExists(PluginName, FileID, DownloadURL, Path);
            Albo1125.Common.DependencyChecker.RegisterPluginForDependencyChecks(PluginName);   
        }
      
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
        internal static readonly Version Albo1125CommonVer = new Version("6.6.3.0");
        internal static readonly Version MadeForGTAVersion = new Version("1.0.1604.1");
        internal static readonly float MinimumRPHVersion = 0.51f;
        internal static readonly string[] AudioFilesToCheckFor = new string[] { "LSPDFR/audio/scanner/Arrest Manager Audio/Camera.wav" };
        internal static readonly Version RAGENativeUIVersion = new Version("1.6.3.0");
        internal static readonly Version MadeForLSPDFRVersion = new Version("0.4.8");
        internal static readonly string[] OtherFilesToCheckFor = new string[] { };

        internal static readonly string FileID = "8107";
#pragma warning disable S1075 // URIs should not be hardcoded
        internal static readonly string DownloadURL = "https://github.com/RelaperCrystal/Arrest-Manager";
#pragma warning restore S1075 // URIs should not be hardcoded
        internal static readonly string PluginName = "Arrest Manager+";
        internal static readonly string Path = "Plugins/LSPDFR/Arrest Manager.dll";

        public static void Functions_OnOnDutyStateChanged(bool onDuty)
        {
            if (onDuty && Albo1125.Common.DependencyChecker.DependencyCheckMain(PluginName, Albo1125CommonVer, MinimumRPHVersion, MadeForGTAVersion, MadeForLSPDFRVersion, RAGENativeUIVersion, AudioFilesToCheckFor, OtherFilesToCheckFor))
            {
                EntryPoint.Initialize();
            }
        }
    }
}