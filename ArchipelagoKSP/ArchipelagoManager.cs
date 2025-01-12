using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using UnityEngine;
using Archipelago.MultiClient.Net;
using ToolbarControl_NS;
using Archipelago.MultiClient.Net.Models;

namespace ArchipelagoKSP
{
    [KSPScenario(
        ScenarioCreationOptions.AddToNewScienceSandboxGames |
        ScenarioCreationOptions.AddToNewCareerGames |
        ScenarioCreationOptions.AddToExistingScienceSandboxGames |
        ScenarioCreationOptions.AddToExistingCareerGames,
        GameScenes.FLIGHT, GameScenes.TRACKSTATION, GameScenes.SPACECENTER, GameScenes.EDITOR
    )]
    class ArchipelagoManager : ScenarioModule
    {

        internal static ArchipelagoManager Instance;
        static internal ToolbarControl toolbarControl = null;
        internal ArchipelagoSession archipelagoSession;
        private PopupDialog dialog;
        internal const string MODID = "ArchipelagoKSP_NS";
        internal const string MODNAME = "ArchipelagoKSP";

        internal static readonly Regex expOnBody = new Regex(@".*@[A-Z][a-z]+", RegexOptions.Compiled);

        [KSPField(isPersistant = true)]
        private string serverAddress = "localhost:38281";

        [KSPField(isPersistant = true)]
        private string username = "user";

        [KSPField(isPersistant = true)]
        private string password = "";

        [KSPField(isPersistant = true)]
        private bool autoConnect = false;


        public void Update()
        {
            if (Input.GetKeyDown(KeyCode.F2))
            {
                Debug.Log("F2 pressed");

                var RnD = ResearchAndDevelopment.Instance;
                var subjects = ResearchAndDevelopment.GetSubjects();
                var rdNodes = KSP.UI.Screens.RDController.Instance.nodes;
                var rdTechs = rdNodes.Select(n => n.tech).ToList();
                var parts = rdTechs.SelectMany(t => t.partsAssigned).ToList();
            }
        }

        private void CreateWindow()
        {
            dialog = PopupDialog.SpawnPopupDialog(
                new MultiOptionDialog(
                    "ArchipelagoKSP",
                    "Connect to Archipelago server",
                    "ArchipelagoKSP",
                    HighLogic.UISkin,
                    new DialogGUIVerticalLayout(

                        new DialogGUILabel("Server:"),
                        new DialogGUITextInput(serverAddress, false, 100, (s) => serverAddress = s, 30),

                        new DialogGUILabel("User:"),
                        new DialogGUITextInput(username, false, 100, (s) => username = s, 30),

                        new DialogGUILabel("Password:"),
                        new DialogGUITextInput(password, false, 100, (s) => password = s, () => password, contentType: TMPro.TMP_InputField.ContentType.Password, 30),

                        new DialogGUIHorizontalLayout(
                            new DialogGUIToggle(autoConnect, "Auto-Connect", (b) => autoConnect = b),
                            new DialogGUIButton("Connect", () => Connect(), false),
                            new DialogGUIButton("Close", () => { }, true)
                        )

                    )
                ),
                false,
                HighLogic.UISkin
            );
            dialog.OnDismiss += () => dialog = null;
        }

        private void UnlockTech(string name)
        {
            Debug.Log("Unlocking tech: " + name);
            var tech = AssetBase.RnDTechTree.FindTech(name);
            if (tech == null)
            {
                Debug.LogError("Tech not found: " + name);
                return;
            }
            ResearchAndDevelopment.Instance.UnlockProtoTechNode(tech);
            ScreenMessages.PostScreenMessage("Tech unlocked: " + name, 5, ScreenMessageStyle.UPPER_CENTER);
        }

        internal void SubmitSubject(ScienceSubject subject)
        {
            try
            {
                var match = expOnBody.Match(subject.id);
                var id = archipelagoSession.Locations.GetLocationIdFromName("KerbalSpaceProgram", match.Value);
                Instance.archipelagoSession.Locations.CompleteLocationChecks(id);
            }
            catch (Exception e)
            {
                Debug.LogError("Error in ResearchAndDevelopment.SubmitScienceData: " + subject.id + e.Message);
            }
        }

