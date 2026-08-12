/// <summary>
/// Deterministic authorization rules shared by Photon RPC handlers and multiplayer tests.
/// This class contains no client-local state; the StateAuthority remains the only writer.
/// </summary>
public static class PartyLifecycleRules
{
    public const int MaximumMembers = 4;

    public static bool CanJoin(int state, int memberCount, bool alreadyMember) =>
        state == 0 && !alreadyMember && memberCount >= 0 && memberCount < MaximumMembers;

    public static bool CanChangeReady(bool isMember, bool isHost) =>
        isMember && !isHost;

    public static bool CanKick(bool requesterIsHost, bool targetIsMember, bool targetIsHost) =>
        requesterIsHost && targetIsMember && !targetIsHost;

    public static bool CanLeave(bool isMember, bool isHost) =>
        isMember && !isHost;

    public static bool CanStartDungeon(
        bool requesterIsHost,
        int state,
        int memberCount,
        int readyCount,
        int pendingInviteCount) =>
        requesterIsHost &&
        state == 0 &&
        memberCount >= 2 &&
        memberCount <= MaximumMembers &&
        readyCount == memberCount &&
        pendingInviteCount <= 0;

    public static bool CanUsePartyChat(bool localIsMember, bool senderIsMember, int senderProfileId) =>
        localIsMember && senderIsMember && senderProfileId > 0;
}
