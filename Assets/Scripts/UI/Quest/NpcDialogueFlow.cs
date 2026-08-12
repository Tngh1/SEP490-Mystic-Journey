using System;
using System.Collections.Generic;
using System.Linq;
using MysticJourney.API.Models.Response;

/// <summary>
/// Selects the configured dialogue sequence for one NPC interaction.
/// Advancing is intentionally explicit: callers must invoke TryAdvance in response to a choice.
/// </summary>
public static class NpcDialogueFlow
{
    public static List<NPCDialogueResponse> SelectSequence(
        IEnumerable<NPCDialogueResponse> dialogues,
        int selectedNpcId,
        int? linkedQuestId)
    {
        if (dialogues == null || selectedNpcId <= 0)
            return new List<NPCDialogueResponse>();

        var selectedNpcDialogues = dialogues
            .Where(d => d != null && d.IsActive && d.NPCId == selectedNpcId);

        if (linkedQuestId.HasValue && linkedQuestId.Value > 0)
            selectedNpcDialogues = selectedNpcDialogues.Where(d => d.LinkedQuestId == linkedQuestId.Value);
        else
            selectedNpcDialogues = selectedNpcDialogues.Where(d => !d.LinkedQuestId.HasValue);

        return selectedNpcDialogues
            .OrderBy(d => d.DisplayOrder)
            .ThenBy(d => d.NPCDialogueId)
            .ToList();
    }

    public static bool TryAdvance(
        IReadOnlyList<NPCDialogueResponse> configuredSequence,
        int currentDialogueId,
        out NPCDialogueResponse next)
    {
        next = null;
        if (configuredSequence == null || configuredSequence.Count == 0)
            return false;

        var currentIndex = -1;
        for (var i = 0; i < configuredSequence.Count; i++)
        {
            if (configuredSequence[i] != null &&
                configuredSequence[i].NPCDialogueId == currentDialogueId)
            {
                currentIndex = i;
                break;
            }
        }

        if (currentIndex < 0 || currentIndex + 1 >= configuredSequence.Count)
            return false;

        next = configuredSequence[currentIndex + 1];
        return next != null;
    }

    public static NPCDialogueResponse FindChoice(
        IEnumerable<NPCDialogueResponse> dialogues,
        int selectedNpcId,
        params string[] responseTypes)
    {
        if (dialogues == null || selectedNpcId <= 0 || responseTypes == null)
            return null;

        return dialogues
            .Where(d => d != null && d.IsActive && d.NPCId == selectedNpcId)
            .OrderBy(d => d.DisplayOrder)
            .ThenBy(d => d.NPCDialogueId)
            .FirstOrDefault(d => responseTypes.Any(type =>
                string.Equals(d.ResponseType, type, StringComparison.OrdinalIgnoreCase)));
    }
}
