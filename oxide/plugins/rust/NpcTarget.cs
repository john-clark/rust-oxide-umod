namespace Oxide.Plugins
{
    [Info("NPC Target", "Iv Misticos", "1.0.0")]
	[Description("Deny NPCs target other NPCs")]
    class NpcTarget : RustPlugin
    {
        private object OnNpcTarget(BaseNpc npc, BaseEntity entity)
        {
            if (entity.IsNpc)
                return false;
            return null;
        }

        private object OnNpcPlayerTarget(NPCPlayerApex npcPlayer, BaseEntity entity)
        {
            if (entity.IsNpc)
                return false;
            return null;
        }
    }
}