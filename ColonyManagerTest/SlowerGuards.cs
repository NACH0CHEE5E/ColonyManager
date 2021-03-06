﻿using Happiness;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pandaros.Settlers.ColonyManagement
{
    public class SlowerGuards : IHappinessEffect
    {
        public string GetDescription(Colony colony, Players.Player player)
        {
            var localizationHelper = new localization.LocalizationHelper("Happiness");
            var name = "";
            var cs = Entities.ColonyState.GetColonyState(colony);

            if (colony.HappinessData.CachedHappiness < 0)
            {
                float percent = 0.05f;

                if (colony.HappinessData.CachedHappiness < -20)
                    percent = 0.10f;

                if (colony.HappinessData.CachedHappiness < -50)
                    percent = 0.15f;

                if (colony.HappinessData.CachedHappiness < -70)
                    percent = 0.20f;

                if (colony.HappinessData.CachedHappiness < -100)
                    percent = 0.25f;

                percent = percent * cs.Difficulty.UnhappyGuardsMultiplyRate;

                foreach (var colonist in colony.Followers)
                {
                    colonist.ApplyJobResearch();

                    if (colonist.Job != null && colonist.Job.IsValid && colonist.TryGetNPCGuardSettings(out var guardJobSettings))
                        guardJobSettings.CooldownShot = guardJobSettings.CooldownShot - (guardJobSettings.CooldownShot * percent);
                }
                
                name = localizationHelper.LocalizeOrDefault("SlowGuards", player) + " " + Math.Round((percent * 100), 2) + "%";
            }
            else
            {
                foreach (var colonist in colony.Followers)
                    colonist.ApplyJobResearch();
            }

            return name;
        }
    }
}