        private void Connect()
        {

            if (archipelagoSession?.Socket.Connected ?? false)
            {
                Debug.Log("Already connected to Archipelago server");
                return;
            }

            Debug.Log("Connecting to Archipelago server");

            archipelagoSession = ArchipelagoSessionFactory.CreateSession(serverAddress);

            archipelagoSession.Items.ItemReceived += (receivedItemsHelper) =>
            {
                var name = receivedItemsHelper.PeekItemName();
                UnlockTech(name);
                receivedItemsHelper.DequeueItem();
            };

            archipelagoSession.MessageLog.OnMessageReceived += (msg) =>
            {
                Debug.Log("Message received from Archipelago server:" + msg);
                ScreenMessages.PostScreenMessage(msg.ToString(), 5, ScreenMessageStyle.UPPER_CENTER);
            };

            LoginResult result;
            try
            {
                result = archipelagoSession.TryConnectAndLogin(
                    "KerbalSpaceProgram",
                    username,
                    Archipelago.MultiClient.Net.Enums.ItemsHandlingFlags.AllItems,
                    password: password
                );
            }
            catch (Exception e)
            {
                result = new LoginFailure(e.GetBaseException().Message);
            }

            if (!result.Successful)
            {
                LoginFailure failure = (LoginFailure)result;
                Debug.LogError("Failed to connect to Archipelago server: " + string.Join(", ", ((LoginFailure)result).Errors));
                ScreenMessages.PostScreenMessage("Failed to connect to Archipelago server: " + string.Join(", ", ((LoginFailure)result).Errors), 5, ScreenMessageStyle.UPPER_CENTER);
                return;
            }

            foreach (NetworkItem item in archipelagoSession.Items.AllItemsReceived)
            {
                UnlockTech(archipelagoSession.Items.GetItemName(item.Item));
            }

            foreach (ScienceSubject subject in ResearchAndDevelopment.GetSubjects())
            {
                SubmitSubject(subject);
            }
        }

        private void AddToolbarButton()
        {
            if (toolbarControl == null)
            {

                toolbarControl = gameObject.AddComponent<ToolbarControl>();
                toolbarControl.AddToAllToolbars(
                    null,
                    null,
                    KSP.UI.Screens.ApplicationLauncher.AppScenes.ALWAYS,
                    MODID,
                    "archipelagoButton",
                    @"ArchipelagoKSP/PluginData/icon.png",
                    @"ArchipelagoKSP/PluginData/icon.png",
                    MODNAME
                );
                toolbarControl.AddLeftRightClickCallbacks(
                    () => { if (dialog == null) { CreateWindow(); } else { dialog?.Dismiss(); } },
                    () => { }
                );
            }
        }

        public override void OnAwake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("ArchipelagoManager already exists");
                Destroy(this);
            }
            else
            {
                Instance = this;
                AddToolbarButton();
                HarmonyLib.Harmony harmony = new HarmonyLib.Harmony("ArchipelagoKSP");
                harmony.PatchAll();
            }
        }

        public void OnDestroy()
        {
            if (toolbarControl != null)
            {
                toolbarControl.OnDestroy();
                Destroy(toolbarControl);
            }
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public override void OnSave(ConfigNode node)
        {
            // Nothing to save, as KSPField is persistant
        }

        public override void OnLoad(ConfigNode node)
        {
            if (autoConnect)
            {
                Connect();
            }
        }
    }

    // Pevent player from researching techs themselves
    [HarmonyLib.HarmonyPatch(typeof(RDTech), nameof(RDTech.ResearchTech))]
    internal class ArchipelagoRDTechResearchTech
    {
        private static bool Prefix(RDTech __instance, ref RDTech.OperationResult __result)
        {
            Debug.Log("You can't research techs yourself in Archipelago mode");
            ScreenMessages.PostScreenMessage("You can't research techs yourself in Archipelago mode", 5, ScreenMessageStyle.UPPER_CENTER);
            __result = RDTech.OperationResult.Failure;
            return false;
        }
    }

    // Hook science data submission for Archipelago location checks
    [HarmonyLib.HarmonyPatch(typeof(ResearchAndDevelopment), nameof(ResearchAndDevelopment.SubmitScienceData), new Type[] { typeof(float), typeof(float), typeof(ScienceSubject), typeof(float), typeof(ProtoVessel), typeof(bool) })]
    internal class ArchipelagoResearchAndDevelopmentSubmitScienceData
    {
        private static void Prefix(ResearchAndDevelopment __instance, ScienceSubject subject)
        {
            Debug.Log("ResearchAndDevelopment.SubmitScienceData: " + subject.id);
            ArchipelagoManager.Instance.SubmitSubject(subject);
        }
    }
}
