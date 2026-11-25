using System.Collections.Generic;
using UnityEngine;

// Client-only visuals of authoritative Health status entries, including late join.
public sealed class StatusVisuals : MonoBehaviour
{
    private Health health;
    private HeroSorting sorting;
    private readonly Dictionary<int, SpriteRenderer> world = new();
    private readonly Dictionary<int, GameObject> effects = new();
    private readonly Dictionary<int, FullScreenStatusOverlay> screen = new();
    private void OnEnable()
    {
        health = GetComponent<Health>();
        sorting = GetComponent<HeroSorting>();
        if (health != null) health.StatusChanged += Refresh;
        Refresh();
    }
    private void OnDisable()
    {
        if (health != null) health.StatusChanged -= Refresh;
        ClearVisuals();
    }
    public void ClearVisuals()
    {
        foreach (SpriteRenderer sprite in world.Values)
            if (sprite != null) Destroy(sprite.gameObject);
        foreach (GameObject effect in effects.Values)
            if (effect != null) Destroy(effect);
        foreach (FullScreenStatusOverlay overlay in screen.Values)
            if (overlay != null) Destroy(overlay.gameObject);
        world.Clear(); effects.Clear(); screen.Clear();
    }
    private void Refresh()
    {
        if (health == null || !health.IsSpawned || !health.IsClient) return;
        HashSet<int> active = new();
        for (int i = 0; i < health.StatusCount; i++)
        {
            StatusDefinition effect = health.StatusAt(i);
            if (effect == null) continue;
            active.Add(effect.id);
            if (effect.worldEffectPrefab != null && !effects.ContainsKey(effect.id))
            {
                Transform head = health.StatusHeadPoint;
                // Anchor at the HeadPoint with zero offset, but keep the VFX
                // prefab's authored local position (and particle settings).
                GameObject anchor = new($"Status VFX {effect.name}");
                anchor.transform.SetParent(head != null ? head : transform, false);
                GameObject instance = Instantiate(effect.worldEffectPrefab, anchor.transform, false);
                foreach (ParticleSystem particles in instance.GetComponentsInChildren<ParticleSystem>(true))
                    if (particles.gameObject.activeInHierarchy && !particles.isPlaying)
                        particles.Play(false);
                effects.Add(effect.id, anchor);
            }
            if (effect.worldOverlay != null && !world.ContainsKey(effect.id))
            {
                GameObject obj = new($"Status {effect.name}");
                Transform head = health.StatusHeadPoint;
                obj.transform.SetParent(head != null ? head : transform, false);
                obj.transform.localPosition = head != null ? Vector3.zero :
                    effect.worldOffset + health.StatusVisualOffset;
                SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
                renderer.sprite = effect.worldOverlay;
                renderer.sortingLayerID = SortingLayer.NameToID("Effect");
                renderer.sortingOrder = 30;
                world.Add(effect.id, renderer);
            }
            if (effect.screenOverlay != null && health.GetComponent<PlayerNetwork>() is PlayerNetwork player &&
                player.IsOwner)
            {
                if (!screen.TryGetValue(effect.id, out FullScreenStatusOverlay overlay) || overlay == null)
                {
                    overlay = FullScreenStatusOverlay.Create(effect.name, effect.screenOverlay,
                        effect.screenOpacity, effect.screenFadeIn, effect.screenFadeOut);
                    screen[effect.id] = overlay;
                }
                overlay.ShowUntil(health.StatusEndsAt(i));
            }
        }
        foreach (int id in new List<int>(world.Keys))
            if (!active.Contains(id)) { if (world[id] != null) Destroy(world[id].gameObject); world.Remove(id); }
        foreach (int id in new List<int>(effects.Keys))
            if (!active.Contains(id)) { if (effects[id] != null) Destroy(effects[id]); effects.Remove(id); }
        foreach (int id in new List<int>(screen.Keys))
            if (!active.Contains(id) && screen[id] != null) screen[id].Hide();
    }
    private void LateUpdate()
    {
        if (world.Count == 0) return;
        SpriteRenderer actor = sorting != null ? sorting.HeroRenderer : null;
        if (actor == null) return;
        foreach (SpriteRenderer sprite in world.Values)
        {
            if (sprite == null) continue;
            sprite.sortingLayerID = actor.sortingLayerID;
            sprite.sortingOrder = actor.sortingOrder + 2;
        }
    }
}
