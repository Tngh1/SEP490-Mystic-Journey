using System;
using System.Collections.Generic;

namespace MysticJourney.API.Models
{
    // Initializes a new default instance of the FriendDto class.
    [Serializable]
    public class FriendDto
    {
        public int FriendshipId;
        public int FriendProfileId;
        public string FriendName;
        // Supported player classes: Knight, Archer, or Mage; the class selects base stats, compatible skills, skins, and combat scaling.
        public string Class;
        public int FriendLevel;
        public string FriendAvatarUrl;
        // Supported friendship states: Pending or Accepted; Pending is unanswered and Accepted is an active friendship.
        public string Status;

        public string CurrentMap;
        public bool IsInDungeon;
        public bool CanInvite;
        public string LastOnline;
        public bool IsOnline;
    }

    // Executes pending friend request dto operation.
    [Serializable]
    public class PendingFriendRequestDto
    {
        public int FriendshipId;
        public int RequesterId;
        public string RequesterName;
        public int RequesterLevel;
        public string RequesterAvatarUrl;
        // Supported player classes: Knight, Archer, or Mage; the class selects base stats, compatible skills, skins, and combat scaling.
        public string Class;
        public string CreatedAt;
    }

    // Executes friend profile dto operation.
    [Serializable]
    public class FriendProfileDto
    {
        public int ProfileId;
        public string CharacterName;
        // Supported player classes: Knight, Archer, or Mage; the class selects base stats, compatible skills, skins, and combat scaling.
        public string Class;
        public int Level;
        public int Power;
        public string Guild;
        public string AvatarUrl;
        // Executes title operation.
        public string Title { get; set; }
        // Executes last online operation.
        public string LastOnline { get; set; }
        // Executes is online operation.
        public bool IsOnline { get; set; }
        // Executes has changed name operation.
        public bool HasChangedName { get; set; }
    }

    // Executes friend relationship status operation.
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

    // Executes friend search dto operation.
    [Serializable]
    public class FriendSearchDto
    {
        public int ProfileId;
        public string CharacterName;
        public int Level;
        // Supported player classes: Knight, Archer, or Mage; the class selects base stats, compatible skills, skins, and combat scaling.
        public string Class;
        public string Avatar;
        public int Power;
        public string GuildName;
        public bool IsOnline;
        public FriendRelationshipStatus RelationshipStatus;
    }

    // Executes friend request payload operation.
    [Serializable]
    public class FriendRequestPayload
    {
        public int TargetProfileId;
    }
}
