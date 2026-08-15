using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ProtectiveShieldSkill : MonoBehaviour
{
    [SerializeField] private float radius = 5f;
    [SerializeField] private float duration = 5f;
    [SerializeField] private float defenseShareRatio = 0.5f;
    [SerializeField] private AudioClip castSound;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;

    private Transform _targetToFollow;

    private IEnumerator Start()
    {
        // Replica markers are attached immediately after Instantiate on remote clients.
        // Delay the gameplay/broadcast logic so a visual copy cannot apply the buff again.
        yield return null;

        if (castSound != null && MysticJourney.Core.Services.AudioManager.Instance != null)
        {
            MysticJourney.Core.Services.AudioManager.Instance.PlaySfx(castSound, soundVolume);
        }
        PlayerCombat casterCombat = null;
        Transform replicaOwner = PlayerSkillVisualReplica.GetOwner(this);
        
        // Find the caster (the local player where this was spawned)
        Collider2D[] casterHits = Physics2D.OverlapCircleAll(transform.position, 0.5f);
        foreach (var hit in casterHits)
        {
            var combat = hit.GetComponent<PlayerCombat>();
            if (combat != null && combat.gameObject.CompareTag("Player"))
            {
                casterCombat = combat;
                break;
            }
        }

        if (replicaOwner != null)
        {
            casterCombat = replicaOwner.GetComponent<PlayerCombat>();
        }
        else if (casterCombat == null && PlayerEntity.Instance != null)
        {
            casterCombat = PlayerEntity.Instance.GetComponent<PlayerCombat>();
        }

        if (casterCombat != null)
        {
            _targetToFollow = casterCombat.transform;
        }
        else
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) _targetToFollow = playerObj.transform;
        }

        if (PlayerSkillVisualReplica.IsReplica(this))
        {
            Destroy(gameObject, duration);
            yield break;
        }

        float casterDef = casterCombat != null ? casterCombat.TotalDef : 0f;
        float buffAmount = casterDef * defenseShareRatio;

        // Apply to all players in radius. A player may have several colliders, so
        // de-duplicate by PlayerCombat before applying stats or broadcasting VFX.
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius);
        var affectedPlayers = new HashSet<PlayerCombat>();
        bool broadcastNetworkVisual = false;
        foreach (var hit in hits)
        {
            var player = hit.GetComponentInParent<PlayerCombat>();
            if (player != null && player.CompareTag("Player") && affectedPlayers.Add(player))
            {
                player.AddDefBuff(buffAmount, duration);
                player.AddDebuffImmunity(duration);

                var networkPlayer = player.GetComponent<NetworkPlayer>();
                if (networkPlayer != null && networkPlayer.Object != null)
                {
                    string prefabName = gameObject.name.EndsWith("(Clone)")
                        ? gameObject.name.Substring(0, gameObject.name.Length - "(Clone)".Length)
                        : gameObject.name;
                    networkPlayer.RPC_ShowBuffVisual(prefabName);
                    broadcastNetworkVisual = true;
                }

                // Show a text popup for buff
                if (DamagePopupManager.Instance != null)
                {
                    DamagePopupManager.Instance.Create(player.transform.position + Vector3.up * 1f, (int)buffAmount, false, false);
                }
            }
        }

        // Network clients render the per-target visual created by the RPC above.
        // Remove the original cast object so the caster does not see two shields.
        if (broadcastNetworkVisual)
        {
            Destroy(gameObject);
            yield break;
        }

        Destroy(gameObject, duration);
    }

    private void Update()
    {
        if (_targetToFollow != null)
        {
            transform.position = _targetToFollow.position;
        }
    }
}
