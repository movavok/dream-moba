# USUALS shop setup

`Assets/Prefabs/Map/Chest.prefab` points at `Assets/Prefabs/UI/chestUI.prefab` and `Assets/Scripts/Items/UsualsCatalog.asset`. The current `Grid.prefab` includes a Chest prefab instance; verify its placement in the loaded GameScene. The Chest prefab is registered with the NetworkManager. Its interaction collider and `ShopChest` remain on the network root.

`UsualsCatalog` now has all six tier-I item positions: Attack (Lighter, RottenTomato), Survival (Pillow, Soap), Mobility & Control (EnergyDrink, BananaPeel), initially 50 gold each. Other positions have **no item**. Slot frames remain visible, but empty item icons are transparent at runtime; empty positions do not pulse or buy. All 24 positions are supplied by the existing chestUI layout. `ShopPanelView` uses the four category references and the `lvl1`/`lvl2`/`lvl3` > `Slot1`... layout anchors; the signature category has three direct slots. `ShopSlot.prefab` is instantiated at every anchor so slot styling stays shared.

## Adding an item

1. Create or reuse an actual `ItemDefinition` and register it in `ItemDatabase.asset` (also used by the player Inventory). Configure its name, description, icon and behavior there as usual.
2. In `UsualsCatalog.asset` add an Offer and set **Category**, **Tier** (I/II/III; Signature always I), **Position** (1–2 on I/III, 1–3 on II/Signature), **Item**, and **Price**. **Icon Override** replaces only the slot icon; the Buy-area preview automatically takes the sprite from the item's WorldItem prefab. If that sprite is missing, it falls back to the inventory icon; **Preview Override** allows a separate Inspector sprite. Do not enter duplicate positions. Empty positions need no Offer.
3. Adjust common slot frame, icon, locked/available/selected/poor colors, pulse and unlock duration on `ShopSlot.prefab`. Adjust the panel and its category icons, preview, gold icon and button imagery directly on `chestUI.prefab`. Price 0 is supported.

Purchases and level gating are checked on `ShopChest` by the server, with `EconomyManager.TrySpendGold`, `Inventory.CanAdd` and `Inventory.TryAdd`. `ShopProgress` stores each client's independent category progression for the match, outside the respawning inventory. Tier I purchase unlocks II; II unlocks III; Signature has no sequential unlock. The client receives only its own progress snapshot and successful-purchase notification. Match manager respawn resets the match progress; a disconnected client's progress is removed.

## Reusing UI effects

- `UIReveal` + `CanvasGroup`: call `Show`/`Hide` for a scale/fade panel transition (unscaled time, interrupted safely).
- `UIGroupTint`: call `SetDisabled` on a button or panel to recolor every nested UI `Graphic`, including TMP text and icon, while preserving transparency.
- `UIPress`: call `Play` on a button to trigger a short scale response.
- `UICoinBurst`: call `Play` for one nonblocking, UI-space burst; set any Sprite, color, count, radius, size and duration in Inspector. Here it reuses the coin sprite from PiggyBank's `coin.mat`; the original world `ParticleSystemRenderer` cannot draw above an Overlay Canvas without a separate camera/render pipeline.

These components are ordinary MonoBehaviours; hook their public methods up to a Unity Button `On Click` or an Animator event in other interfaces. They have no shop dependency. `ShopSlotView` manages only the shared item slot states and is shop-specific.

## User Play Mode checklist

Confirm chest opens only for the interacting client; Escape/range exit/death closes it; quick open/close leaves no stuck panel; the 24 anchors and transparent empty icons look right at your resolution; a purchase succeeds with 50+ gold and free capacity; zero/insufficient gold grays the entire button; a full inventory, locked tier, empty slot and out-of-range purchase do nothing; money/inventory/unlocks stay consistent on host and remote client; using/dropping a purchased item and respawning preserves category progression; a successful purchase alone plays the coin burst and unlock transition. Unity import, compilation and host/client Play Mode verification are left to the user.
