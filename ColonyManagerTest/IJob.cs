using NPC;
using Pipliz;

namespace Nach0.ColonyManagement.Job
{
    public interface IJob
    {
        float NPCShopGameHourMinimum { get; }

        float NPCShopGameHourMaximum { get; }

        Colony Owner { get; }

        bool NeedsNPC { get; }

        InventoryItem RecruitmentItem { get; }

        NPCBase NPC { get; }

        NPCType NPCType { get; }

        bool IsValid { get; }

        NPCBase.NPCGoal CalculateGoal(ref NPCBase.NPCState state);

        Vector3Int GetJobLocation();

        void SetNPC(NPCBase npc);

        void OnNPCAtJob(ref NPCBase.NPCState state);

        void OnNPCAtStockpile(ref NPCBase.NPCState state);
    }
}