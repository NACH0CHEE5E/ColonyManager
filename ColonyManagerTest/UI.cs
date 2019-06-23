using Pipliz;

using Chatting;
using NetworkUI;
using NetworkUI.Items;

using System.Collections.Generic;
using System.Linq;
using Pipliz.JSON;
using System.IO;
using NPC;
using Jobs;

namespace Nach0.ColonyManagement
{
    //open ui with command
    [ChatCommandAutoLoader]
    public class ColonyUICommand : IChatCommand
    {
        public bool TryDoCommand(Players.Player player, string chat, List<string> splits)
        {
            if (player == null)
                return false;

            if (!chat.Equals("?nach0manager"))
                return false;

            Dictionary<string, JobCounts> jobCounts = SendColonyUI.GetJobCounts(player.ActiveColony);
            NetworkMenuManager.SendServerPopup(player, SendColonyUI.BuildMenu(player, jobCounts, false, string.Empty, 0));

            return true;
        }
    }

    public class JobCounts
    {
        public string Name { get; set; }
        public int AvailableCount { get; set; }
        public int TakenCount { get; set; }
        public List<IJob> AvailableJobs { get; set; } = new List<IJob>();
        public List<IJob> TakenJobs { get; set; } = new List<IJob>();
    }

    //ui drawing/button press
    [ModLoader.ModManager]
    public class SendColonyUI
    {
        //on push button
        [ModLoader.ModCallback(ModLoader.EModCallbackType.OnPlayerPushedNetworkUIButton, "NACH0.ColonyManagement.UIButton.Pressed")]
        public static void PressButton(ButtonPressCallbackData data)
        {
            if ((!data.ButtonIdentifier.Contains(".RecruitButton") &&
                !data.ButtonIdentifier.Contains(".FireButton") &&
                !data.ButtonIdentifier.Contains(".SwapJob") &&
                !data.ButtonIdentifier.Contains(".ColonyToolMainMenu") &&
                !data.ButtonIdentifier.Contains(".KillFired") &&
                !data.ButtonIdentifier.Contains(".AutoRecruitment")) || data.Player.ActiveColony == null)
                return;

            Dictionary<string, JobCounts> jobCounts = GetJobCounts(data.Player.ActiveColony);
            Dictionary<Players.Player, string> AutoRecruit_Opt = new Dictionary<Players.Player, string>();

            if (data.ButtonIdentifier.Contains(".ColonyToolMainMenu"))
            {
                NetworkMenuManager.SendServerPopup(data.Player, BuildMenu(data.Player, jobCounts, false, string.Empty, 0));
            }
            else if (data.ButtonIdentifier.Contains(".FireButton"))
            {
                foreach (var job in jobCounts)
                    if (data.ButtonIdentifier.Contains(job.Key))
                    {
                        var recruit = data.Storage.GetAs<int>(job.Key + ".Recruit");
                        var count = GetCountValue(recruit);
                        var menu = BuildMenu(data.Player, jobCounts, true, job.Key, count);

                        menu.LocalStorage.SetAs(Nach0Config.NAMESPACE + ".FiredJobName", job.Key);
                        menu.LocalStorage.SetAs(Nach0Config.NAMESPACE + ".FiredJobCount", count);

                        NetworkMenuManager.SendServerPopup(data.Player, menu);
                        break;
                    }
            }
            else if (data.ButtonIdentifier.Contains(".KillFired"))
            {
                var firedJob = data.Storage.GetAs<string>(Nach0Config.NAMESPACE + ".FiredJobName");
                var count = data.Storage.GetAs<int>(Nach0Config.NAMESPACE + ".FiredJobCount");

                foreach (var job in jobCounts)
                {
                    if (job.Key == firedJob)
                    {
                        if (count > job.Value.TakenCount)
                            count = job.Value.TakenCount;

                        for (int i = 0; i < count; i++)
                        {
                            var npc = job.Value.TakenJobs[i].NPC;
                            npc.ClearJob();
                            npc.OnDeath();
                        }

                        break;
                    }
                }

                data.Player.ActiveColony.SendCommonData();
                jobCounts = GetJobCounts(data.Player.ActiveColony);
                NetworkMenuManager.SendServerPopup(data.Player, BuildMenu(data.Player, jobCounts, false, string.Empty, 0));
            }
            else if (data.ButtonIdentifier.Contains(".SwapJob"))
            {
                var firedJob = data.Storage.GetAs<string>(Nach0Config.NAMESPACE + ".FiredJobName");
                var count = data.Storage.GetAs<int>(Nach0Config.NAMESPACE + ".FiredJobCount");

                foreach (var job in jobCounts)
                    if (data.ButtonIdentifier.Contains(job.Key))
                    {
                        if (count > job.Value.AvailableCount)
                            count = job.Value.AvailableCount;

                        if (jobCounts.TryGetValue(firedJob, out var firedJobCounts))
                        {
                            for (int i = 0; i < count; i++)
                            {
                                if (firedJobCounts.TakenCount > i)
                                {
                                    var npc = firedJobCounts.TakenJobs[i].NPC;
                                    npc.ClearJob();
                                    npc.TakeJob(job.Value.AvailableJobs[i]);
                                    data.Player.ActiveColony.JobFinder.Remove(job.Value.AvailableJobs[i]);
                                }
                                else
                                    break;
                            }
                        }

                        data.Player.ActiveColony.SendCommonData();
                        break;
                    }

                jobCounts = GetJobCounts(data.Player.ActiveColony);
                NetworkMenuManager.SendServerPopup(data.Player, BuildMenu(data.Player, jobCounts, false, string.Empty, 0));
            }
            else if (data.ButtonIdentifier.Contains(".RecruitButton"))
            {
                foreach (var job in jobCounts)
                    if (data.ButtonIdentifier.Contains(job.Key))
                    {
                        var recruit = data.Storage.GetAs<int>(job.Key + ".Recruit");
                        var count = GetCountValue(recruit);

                        if (count > job.Value.AvailableCount)
                            count = job.Value.AvailableCount;

                        for (int i = 0; i < count; i++)
                        {
                            var num = 0f;
                            data.Player.ActiveColony.HappinessData.RecruitmentCostCalculator.GetCost(data.Player.ActiveColony.HappinessData.CachedHappiness, data.Player.ActiveColony, out float foodCost);
                            if (data.Player.ActiveColony.Stockpile.TotalFood < foodCost ||
                                !data.Player.ActiveColony.Stockpile.TryRemoveFood(ref num, foodCost))
                            {
                                //PandaChat.Send(data.Player, _localizationHelper.LocalizeOrDefault("Notenoughfood", data.Player), ChatColor.red);
                                Chat.Send(data.Player, "<color=red>Notenoughfood</color>");
                                break;
                            }
                            else
                            {
                                var newGuy = new NPCBase(data.Player.ActiveColony, data.Player.ActiveColony.GetClosestBanner(new Vector3Int(data.Player.Position)).Position);
                                data.Player.ActiveColony.RegisterNPC(newGuy);
                                //SettlerInventory.GetSettlerInventory(newGuy);
                                NPCTracker.Add(newGuy);
                                //ModLoader.TriggerCallbacks(ModLoader.EModCallbackType.OnNPCRecruited, newGuy);

                                if (newGuy.IsValid)
                                {
                                    newGuy.TakeJob(job.Value.AvailableJobs[i]);
                                    data.Player.ActiveColony.JobFinder.Remove(job.Value.AvailableJobs[i]);
                                }
                            }
                        }


                        data.Player.ActiveColony.SendCommonData();

                        jobCounts = GetJobCounts(data.Player.ActiveColony);
                        NetworkMenuManager.SendServerPopup(data.Player, BuildMenu(data.Player, jobCounts, false, string.Empty, 0));
                    }
            }
            else if (data.ButtonIdentifier.Contains(".AutoRecruitment"))
            {
                if (AutoRecruit_Opt[data.Player].Equals("on"))
                {
                    AutoRecruit_Opt[data.Player] = "off";
                    Chat.Send(data.Player, "<color=blue>Auto Recruitment is now " + AutoRecruit_Opt[data.Player] + "</color>");
                }
                else
                {
                    AutoRecruit_Opt[data.Player] = "on";
                    Chat.Send(data.Player, "<color=blue>Auto Recruitment is now " + AutoRecruit_Opt[data.Player] + "</color>");
                }
            }
        }

