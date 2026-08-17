using MysticJourney.API.Models.Response;

namespace MysticJourney.API.Models.Response
{
    // Initializes a new default instance of the CharacterResponse class.
    [System.Serializable]
    public class CharacterResponse
    {
        // Executes player profile id operation.
        public int PlayerProfileId { get; set; }
        // Executes account id operation.
        public int AccountId { get; set; }
        // Executes character name operation.
        public string CharacterName { get; set; }
        // Executes player class operation.
        public string PlayerClass { get; set; }
        // Executes level operation.
        public int Level { get; set; }
        // Executes experience points operation.
        public int ExperiencePoints { get; set; }
        // Executes gold operation.
        public decimal Gold { get; set; }
        // Executes gems operation.
        public decimal Gems { get; set; }
        // Executes energy operation.
        public int Energy { get; set; }
        // Executes max energy operation.
        public int MaxEnergy { get; set; }
        // Executes last energy update time operation.
        public string LastEnergyUpdateTime { get; set; }
        // Executes created at operation.
        public string CreatedAt { get; set; }
        // Executes stats operation.
        public PlayerStatsResponse Stats { get; set; }
    }


}

namespace MysticJourney.API.Models.Request
{
    // Executes create character request operation.
    [System.Serializable]
    public class CreateCharacterRequest
    {
        // Executes character name operation.
        public string CharacterName { get; set; }

        // Executes selected class operation.
        public string SelectedClass { get; set; }
    }

}
