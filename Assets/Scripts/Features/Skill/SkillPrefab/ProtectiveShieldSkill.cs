using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// Executes mono behaviour operation.
public class ProtectiveShieldSkill : MonoBehaviour
{
    [SerializeField] private float radius = 5f;
    [SerializeField] private float duration = 5f;
    [SerializeField] private float defenseShareRatio = 0.5f;
    [SerializeField] private AudioClip castSound;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;

    private Transform _targetToFollow;

    // Performs startup initialization for ProtectiveShieldSkill on the first active frame.
    // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
    private IEnumerator Start()
    {
        yield return null;

        if (castSound != null && MysticJourney.Core.Services.AudioManager.Instance != null)
        {
            MysticJourney.Core.Services.AudioManager.Instance.PlaySfx(castSound, soundVolume);
        }
        PlayerCombat casterCombat = null;
        Transform replicaOwner = PlayerSkillVisualReplica.GetOwner(this);

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

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius);
        var affectedPlayers = new HashSet<PlayerCombat>();
        bool broadcastNetworkVisual = false;
        foreach (var hit in hits)
        {
            var player = hit.GetComponentInParent<PlayerCombat>();
            if (player != null && player.CompareTag("Player") && affectedPlayers.Add(player))
            {
                var networkPlayer = player.GetComponent<NetworkPlayer>();
                if (networkPlayer != null && networkPlayer.Object != null)
                {
                    networkPlayer.RPC_ApplyDefBuff(buffAmount, duration);
                    networkPlayer.RPC_ApplyDebuffImmunity(duration);

                    string prefabName = gameObject.name.EndsWith("(Clone)")
                        ? gameObject.name.Substring(0, gameObject.name.Length - "(Clone)".Length)
                        : gameObject.name;
                    networkPlayer.RPC_ShowBuffVisual(prefabName);
                    broadcastNetworkVisual = true;
                }
                else
                {
                    player.AddDefBuff(buffAmount, duration);
                    player.AddDebuffImmunity(duration);
                }

                if (DamagePopupManager.Instance != null)
                {
                    DamagePopupManager.Instance.Create(player.transform.position + Vector3.up * 1f, (int)buffAmount, false, false);
                }
            }
        }

        if (broadcastNetworkVisual)
        {
            Destroy(gameObject);
            yield break;
        }

        Destroy(gameObject, duration);
    }

    // Per-frame update loop for ProtectiveShieldSkill.
    // Handles real-time input polling, smooth interpolations, cooldown timers, and UI updates.
    private void Update()
    {
        if (_targetToFollow != null)
        {
            transform.position = _targetToFollow.position;
        }
    }
}
