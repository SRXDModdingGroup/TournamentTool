using BepInEx;
using BepInEx.IL2CPP;
using HarmonyLib;
using SimpleJSON;
using UnityEngine;
using Steamworks;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System;
using System.Threading;

namespace TournamentTool
{
    [BepInPlugin("TournamentTool", "TournamentTool", "0.1.0.0")]
    public class Main : BasePlugin
    {
        public static BepInEx.Logging.ManualLogSource Logger;

        public override void Load()
        {
            Logger = Log;
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
                SpinTournament.SteamID = SteamUser.GetSteamID().ToString();
                SpinTournament.SteamName = SteamFriends.GetPersonaName();
                SpinTournament.jsonObjectString += "{";
                SpinTournament.jsonObjectString += "\"score\":0,";
                SpinTournament.jsonObjectString += "\"missed\":0,";
                SpinTournament.jsonObjectString += "\"early\":0,";
                SpinTournament.jsonObjectString += "\"late\":0,";
                SpinTournament.jsonObjectString += "\"perfect\":0,";
                SpinTournament.jsonObjectString += "\"valid\":0,";
                SpinTournament.jsonObjectString += "\"multiplier\":0,";
                SpinTournament.jsonObjectString = SpinTournament.jsonObjectString + "\"steamID\":\"" + SpinTournament.SteamID + "\",";
                SpinTournament.jsonObjectString = SpinTournament.jsonObjectString + "\"steamName\":\"" + SpinTournament.SteamName + "\"";
                SpinTournament.jsonObjectString += "}";
                SpinTournament.jsonObject = JSONNode.Parse(SpinTournament.jsonObjectString);
                Logger.LogMessage($"Welcome to the tournament, {SpinTournament.SteamName} (ID: {SpinTournament.SteamID}), GLHF!");
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
                SpinTournament.sendScoreData(SpinTournament.score);
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
                    SpinTournament.score = playState.scoreState.totalNoteScore._value;
                    SpinTournament.sendScoreData(SpinTournament.score);
                }
            }

            [HarmonyPatch(typeof(TrackGameplayFeedbackObjects), "PlayTimingFeedback"), HarmonyPrefix]

            // Token: 0x06000116 RID: 278 RVA: 0x00005258 File Offset: 0x00003458
            public static void PlayTimingFeedback_Prefix(PlayState playState, NoteTimingAccuracy noteTimingAccuracy)
            {
                bool flag = !SpinTournament.PlayingTrack;
                if (!flag)
                {
                    SpinTournament.jsonObject["multiplier"] = playState.multiplier;
                    bool flag2 = noteTimingAccuracy == NoteTimingAccuracy.Failed;
                    if (flag2)
                    {
                        SpinTournament.jsonObject["missed"] = SpinTournament.jsonObject["missed"] + 1;
                    }
                    else
                    {
                        bool flag3 = noteTimingAccuracy == NoteTimingAccuracy.Early;
                        if (flag3)
                        {
                            SpinTournament.jsonObject["early"] = SpinTournament.jsonObject["early"] + 1;
                        }
                        else
                        {
                            bool flag4 = noteTimingAccuracy == NoteTimingAccuracy.Late;
                            if (flag4)
                            {
                                SpinTournament.jsonObject["late"] = SpinTournament.jsonObject["late"] + 1;
                            }
                            else
                            {
                                bool flag5 = noteTimingAccuracy == NoteTimingAccuracy.Perfect;
                                if (flag5)
                                {
                                    SpinTournament.jsonObject["perfect"] = SpinTournament.jsonObject["perfect"] + 1;
                                }
                                else
                                {
                                    bool flag6 = noteTimingAccuracy == NoteTimingAccuracy.Valid;
                                    if (flag6)
                                    {
                                        SpinTournament.jsonObject["valid"] = SpinTournament.jsonObject["valid"] + 1;
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
            public static void timerTick(object state) {
                canSend = true;
                timer.Change(Timeout.Infinite, Timeout.Infinite);
                if (triedToSendInInvalidPeriod)
                {
                    Thread.Sleep(msInterval/2);
                    if (!t.IsAlive)
                    {
                        sendDataThread(new { score });
                    }
                }
                triedToSendInInvalidPeriod = false;
            }

            public static int msInterval = 500;
            public static Timer timer = new Timer(timerTick, null, Timeout.Infinite, Timeout.Infinite);
            public static bool canSend = true;
            public static bool triedToSendInInvalidPeriod = false;

            public static Thread t = new Thread(new ParameterizedThreadStart(sendDataThread));

            public static bool PlayingTrack = false;

            public static string SteamName = "";

            public static string SteamID = "";

            public static string client = "51.195.138.4";

            public static int port = 11000;

            public static int missedint = 0;

            public static int multiplier = 1;

            public static int lateint = 0;

            public static int earlyint = 0;

            public static int perfectint = 0;

            public static int validint = 0;

            public static int score = 0;

            public static bool acceptingScores = false;

            public static JSONNode jsonObject;

            public static string jsonObjectString = "";

            /*public static JSONNode songList;*/

            // Token: 0x04000034 RID: 52
            /*public static int list;*/
            public static void resetdata()
            {
                SpinTournament.jsonObject["missed"] = 0;
                SpinTournament.jsonObject["early"] = 0;
                SpinTournament.jsonObject["late"] = 0;
                SpinTournament.jsonObject["perfect"] = 0;
                SpinTournament.jsonObject["valid"] = 0;
                SpinTournament.jsonObject["multiplier"] = 1;
                SpinTournament.sendScoreData(0);
            }

            public static void sendScoreData(int score)
            {
                bool flag = !SpinTournament.acceptingScores;
                if (!flag)
                {
                    if (canSend) {
                        canSend = false;
                        timer.Change(msInterval, Timeout.Infinite);

                        if (!t.IsAlive)
                        {
                            sendDataThread(new { score });
                        }
                    }
                    else
                    {
                        triedToSendInInvalidPeriod = true;
                    }
                }
            }
            /*public static void sendDataBypass(int score)
            {
                SpinTournament.jsonObject["score"] = score;
                UdpClient udpClient = new UdpClient(SpinTournament.client, SpinTournament.port);
                string str = SpinTournament.jsonObject.ToString();
                Logger.LogMessage(str);
                byte[] bytes = Encoding.ASCII.GetBytes("%%DataStart%%" + str + "%%DataEnd%%");
                try
                {
                    udpClient.SendAsync(bytes, bytes.Length);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex.ToString());
                }
            }*/
            public static void sendDataThread(object sender)
            {
                dynamic dSender = sender;
                SpinTournament.jsonObject["score"] = dSender.score;
                UdpClient udpClient = new UdpClient(SpinTournament.client, SpinTournament.port);
                string str = SpinTournament.jsonObject.ToString();
                Logger.LogMessage(str);
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