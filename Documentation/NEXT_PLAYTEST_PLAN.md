# Dream MOBA — plan after the 2v2 playtest

Recorded: 2026-11-14; updated 2026-11-15. Some early slices are implemented and user-playtested; later economy and upgrade work remains planned.

## Priority 1 — movement and network quality

- Reproduce the micro-teleports with host + two remote clients, including different latency/jitter and movement near walls/doors. Record RTT, packet loss, server tick/frame time, client frame time and correction frequency; compare what the owner, host and other clients see. Separate actual network latency from rendering stutter and movement corrections.
- Source inspection identified competing dynamic client physics and network interpolation, plus redundant transform data. A first source/prefab correction now makes remote player bodies kinematic, keeps one interpolation path, sends XY position deltas unreliably and stops duplicating the projectile's separately synchronized scale. This is **not** a measured gameplay fix until tested on remote clients.
- Reproduce and measure RTT, packet loss, server tick/frame time, client frame time and correction frequency; then test owner responsiveness, remote smoothness, collisions, camera, building teleports, respawns and 2v2 load. If server-authoritative movement is still sluggish at real RTT, design client-side prediction with reconciliation separately; do not mask corrections with arbitrary smoothing.

## Priority 2 — shared visibility, edge indicators and minimap

- Agreed team-visibility rules, object categories, low-resolution static-map capture, camera rectangle outlines and implementation stages are in `Documentation/VISIBILITY_MINIMAP_PLAN.md`. Team vision is the union of actual per-player/spectator camera rectangles; there is no wall occlusion or last-known-position marker at present.
- A first UI-level shared camera feed, minimap markers, and screen-edge arrows are in source; the user confirmed the map and arrow visuals, but full host/client visibility checks remain. Verify different aspect ratios, building transitions, spectating and immediate loss of enemy markers on both UI elements.

## Priority 3 — economy and scarecrow rewards

- Add server-owned per-player gold, synchronized to HUD, retained across player despawn/respawn by client ID; determine initial balance, UI and future spending rules. Avoid awarding the same kill twice.
- On each scarecrow spawn, track valid player attackers and time of their last actual damage on the server. On death, determine the team of the killing blow; award a configurable amount of gold to **every player on that team** (including those who did not attack). Separately, players of that team who dealt damage in the preceding 10 seconds receive one energy drink directly in inventory. Decide how to handle a full inventory before implementation (e.g. drop the reward at the house, rather than silently deleting it). Clear the contribution window between spawns and test delayed/projectile hits, disconnected players and respawns.
- Create the consumable energy drink: +15% movement speed for 15 seconds after use, with authoritative duration and stackable speed modifiers so it combines correctly with Guitarist abilities and future flag effects. Grant another drink on respawn to speed up the return to play without forcing immediate activation; apply the same full-inventory policy. Test repeated use and effects across death/respawn.

## Priority 4 — signature hero upgrades

- Design a reusable hero-specific upgrade track: three levels per hero that strengthen that hero's own mechanic, rather than generic stats; server owns unlocked level, HUD shows level/next effect, and progress survives respawn. Define the Guitarist's three concrete upgrades with balance values before coding effects. Future heroes provide their own definitions.
- First level is awarded through the scarecrow objective. Proposed rule: eligible members of the killing team who damaged it in the last 10 seconds unlock level 1; **confirm whether it should instead go to the whole winning team**, and how levels 2/3 are earned. Prevent duplicate unlocks if the scarecrow respawns.

## Priority 5 — flag comeback mechanic and gradual map work

- While a team carries the flag, grant **the opposing team** a server-controlled movement-speed bonus that increases as the carrier approaches the delivery zone; choose minimum/maximum percentages and distance curve after a playtest. Recalculate for pickup, throw, death drop, return and delivery, and ensure there is no lingering bonus. Combine it safely with drink and hero speed buffs; keep it from enabling uncontrollable chase speed.
- Grow map content iteratively alongside playtests (routes, cover, landmarks, objectives and visual readability); check collisions, spawn safety, camera zones and navigation each time. This is ongoing level design rather than one blocking feature.

## Recommended first slice

The first movement correction made 2v2 play substantially smoother according to the user; network-jitter profiling remains a follow-up. The map background, UI-level visibility, minimap and arrows now have initial implementations (see `Documentation/VISIBILITY_MINIMAP_PLAN.md`); verify them in host/client play before expanding. Next economy/consumables should precede scarecrow payouts and signature upgrades.