        static readonly localization.LocalizationHelper _localizationHelper = new localization.LocalizationHelper("ColonyManagementUI");

        public static int GetCountValue(int countIndex)
        {
            var value = _recruitCount[countIndex];
            int retval = int.MaxValue;

            if (int.TryParse(value, out int count))
                retval = count;

            return retval;
        }

        public static List<string> _recruitCount = new List<string>()
        {
            "1",
            "5",
            "10",
            "Max"
        };

        public static NetworkMenu BuildMenu(Players.Player player, Dictionary<string, JobCounts> jobCounts, bool fired, string firedName, int firedCount)
        {
            //UI Settings
            NetworkMenu menu = new NetworkMenu();
            menu.LocalStorage.SetAs("header", _localizationHelper.LocalizeOrDefault("ColonyManagement", player));
            menu.Width = 1000;
            menu.Height = 600;

            //if fire colonist has been slected
            if (fired)
            {
                var count = firedCount.ToString();

                if (firedCount == int.MaxValue)
                    count = "all";

                //kill colonist button
                menu.Items.Add(new ButtonCallback(Nach0Config.BUTTON_NAMESPACE + ".KillFired", new LabelData(_localizationHelper.LocalizeOrDefault("KillColonist", player), UnityEngine.Color.black, UnityEngine.TextAnchor.MiddleCenter)));
            } else
            {
                menu.Items.Add(new ButtonCallback(Nach0Config.BUTTON_NAMESPACE + ".AutoRecruitment", new LabelData(_localizationHelper.GetLocalizationKey("AutoRecruitment"), UnityEngine.Color.black, UnityEngine.TextAnchor.MiddleCenter)));
            }

            menu.Items.Add(new Line());

            //Standard UI
            //Header
            List<(IItem, int)> header = new List<(IItem, int)>();

            header.Add((new Label(new LabelData(_localizationHelper.LocalizeOrDefault("Job", player), UnityEngine.Color.black)), 140)); //Job Title

            if (!fired)
            {
                header.Add((new Label(new LabelData(_localizationHelper.LocalizeOrDefault("Working", player), UnityEngine.Color.black)), 140)); //Working amount title (only Shows if fired not selected)
            }
            header.Add((new Label(new LabelData(_localizationHelper.LocalizeOrDefault("NotWorking", player), UnityEngine.Color.black)), 140)); //not working amount title
            header.Add((new Label(new LabelData("", UnityEngine.Color.black)), 140));
            header.Add((new Label(new LabelData("", UnityEngine.Color.black)), 140));

            menu.Items.Add(new HorizontalRow(header));

            //add jobs
            int jobCount = 0;

            foreach (var jobKvp in jobCounts)
            {
                if (fired && jobKvp.Value.AvailableCount == 0)
                    continue;

                jobCount++;
                List<(IItem, int)> items = new List<(IItem, int)>();

                items.Add((new Label(new LabelData(_localizationHelper.LocalizeOrDefault(jobKvp.Key.Replace(" ", ""), player), UnityEngine.Color.black)), 140));

                if (!fired)
                {

                }

                items.Add((new Label(new LabelData(jobKvp.Value.TakenCount.ToString(), UnityEngine.Color.black)), 140));
                items.Add((new Label(new LabelData(jobKvp.Value.AvailableCount.ToString(), UnityEngine.Color.black)), 140));

                if (fired)
                {
                    items.Add((new ButtonCallback(jobKvp.Key + ".SwapJob", new LabelData(_localizationHelper.LocalizeOrDefault("SwapJob", player), UnityEngine.Color.black, UnityEngine.TextAnchor.MiddleLeft)), 140));
                }
                else
                {
                    items.Add((new DropDown(new LabelData(_localizationHelper.LocalizeOrDefault("LblRecruit", player), UnityEngine.Color.black), jobKvp.Key + ".Recruit", _recruitCount), 140));
                    items.Add((new ButtonCallback(jobKvp.Key + ".RecruitButton", new LabelData(_localizationHelper.LocalizeOrDefault("Recruit", player), UnityEngine.Color.black, UnityEngine.TextAnchor.MiddleCenter)), 140));
                    items.Add((new ButtonCallback(jobKvp.Key + ".FireButton", new LabelData(_localizationHelper.LocalizeOrDefault("Fire", player), UnityEngine.Color.black, UnityEngine.TextAnchor.MiddleCenter)), 140));

                }

                menu.LocalStorage.SetAs(jobKvp.Key + ".Recruit", 0);

                menu.Items.Add(new HorizontalRow(items));
            }

            if (jobCount == 0)
                menu.Items.Add(new Label(new LabelData(_localizationHelper.LocalizeOrDefault("NoJobs", player), UnityEngine.Color.black)));

            return menu;
        }

