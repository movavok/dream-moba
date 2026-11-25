# Following hero effects

`HeroEffectVisuals` is a reusable NetworkBehaviour on `Assets/Prefabs/Player.prefab`. Existing ability visuals (`IAbilityVisual` / PlayerAbilities) and damage feedback remain separate.

## Bindings

Player prefab > HeroEffectVisuals > Effects contains entries with:

- **Source**: the ScriptableObject identifying the effect (for an energy drink, `EnergyDrinkUse.asset`, not the item definition).
- **Prefab**: an ordinary local visual prefab; it does not need a NetworkObject or network prefab registration.
- **Local Offset**: an additional offset relative to the selected attachment point (hero root by default). The visual retains the prefab's authored local position, rotation, scale, materials and sorting settings.
- **Attachment Point**: optional Transform on the hero for this binding (otherwise the hero root); the instantiated prefab follows it.

The soap binding uses `Assets/Prefabs/Effects/bublesParticles.prefab`. EnergyDrinkUse uses `Assets/Prefabs/Effects/EnergyPixelLightning.prefab` attached to the existing Player > Body > LegsPoint. The bubble prefab and its resources remain available for soap.

Open `Assets/Prefabs/Effects/EnergyPixelLightning.prefab` in Prefab Mode and select its root `PixelLightningVisual`. Its Inspector exposes **Feet** (the effect prefab's root Transform), **Lightning Count** (2 by default), **Start Color** (white), **End Color** (yellow), **Width** (0.0625), **Min/Max Length**, **Length Per Speed**, **Segments** (8), **Deviation**, **Refresh Rate** (14 Hz), **Pixels Per Unit** (16), **Material** (`EnergyPixelLightning`, URP 2D Sprite Unlit), **Sorting Layer** (Effect) and **Order In Layer** (1, above Hero). These are the editable VFX settings; the root `GameObject` layer is not the renderer's Sorting Layer. To move the origin, adjust the existing LegsPoint or change **Attachment Point** on Player > HeroEffectVisuals > Effects > EnergyDrinkUse in the Inspector.

Two LineRenderers are created once when the prefab appears and reused across shape refreshes. Their **world-space** trail follows the recent positions of the hero's feet: old trail positions stay in the world while the first vertex follows the current feet every frame, so the lines bend through turns. Each independently refreshes its jagged offsets; sharp pixel-grid steps remain. Length follows the client's observed hero-root displacement, including replicated movement; facing and aim are not consulted. At rest the trail shrinks to the minimum length in the last travel direction.

## Lifecycle and networking

- Server-side `SetActive(source, true/false)` changes a server-owned NetworkList of active binding indices.
- Clients instantiate at most one local visual per active binding, parented to the hero. Multiple different effects can run together.
- The list's initial synchronized state also creates visuals for clients joining during an active effect. A dedicated server creates no visual instances.
- `TimedStatEffects` activates/deactivates bindings with the same source used for stat modifiers. Energy drink therefore shows lightning during the actual 15-second modifier, without a second visual timer.
- Expiry/removal stops particle emission with `StopEmitting`: existing particles finish naturally, then the visual is destroyed once all its particle systems are no longer alive. Finishing instances keep following the hero and do not prevent a new activation. Non-particle visuals are removed immediately.
- Server-side death clears the active visual list. Network despawn immediately cleans up both active and finishing instances.
- Lightning uses the same active-effect list for all observers and late joiners; its vertex coordinates are never synchronized. Expiry and death remove the local prefab when the replicated binding clears.
- Adding another timed stat effect requires another binding, not another particle-specific script. Other server-owned systems can call the same SetActive API.

## Verification

Unity compiled Assembly-CSharp, including HeroEffectVisuals and TimedStatEffects, and completed Netcode IL post-processing successfully. Play Mode/host-client visuals have not been verified.

Next checks: soap starts bubbles; the drink starts two sharp white-to-yellow stair-step lines above the hero at LegsPoint on host and remote clients; verify that recent trail positions stay in place through turns, speed-dependent length, short stationary lines, 15-second expiry, death/respawn and late join. Adjust attachment point and sorting near buildings in the Inspector if needed.
