using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace HaveMoreKids.Framework;

internal static class MultiplayerSync
{
    private const string ChildToNPCMsg = "ChildToNPC";
    private const string ModConfigMsg = "ModConfig";

    internal static void Register()
    {
        // multiplayer
        ModEntry.help.Events.Multiplayer.PeerConnected += OnPeerConnected;
        ModEntry.help.Events.Multiplayer.ModMessageReceived += OnModMessageReceived;
    }

    internal static void SendKidEntries(long[]? playerIDs)
    {
        if (!Context.IsMultiplayer)
            return;
        ModEntry.Log("Send ChildToNPC");
        ModEntry.help.Multiplayer.SendMessage(KidHandler.KidEntries, ChildToNPCMsg, [ModEntry.ModId], playerIDs);
    }

    internal static void SendModConfig(long[]? playerIDs)
    {
        if (!Context.IsMultiplayer)
            return;
        ModEntry.Log("Send ModConfig");
        ModEntry.help.Multiplayer.SendMessage(
            ModEntry.Config as ModConfigValues,
            ModConfigMsg,
            [ModEntry.ModId],
            playerIDs
        );
    }

    internal static void OnModMessageReceived(object? sender, ModMessageReceivedEventArgs e)
    {
        if (!Context.IsMainPlayer && e.FromModID == ModEntry.ModId)
        {
            ModEntry.Log($"Recv {e.Type}");
            switch (e.Type)
            {
                case ChildToNPCMsg:
                    KidHandler.KidEntries_FromHost(e.ReadAs<Dictionary<string, KidEntry>>());
                    break;
                case ModConfigMsg:
                    ModEntry.Config.SyncAndUnregister(e.ReadAs<ModConfigValues>());
                    break;
            }
        }
    }

    internal static void OnPeerConnected(object? sender, PeerConnectedEventArgs e)
    {
        if (Context.IsMainPlayer)
        {
            long[] connecting = [e.Peer.PlayerID];
            SendKidEntries(connecting);
            SendModConfig(connecting);
        }
    }
}