        public static Dictionary<string, JobCounts> GetJobCounts(Colony colony)
        {
            Dictionary<string, JobCounts> jobCounts = new Dictionary<string, JobCounts>();
            var jobs = colony?.JobFinder?.JobsData?.OpenJobs;
            var npcs = colony?.Followers;

            if (jobs != null)
                foreach (var job in jobs)
                {
                    if (NPCType.NPCTypes.TryGetValue(job.NPCType, out var nPCTypeSettings))
                    {
                        if (!jobCounts.ContainsKey(nPCTypeSettings.PrintName))
                            jobCounts.Add(nPCTypeSettings.PrintName, new JobCounts() { Name = nPCTypeSettings.PrintName });

                        jobCounts[nPCTypeSettings.PrintName].AvailableCount++;
                        jobCounts[nPCTypeSettings.PrintName].AvailableJobs.Add(job);
                    }
                }


            if (npcs != null)
                foreach (var npc in npcs)
                {
                    if (npc.Job != null && npc.Job.IsValid && NPCType.NPCTypes.TryGetValue(npc.Job.NPCType, out var nPCTypeSettings))
                    {
                        if (!jobCounts.ContainsKey(nPCTypeSettings.PrintName))
                            jobCounts.Add(nPCTypeSettings.PrintName, new JobCounts() { Name = nPCTypeSettings.PrintName });

                        jobCounts[nPCTypeSettings.PrintName].TakenCount++;
                        jobCounts[nPCTypeSettings.PrintName].TakenJobs.Add(npc.Job);
                    }
                }

            return jobCounts;
        }
    }
}
