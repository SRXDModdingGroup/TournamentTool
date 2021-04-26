using BepInEx;
using BepInEx.IL2CPP;
using HarmonyLib;
using BestHTTP;
using LitJson;

using UnityEngine;
using Steamworks;

namespace TournamentTool
{
    [BepInPlugin("TournamentTool", "TournamentTool", "0.1.2.0")]
    public class Main : BasePlugin
    {
        public static GameObject inLevel;

        public static bool PlayingTrack = false;

        public static string SteamName = "";

        public static string SteamID = "";

        public static string client = "51.195.138.4";

        public static int port = 11000;

        public static bool acceptingScores = false;

        public static LitJson. jsonObject;

        public static string jsonObjectString = "";

        public static JSONNode songList;

        public static int list;
        
        public override void Load()
        {
            Harmony.CreateAndPatchAll(typeof(plugins));
        }
        public class plugins
        {
            [HarmonyPatch(typeof(Track), "CompleteSong"), HarmonyPostfix]
            public static void CompleteSong_Postfix(Track __instance)
            {
                PlayingTrack = false;
                sendScoreData(score);
            }

            [HarmonyPatch(typeof(Track), "FailSong"), HarmonyPostfix]
            public static void FailSong_Postfix(Track __instance)
            {
                PlayingTrack = false;
                sendScoreData(score);
            }
            [HarmonyPatch(typeof(Track), "EnterPracticeMode"), HarmonyPostfix]
            public static void EnterPracticeMode_Postfix(Track __instance)
            {
                PlayingTrack = false;
                resetdata();
            }
            [HarmonyPatch(typeof(Track), "AddScoreIfPossible"), HarmonyPostfix]
            public static void AddScoreIfPossible_Postfix(Track __instance, PlayState playState, int pointsToAdd, int comboIncrease, NoteTimingAccuracy noteTimingAccuracy, float trackTime, int noteIndex)
            {
                if (PlayingTrack)
                {
                    score = SecuredInt.op_Implicit(playState.get_totalScore());
                    sendScoreData(SecuredInt.op_Implicit(playState.get_totalScore()));
                }
            }

        }

        public static void sendScoreData(int score)
        {
        }

        public static class scoreObj()
        {
            public static int missedint = 0;

            public static int multiplier = 1;

            public static int lateint = 0;

            public static int earlyint = 0;

            public static int perfectint = 0;

            public static int validint = 0;

            public static int score = 0;
        }
    }
}