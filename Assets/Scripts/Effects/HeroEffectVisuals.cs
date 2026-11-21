using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Server-owned effect state; each client creates its own following visuals.
// Prefab bindings are shared by every hero using Player.prefab.
[DisallowMultipleComponent]
public class HeroEffectVisuals : NetworkBehaviour
{
    [Serializable]
    private struct EffectVisual
    {
        public ScriptableObject source;
        public GameObject prefab;
        public Vector3 localOffset;
    }

    [SerializeField] private EffectVisual[] effects;

    private readonly NetworkList<int> activeEffects = new();
    private readonly Dictionary<int, GameObject> instances = new();
    private readonly List<GameObject> finishingInstances = new();
    private Health health;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        activeEffects.OnListChanged += OnEffectsChanged;
        if (IsServer)
        {
            health = GetComponent<Health>();
            if (health != null)
                health.Died += ClearActiveEffects;
        }
        RefreshVisuals(); // Includes effects already active when joining late.
    }

    public override void OnNetworkDespawn()
    {
        activeEffects.OnListChanged -= OnEffectsChanged;
        if (health != null)
            health.Died -= ClearActiveEffects;
        ClearVisuals();
        base.OnNetworkDespawn();
    }

    public void SetActive(ScriptableObject source, bool active)
    {
        if (!IsServer || !IsSpawned || source == null || effects == null)
            return;

        for (int i = 0; i < effects.Length; i++)
        {
            if (effects[i].source != source)
                continue;
            bool alreadyActive = activeEffects.Contains(i);
            if (active && !alreadyActive)
                activeEffects.Add(i);
            else if (!active && alreadyActive)
                activeEffects.Remove(i);
        }
    }

    private void ClearActiveEffects()
    {
        if (IsServer && IsSpawned)
            activeEffects.Clear();
    }

    private void OnEffectsChanged(NetworkListEvent<int> change)
    {
        RefreshVisuals();
    }

    private void RefreshVisuals()
    {
        if (!IsClient || effects == null)
            return;

        for (int i = 0; i < effects.Length; i++)
        {
            if (!activeEffects.Contains(i))
            {
                if (instances.TryGetValue(i, out GameObject oldInstance))
                {
                    if (oldInstance != null)
                        StopVisual(oldInstance);
                    instances.Remove(i);
                }
                continue;
            }

            if (instances.ContainsKey(i) || effects[i].prefab == null)
                continue;

            // Preserve the authored rotation/scale (including particle emitters).
            GameObject instance = Instantiate(effects[i].prefab, transform, false);
            instance.transform.localPosition += effects[i].localOffset;
            instances.Add(i, instance);
            foreach (ParticleSystem particles in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                // Play explicitly: buff prefabs may have Play On Awake disabled.
                if (particles.gameObject.activeInHierarchy)
                    particles.Play(false);
            }
        }
    }

    private void StopVisual(GameObject instance)
    {
        ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
        if (systems.Length == 0)
        {
            Destroy(instance);
            return;
        }

        // The whole effect must outlive every emitter, even if a child has an
        // authored Destroy/Disable stop action. Do not clear existing particles.
        foreach (ParticleSystem particles in systems)
        {
            var main = particles.main;
            main.stopAction = ParticleSystemStopAction.None;
        }
        foreach (ParticleSystem particles in systems)
            particles.Stop(false, ParticleSystemStopBehavior.StopEmitting);

        finishingInstances.Add(instance);
        StartCoroutine(FinishVisual(instance, systems));
    }

    private IEnumerator FinishVisual(GameObject instance, ParticleSystem[] systems)
    {
        while (instance != null)
        {
            bool alive = false;
            foreach (ParticleSystem particles in systems)
            {
                if (particles != null && particles.IsAlive(false))
                {
                    alive = true;
                    break;
                }
            }
            if (!alive)
                break;
            yield return null;
        }

        finishingInstances.Remove(instance);
        if (instance != null)
            Destroy(instance);
    }

    private void ClearVisuals()
    {
        StopAllCoroutines();
        foreach (GameObject instance in instances.Values)
            if (instance != null)
                Destroy(instance);
        instances.Clear();
        foreach (GameObject instance in finishingInstances)
            if (instance != null)
                Destroy(instance);
        finishingInstances.Clear();
    }
}
