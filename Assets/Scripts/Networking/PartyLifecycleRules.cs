// Initializes a new default instance of the PartyLifecycleRules class.
public static class PartyLifecycleRules
{
    public const int MaximumMembers = 4;

    // Executes can join operation.
    public static bool CanJoin(int state, int memberCount, bool alreadyMember) =>
        state == 0 && !alreadyMember && memberCount >= 0 && memberCount < MaximumMembers;

    // Executes can change ready operation.
    public static bool CanChangeReady(bool isMember, bool isHost) =>
        isMember && !isHost;

    // Executes can kick operation.
    public static bool CanKick(bool requesterIsHost, bool targetIsMember, bool targetIsHost) =>
        requesterIsHost && targetIsMember && !targetIsHost;

    // Executes can leave operation.
    public static bool CanLeave(bool isMember, bool isHost) =>
        isMember && !isHost;

    // Evaluate start dungeon using requester is host, state, member count, and ready count and returns the computed result.
    public static bool CanStartDungeon(
        bool requesterIsHost,
        int state,
        int memberCount,
        int readyCount,
        int pendingInviteCount)
    {
        _ = pendingInviteCount;
        return requesterIsHost &&
               state == 0 &&
               memberCount >= 2 &&
               memberCount <= MaximumMembers &&
               readyCount == memberCount;
    }

    // Executes can use party chat operation.
    public static bool CanUsePartyChat(bool localIsMember, bool senderIsMember, int senderProfileId) =>
        localIsMember && senderIsMember && senderProfileId > 0;
}
