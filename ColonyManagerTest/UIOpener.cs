using NetworkUI;
using Pipliz;
using Shared;
using System.Collections.Generic;
using UnityEngine;
using Chatting;

namespace Nach0.ColonyManagement
{
    [ModLoader.ModManager]
    public class ItemPlace
    {
        static string itemName = "NACH0.Types.ColonyManager";
        
        [ModLoader.ModCallback(ModLoader.EModCallbackType.OnPlayerClicked, "NACH0.UI.ItemPlace.OnPlayerClick")]
        public static void PlaceItem(Players.Player player, PlayerClickedData data)
        {
           
            if (data.TypeSelected != ItemTypes.GetType(itemName).ItemIndex)
            {
                return;
            } else if (data.TypeSelected == ItemTypes.GetType(itemName).ItemIndex)
            {
                if (data.ClickType == PlayerClickedData.EClickType.Left)
                {
                    Dictionary<string, JobCounts> jobCounts = SendColonyUI.GetJobCounts(player.ActiveColony);
                    NetworkMenuManager.SendServerPopup(player, SendColonyUI.BuildMenu(player, jobCounts, false, string.Empty, 0));
                }
            }
        }
    }
}
