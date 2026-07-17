using System;
using System.Collections.Generic;

namespace MysticJourney.API.Models
{
    [Serializable]
    public class FriendDto
    {
        public int FriendshipId;
        public int FriendProfileId;
        public string FriendName;
        public string Class;
        public int FriendLevel;
        public string FriendAvatarUrl;
        public string Status;
        
        // New features
        public string CurrentMap;
        public bool IsInDungeon;
        public bool CanInvite;
        public string LastOnline;
        public bool IsOnline;
    }

    [Serializable]
    public class PendingFriendRequestDto
    {
        public int FriendshipId;
        public int RequesterId;
        public string RequesterName;
        public int RequesterLevel;
        public string RequesterAvatarUrl;
        public string Class;
        public string CreatedAt;
    }

    [Serializable]
    public class FriendProfileDto
    {
        public int ProfileId;
        public string CharacterName;
        public string Class;
        public int Level;
        public int Power;
        public string Guild;
        public string AvatarUrl;
        public string Title { get; set; }
        public string LastOnline { get; set; }
        public bool IsOnline { get; set; }
        public bool HasChangedName { get; set; }
    }

    [Serializable]
    public enum FriendRelationshipStatus
    {
        Self,
        None,
        RequestSent,
        RequestReceived,
        Friend,
        Blocked
    }

    [Serializable]
    public class FriendSearchDto
    {
        public int ProfileId;
        public string CharacterName;
        public int Level;
        public string Class;
        public string Avatar;
        public int Power;
        public string GuildName;
        public bool IsOnline;
        public FriendRelationshipStatus RelationshipStatus;
    }

    [Serializable]
    public class FriendRequestPayload
    {
        public int TargetProfileId;
    }
}
