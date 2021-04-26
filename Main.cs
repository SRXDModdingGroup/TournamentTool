using BepInEx;
using BepInEx.IL2CPP;
using HarmonyLib;
using BestHTTP;
using LitJson;
using SimpleJSON;
using UnhollowerBaseLib;
using UnityEngine;
using Steamworks;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System;

namespace TournamentTool
{
    [BepInPlugin("TournamentTool", "TournamentTool", "0.1.0.0")]
    public class Main : BasePlugin
    {
        public static BepInEx.Logging.ManualLogSource Logger;

        public static scoreObj scoreObject;

        public override void Load()
        {
            Logger = Log;
            scoreObject = new scoreObj();
            using (WebClient webClient = new WebClient())
            {
                string aJSON = webClient.DownloadString("https://www.questboard.xyz/SpinShare/acceptingScores.json");
                JSONNode jsonnode = JSONNode.Parse(aJSON);
                SpinTournament.acceptingScores = jsonnode["acceptingScores"].AsBool;
            }
            Harmony.CreateAndPatchAll(typeof(plugins));
        }    

        public class plugins
        {
            [HarmonyPatch(typeof(GameStateManager), nameof(GameStateManager.Awake)), HarmonyPostfix]
            public static void OnLevelWasInitialized()
            {
                scoreObject.steamID = SteamUser.GetSteamID().ToString();
                scoreObject.steamName = SteamFriends.GetPersonaName();
                SpinTournament.resetdata();


                Logger.LogMessage(scoreObject.steamID);
                Logger.LogMessage(JsonMapper.ToJson(scoreObject).ToString());
            }

            [HarmonyPatch(typeof(Track), "PlayTrack"), HarmonyPostfix]
            // Token: 0x06000114 RID: 276 RVA: 0x00005209 File Offset: 0x00003409
            public static void PlayTrack_Postfix(Track __instance)
            {
                SpinTournament.PlayingTrack = true;
                SpinTournament.resetdata();
            }

            [HarmonyPatch(typeof(Track), "CompleteSong"), HarmonyPostfix]
            [HarmonyPatch(typeof(Track), "FailSong")]
            public static void EndSongPostfix(Track __instance)
            {
                SpinTournament.PlayingTrack = false;
                SpinTournament.sendScoreData(scoreObject.score);
            }

            [HarmonyPatch(typeof(Track), "EnterPracticeMode"), HarmonyPostfix] 
            [HarmonyPatch(typeof(Track), "StopTrack")]
            public static void SetPlayingTrackAsFalseAndReset(Track __instance)
            {
                SpinTournament.PlayingTrack = false;
                SpinTournament.resetdata();
            }

            [HarmonyPatch(typeof(TrackGameplayLogic), nameof(TrackGameplayLogic.AddScoreIfPossible)), HarmonyPostfix]
            public static void AddScoreIfPossible_Postfix(Track __instance, PlayState playState, int pointsToAdd, int comboIncrease, NoteTimingAccuracy noteTimingAccuracy, float trackTime, int noteIndex)
            {
                if (SpinTournament.PlayingTrack)
                {
                    scoreObject.score = playState.scoreState.totalNoteScore._value;
                    SpinTournament.sendScoreData(scoreObject.score);
                }
            }

            [HarmonyPatch(typeof(TrackGameplayFeedbackObjects), "PlayTimingFeedback"), HarmonyPrefix]

            // Token: 0x06000116 RID: 278 RVA: 0x00005258 File Offset: 0x00003458
            public static void PlayTimingFeedback_Prefix(PlayState playState, NoteTimingAccuracy noteTimingAccuracy)
            {
                bool flag = !SpinTournament.PlayingTrack;
                if (!flag)
                {
                    scoreObject.multiplier = playState.multiplier;
                    bool flag2 = noteTimingAccuracy == NoteTimingAccuracy.Failed;
                    if (flag2)
                    {
                        scoreObject.missed++;
                    }
                    else
                    {
                        bool flag3 = noteTimingAccuracy == NoteTimingAccuracy.Early;
                        if (flag3)
                        {
                            scoreObject.early++;
                        }
                        else
                        {
                            bool flag4 = noteTimingAccuracy == NoteTimingAccuracy.Late;
                            if (flag4)
                            {
                                scoreObject.late++;
                            }
                            else
                            {
                                bool flag5 = noteTimingAccuracy == NoteTimingAccuracy.Perfect;
                                if (flag5)
                                {
                                    scoreObject.perfect++;
                                }
                                else
                                {
                                    bool flag6 = noteTimingAccuracy == NoteTimingAccuracy.Valid;
                                    if (flag6)
                                    {
                                        scoreObject.valid++;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        static class SpinTournament
        {
            public static GameObject inLevel;

            // Token: 0x04000024 RID: 36
            public static bool PlayingTrack = false;

            // Token: 0x04000027 RID: 39
            public static string client = "51.195.138.4";

            // Token: 0x04000028 RID: 40
            public static int port = 11000;

            // Token: 0x04000030 RID: 48
            public static bool acceptingScores = false;

            // Token: 0x04000033 RID: 51
            /*public static JSONNode songList;*/

            // Token: 0x04000034 RID: 52
            /*public static int list;*/
            public static void resetdata()
            {
                scoreObject.missed = 0;
                scoreObject.early = 0;
                scoreObject.late = 0;
                scoreObject.perfect = 0;
                scoreObject.valid = 0;
                scoreObject.multiplier = 0;
                SpinTournament.sendScoreData(0);
            }

            public static void sendScoreData(int score)
            {
                bool flag = !SpinTournament.acceptingScores;
                if (!flag)
                {
                    scoreObject.score = score;
                    UdpClient udpClient = new UdpClient(SpinTournament.client, SpinTournament.port);
                    string str = JsonMapper.ToJson(scoreObject).ToString();
                    byte[] bytes = Encoding.ASCII.GetBytes("%%DataStart%%" + str + "%%DataEnd%%");
                    try
                    {
                        udpClient.SendAsync(bytes, bytes.Length);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex.ToString());
                    }
                }
            }
        }        
    }
}