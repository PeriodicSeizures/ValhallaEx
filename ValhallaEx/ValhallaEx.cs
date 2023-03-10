using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ValhallaEx
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    internal class ValhallaEx : BaseUnityPlugin
    {
        // BepInEx' plugin metadata
        public const string PluginGUID = "com.crzi.ValhallaEx";
        public const string PluginName = "ValhallaEx";
        public const string PluginVersion = "1.0.0";

        Harmony _harmony;

        bool m_announced = false;

        private void Awake()
        {
            Game.isModded = true;
            _harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), harmonyInstanceId: PluginGUID);

            ZLog.Log("Loading ValhallaEx");
        }

        private void Destroy()
        {
            _harmony?.UnpatchSelf();
        }

        private void Update()
        {
            if (!m_announced)
            {
                if (Console.instance && !Player.m_localPlayer && ZNet.instance)
                {
                    Console.instance.Print("(ValhallaEx) Custom command functionality enabled");

                    new Terminal.ConsoleCommand(".vs", "Execute custom Valhalla server commands", delegate (Terminal.ConsoleEventArgs args)
                    {
                        if (!(ZNet.instance.IsDedicated() && !ZNet.instance.IsServer()))
                        {
                            if (args.Args.Length > 1)
                            {
                                string cmd = args.Args[1];
                                List<string> list = args.Args.Skip(2).ToList();
                                ZNet.instance.GetServerRPC().Invoke("vs", cmd, list);
                            }
                            else
                            {
                                Console.instance.Print("(ValhallaEx) Input more args: .vs <command> [args...]");
                            }
                        } else
                        {
                            Console.instance.Print("(ValhallaEx) .vs can only be ran while connected to a server");
                        }
                    });

                    m_announced = true;
                }
            } 
            else if (Player.m_localPlayer)
            {
                m_announced = false;
            }
        }

        // Dungeon logging tools
        [HarmonyPatch(typeof(DungeonGenerator))]
        class DungeonGeneratorPatch
        {
            [HarmonyPostfix]
            [HarmonyPatch(nameof(DungeonGenerator.Generate), new Type[] { typeof(int), typeof(ZoneSystem.SpawnMode) })]
            static void GeneratePostfix(ref DungeonGenerator __instance)
            {
                ZLog.LogWarning("Dungeon '" + __instance.name
                    + "', seed: " + __instance.m_generatedSeed
                    + ", pos: " + __instance.transform.position
                    + ", rot: " + __instance.transform.rotation
                    + ", m_zoneCenter: " + __instance.m_zoneCenter
                    + ", m_originalPosition: " + __instance.m_originalPosition
                );
            }
        }

        // ZRpc infinite timeout patch
        [HarmonyPatch(typeof(ZRpc))]
        class ZRpcPatch
        {
            [HarmonyPrefix]
            [HarmonyPatch(nameof(ZRpc.Update))]
            static void GeneratePostfix()
            {
                ZRpc.m_timeout = 1 * 1000 * 60 * 60 * 24;
            }
        }

        // ZSteamSocket timeout patch
        [HarmonyPatch(typeof(ZSteamSocket))]
        class ZSteamSocketPatch
        {
            [HarmonyTranspiler]
            [HarmonyPatch(nameof(ZSteamSocket.RegisterGlobalCallbacks))]
            static IEnumerable<CodeInstruction> RegisterGlobalCallbacksTranspiler(IEnumerable<CodeInstruction> instructions)
            {
                return new CodeMatcher(instructions)
                    .MatchForward(useEnd: false, new CodeMatch(OpCodes.Ldc_R4, 30000f))
                    .SetAndAdvance(OpCodes.Ldc_R4, (float)(1 * 1000 * 60 * 60 * 24))
                    .InstructionEnumeration();
            }
        }

    }

}