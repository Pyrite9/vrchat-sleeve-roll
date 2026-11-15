# Pyrite9 Sleeve Roll

Version 0.1.3 (2026-11-15)

A sleeve-rolling gimmick for VRChat avatars. Drop the prefab into an avatar and pick the sleeved garment — when you build, the rolled-up sleeve is worked out from that garment's own mesh.
Turn the Expression Menu toggle on and both sleeves roll up, and others see it too. No shape keys or mesh data come with this package — the shape is made at build time from the garment you own.

한국어: `README_ko.md` / 日本語: `README_jp.md` / License: `LICENSE.txt`

## Features

- An Expression Menu toggle rolls both sleeves up in 0.2 s — others see it too (1 synced bit). Put the toggle in the root or any submenu, and choose whether its state is saved
- The rolled shape is worked out at build time from the garment you own — it measures the skin so the cloth doesn't sink into the arm, keeps the cloth's own thickness and some of its slack, and adds folds where it bunches up
- Where the outfit shrinks the arm under the sleeve, it releases the shrink while rolled (also a shrink at 0 at build that an outfit toggle turns on) — no thin arm shows inside the rolled cuff
- The rolled cuff stays put when you bend or twist the wrist (a wrist helper bone)
- Sleeves with a PhysBone (moe sleeves) keep swaying as usual and hold still only while rolled
- Layered outfits (a shirt under a jacket): pick several garments — they are rolled from the inside out, and the outer sleeve sits over the inner one's rolled cuff
- Your scene and the garment's mesh file are not changed — only the build copy is
- In the inspector, pick the garment from buttons, set where the cuff sits, the gap and the folds with gauges, and see where the cuff ends up as a gold ring in the Scene view
- See the rolled sleeves in the Scene view without building (**Preview** in **How far it rolls**) — same calculation as the build. For an outfit put on at build time (Modular Avatar and the like), whether a sleeve with a PhysBone rolls and keeps its physics is decided by the build, so check **Last build** and the build warnings for that

## Requirements

- Unity 2022.3 (VRChat)
- VRChat SDK – Avatars 3.10 or later
- Modular Avatar
- NDMF (Non-Destructive Modular Framework)

Tested with: Unity 2022.3.22f1, VRChat SDK Avatars 3.10.5, Modular Avatar 1.18.7, NDMF 1.14.8.
Older versions and preview versions (alpha, beta) are not tested.

- Humanoid avatars only (the elbow and wrist are found from the Humanoid bones).
- Tested outfits: Salt's default hoodie, Kipfel's shirt, Milfy's cardigan, and Kaguya's cardigan layered over her sailor top. **Tested on PC.** Android / Quest is not tested. Layering was tested with that one pair.
- PhysBone sleeves: tested on one outfit (Kaguya's cardigan) with a PhysBone under the hand bone, in Unity Play mode and inside VRChat, including another player's view and that player rejoining the instance.

## Install

1. **Add the required packages.** In VCC / ALCOM, open your avatar project with "Manage Project" and add **Modular Avatar**.
   NDMF comes with Modular Avatar. Use stable versions (not previews).
2. **Import this package.** Double-click the `.unitypackage` (or Unity's `Assets > Import Package > Custom Package…`), leave everything checked and click **Import**. The files go to `Assets/Pyrite9/SleeveRoll/`.

## Usage

1. In the Project window open `Assets/Pyrite9/SleeveRoll/` and drag `Pyrite9 Sleeve Roll.prefab` onto your avatar (the object with the VRC Avatar Descriptor) in the Hierarchy. Put it directly under the avatar. **Outside the avatar nothing happens.**
   - Don't unpack the prefab (updates would not reach it).
2. In the inspector's first section (Basics):
   - **Menu language** (`EN` / `日本語` / `한국어`) sets the in-game menu name and the inspector's language (default EN).
   - **Garments**: this avatar's sleeved garments are listed as buttons — click the one to roll (click again to drop it). For layered outfits click several. If it isn't listed, drag the garment's Skinned Mesh Renderer into the **Not listed** field below the buttons. Pick the **garment** mesh, not the body.
   - **Sleeves**: Both (default) / Left / Right.
3. (Optional) In **How far it rolls** and **Rolled shape**, set where the cuff sits, the gap to the skin, the sleeve width and the folds. The gold ring in the Scene view is where the cuff ends up. Set **Preview** to **ON** to see the rolled sleeves right there while you adjust; it changes nothing in your scene or your upload. Double-click a gauge to reset it.
4. Try it:
   - Press **Play** in Unity: the rolled sleeve is built and the inspector's **Last build** is filled in (shown after you stop Play). During Play, turn the Expression Menu toggle on with Gesture Manager or similar.
   - `VRChat SDK > Show Control Panel > Builder` → **Build & Test**, then switch to the test avatar in VRChat. Build & Test shows only your own view — how others see it can be checked after uploading (as Private).
5. Check the result. When all is well, **Last build** shows the garment and, per side, where the cuff ended up (e.g. `to 0.30`), and **Warnings: none**. Layered garments are listed from the inside out, an outer one marked `over …`.
   - Warnings also appear in the Console as yellow lines starting with `[Pyrite9 Sleeve Roll]` — see "Troubleshooting". Details are written to `Logs/Pyrite9_SleeveRoll_build.txt` in the project folder.

For layered garments (a jacket over a shirt), pick both in one Sleeve Roll. The build tells inner from outer by the forearm girth (the thinner one is inside). To give each garment its own settings you can also add another Sleeve Roll and pick the other garment there — the layering still fits together, and one toggle rolls both. If the inner one then fails with `is thinner than the skin` (an inner garment shrunk with shape keys to fit under the outer one — a blazer and the like), pick only the outer one — see "Troubleshooting".

## Expression Menu

| Menu | What it does |
|---|---|
| `Sleeve Roll` | On rolls both sleeves up, off brings them down (others see it, off by default, not saved unless you turn on Saved) |

The menu name is `Sleeve Roll` / `袖まくり` / `소매 걷기` by the menu language; change it with **Menu name** in the inspector.
- **Install to**: the menu the toggle goes in. Empty = the root (default). Right below, **Avatar menu** shows this avatar's Expression Menu as a tree — click the menu you want (no need to know menu names). The number on the right is how many items that menu has; put it in a full one (8/8) and Modular Avatar moves the overflow into a 'More' submenu at the end. Dropping a menu asset on the field works too. A menu that is not in the avatar's menu leaves the toggle out — the inspector and **Last build** warn you.
- **Saved**: on keeps the toggle's state (rolled or down) when you rejoin or change avatars. Off by default (rolled down each time).
- **Last build** shows where the toggle actually ended up (e.g. `Root / Kemono`) and whether it is saved.

With **Menu** off, no menu is added. The synced Bool parameter `Pyrite9/SleeveRoll/Hold` is still made, so your own menu or a Contact can turn it on and off by that name (**Saved** still applies with the menu off).

### Making the toggle yourself

**Install to** and **Saved** cover most cases. If you still make your own toggle:

- The parameter `Pyrite9/SleeveRoll/Hold` is **made at build time**, so it is not in the avatar's Expression Parameters before the build. The VRChat SDK's Expressions Menu parameter field (the dropdown) only lists names in the Expression Parameters asset, so searching for it finds nothing.
- **The VRChat way:** add `Pyrite9/SleeveRoll/Hold` to the avatar's Expression Parameters yourself — Type **Bool**, Default 0, **Saved** (if you want it saved), **Synced** on. Then make a Toggle in the menu, pick that name as its Parameter and set **Value to 1**. At build it merges with Sleeve Roll's own declaration, so it costs no extra synced bits (still 1 bit).
- **The Modular Avatar way:** type `Pyrite9/SleeveRoll/Hold` into the parameter field of an MA Menu Item (Type Toggle) and set Value to 1. To have it saved, add **MA Parameters** to the same object and declare the same name as **Bool**, synced, **Saved**.
- **Turning on only the MA Menu Item's "Saved" does not save it.** Saving is decided by the parameter's declaration (MA Parameters, or Saved in Expression Parameters).
- If any one declaration is saved, the parameter is saved. If you added it to Expression Parameters with Saved, it stays saved even with the inspector's **Saved** off.
- **If you made your own toggle with 0.1.0:** after updating to 0.1.1, delete your own toggle if you keep the inspector's **Menu** on (otherwise there are two of the same toggle).

## Performance

| Item | Added |
|---|---|
| BlendShape | 2 on the garment (`SleeveUp_L`, `SleeveUp_R`), 2 on a body mesh that shrinks the arm under the outfit (`SleeveRoll_Unhide_L/R` — only when there is a shrink). On an avatar where an animation (an outfit toggle and the like) turns that shrink on and off (even one at 0 at build), 2 per such shrink shape (`SleeveRoll_Unhide_L/R__<shape name>`) — both kinds when it also has a fixed shrink |
| Bones | 2 (wrist helper bones `SleeveRoll_Wrist_L/R`). More depending on the outfit: with a kept sleeve PhysBone, 1 per bone from the ones that hold cloth up to the chain's first bone (`SleeveRoll_Shadow_…`); on a garment whose rolled vertices are weighted to fingers, 1 per finger bone from the hand to that bone (`SleeveRoll_Shadow_…`); on a garment whose sleeve is also weighted to bones that move on their own (a PhysBone sleeve under the upper arm and the like), 1 per such bone (`SleeveRoll_Follow_…`) |
| Constraints | 2 VRC Rotation Constraints (wrist helper bones), plus 1 VRC Rotation Constraint per `SleeveRoll_Shadow_…` bone and 1 VRC Parent Constraint per `SleeveRoll_Follow_…` bone |
| FX layers | 1, plus 1 (`Sleeve Roll Unhide`) only when an animation turns the shrink on and off |
| Synced parameters | 1 bit (shared by every Sleeve Roll in the avatar) |
| Menu icon | 1 texture, 256×256 (only with the menu toggle) |

No meshes or materials are added, and no textures except the menu icon. No sleeve PhysBones are added either (the existing ones keep running).

Only when an animation turns the shrink on and off (even one at 0 at build), unsynced Animator parameters are added — 1 per such shrink shape (`Pyrite9/SleeveRoll/Live/…`) plus 2 (`Pyrite9/SleeveRoll/One`, `Pyrite9/SleeveRoll/Rolled`). They are not put in Expression Parameters, so the synced bits stay the same.

## Settings (Sleeve Roll component)

- **Basics**: Menu Language · Garments (candidate buttons — several allowed — and a field for garments not listed) · Sleeves (Both / Left / Right).
- **Menu**: Install Menu (+ Menu Name · Install To) · Saved. With several Sleeve Rolls in an avatar, the first one with a garment picked decides.
- **How far it rolls** (0 = wrist · 1 = elbow, bigger rolls further): Roll To (Cuff position, default 0.30; the gauge's yellow (0.45+) and orange (0.60+) stretches can look odd, so check with **Preview**) · Unhide When Rolled (Arm shrink — off keeps the shrink and stops before the shrunk arm shows, judged by the shrink that is on at build; if off, be sure to check inside the sleeve in-game).
- **Rolled shape** (mm, Sleeve width is a ratio 0–1): Margin (Gap to skin) · Looseness (Sleeve width) · Fold Amplitude (Fold depth) · Fold Length (Fold spacing) · Start At (Starts at, 0 = wrist · 1 = elbow; above this the sleeve doesn't move. Default 0.75. For fine-tuning: it changes the shape, so try it with **Preview**. Too close to the elbow, the sleeve looks shrunk rather than rolled, and on some outfits skin can poke through when the elbow bends, so check in-game).
- **Advanced**: Wrist Proxy (keep the rolled cuff still) · Body Meshes (where the skin is measured, empty = auto, leaving out turned-off objects).
- **Last build**: that avatar's last build (per garment — inside out, an outer one shows what it sits over — and side: where the cuff ended up, whether it stopped at the shrink edge, how many vertices had the shrink released), where the menu toggle is and whether it is saved, warnings, Build log and README buttons.
- **Version**: once a day it asks GitHub for the latest version and tells you when there is a newer one (see "Notes").
- **Changelog**: this version's changes (read from this README).

## Troubleshooting

Console warnings are in English. **Last build** in the inspector shows the same English sentence, with what to do under it in the menu language.

| Message or symptom | What to do |
|---|---|
| `Not a humanoid avatar — Sleeve Roll not built` | Check that the avatar's Rig is Humanoid. Only humanoids are supported. |
| `no humanoid forearm or hand bone` | Check the avatar's Humanoid mapping — the lower arm and hand bones are needed. |
| `the forearm has zero length` | The lower arm and hand bones sit at the same spot in this garment's bind pose, so there is no direction to roll along. Check the avatar's Humanoid mapping (Lower Arm and Hand on different bones) — if it looks fine, please send the build log (see "Contact"). |
| `no garment picked — this Sleeve Roll does nothing` | Pick the sleeved garment in the inspector's **Garments**. |
| `not inside this avatar — skipped` | That garment belongs to another avatar (a copied component?). Pick the garment of this avatar. |
| `no mesh — skipped` | This garment's Skinned Mesh Renderer has no mesh (the Mesh field is empty or the asset is missing). Assign the mesh, or drop it from the inspector's **Garments**. |
| `picked in two Sleeve Rolls — the first one's settings are used` | Drop it from one of them — each garment belongs to one Sleeve Roll. |
| `the sleeve (… mm) is thinner than the skin (… mm) — the picked mesh looks like a body` | One of two causes. ① You picked a body mesh — pick the garment. ② You picked an inner garment shrunk with shape keys to fit under an outer one (a blazer and the like) together with that outer one (the outer one may then roll only a little) — pick only the outer one (see the row "You want to roll only the outer garment" below). |
| `the garment is not weighted to the forearm bone` | This garment's sleeve isn't bound to the forearm (short sleeves, etc.). It can't be rolled. For an outfit added with MA Merge Armature, check that its forearm bone merges into the avatar's (bone names match). |
| `no sleeve vertices on the forearm` | This mesh has no sleeve on the forearm (short sleeves, or not a garment?). It can't be rolled — drop it. |
| `the sleeve is already shorter than Roll To` | The sleeve is already shorter than Cuff position. Raise **Cuff position** (toward the elbow). |
| `the body mesh has forearm skin only from t=… (cut away under the outfit)` | The body model has the forearm cut away under the outfit. Use a body mesh with the whole forearm. The same applies when it rolls only as far as the skin reaches and warns (`— rolled only to t=…`): that body mesh rolls it all the way. |
| `no skin (body) mesh found on the forearm` | The body to measure the skin on wasn't found. Put the body mesh in **Advanced > Body Meshes**. |
| `the rolled shape … — skipped`, `normalizing produced an invalid position — skipped` | Sleeve Roll could not work out a sound rolled shape for this sleeve, so it is left as it is. Check the avatar's arm bones (lower arm and hand apart?) — if they look fine, please send the build log (see "Contact"). |
| `the garment already has a shape key with this name — skipped` | The garment already has `SleeveUp_L/R` (another tool or a baked copy?). Use the original garment mesh. |
| `an MA Shape Changer in Delete mode removes vertices at build` | That body mesh loses vertices at build. If you see a hole where the sleeve is rolled, switch that Shape Changer to Set mode. |
| `an animation changes …, which move the sleeve — the roll is made for the values at build time` | An animation (a menu toggle or the like) changes a shape key that moves this garment's sleeve. The rolled shape is made for the values at build time, so it may not fit once they change. Set the scene to the values you use most, then build. |
| `… the follow shadow follows …, the rolled cuff of … may sit off` (also `the wrist proxy follows`, `the finger shadow follows`) | Two garments use the same bone with different bind poses, and Sleeve Roll follows the one it handled first — the later garment's rolled cuff may sit a little off. If it doesn't show, you can leave it. |
| `hide shape '…' is animated in the … layer — the arm brought back under a rolled sleeve follows it only in FX` | An animation in a layer other than FX (Gesture and the like) changes this shrink shape. Releasing the shrink while rolled follows only FX animations, so if that layer takes the shrink off, the arm under the rolled sleeve can bulge. Move that animation to the FX layer. |
| `'…' (built at 0, the FX sets up to …) has … frames — its share of the restored arm assumes the shape moves in a straight line with its value` | This shrink shape has in-between frames. The arm brought back under the rolled sleeve is exact at 0 and at the value the FX sets; at values in between (while a toggle blends, or with a slider) it can sit a little off. A toggle that only switches it on and off is fine as it is. |
| `could not read the animator controllers (…) — the arm an outfit hides is brought back as it was at build, even when the outfit is toggled off` | Reading the animator controllers failed (reason in the brackets). The build goes on and the sleeves roll, but releasing the shrink doesn't follow outfit toggles (as in 0.1.2 — turning the outfit off while rolled can make the forearm bulge). Please send the build log (see "Contact"). |
| `FX: layer 'Sleeve Roll' not found — …` | Sleeve Roll's own FX layer ('Sleeve Roll') is missing from the built FX — another build tool may have removed or renamed it. The part after the dash is what stops working; the sleeves themselves may not roll either. Please send the build log (see "Contact"). |
| `FX: no curve for '…' on … found — its share of the restored arm stays on while rolled` | The animation that changes this shrink shape was found earlier in the build but is not in the final FX (another build tool moved or renamed it?). The sleeves still roll, but releasing this shrink doesn't follow that toggle. Please send the build log (see "Contact"). |
| `Sleeve Roll stopped on an internal error — the sleeves may be left half set up` | Sleeve Roll hit a bug on this avatar — the sleeves may roll wrong. Please send `Logs/Pyrite9_SleeveRoll_build.txt` with the EXCEPTION line (see "Contact"). |
| `the menu toggle is not in the built Expression Menu — Install to (…) is not a menu of this avatar` | The **Install to** menu is not part of this avatar's menu. Empty it (the root) or pick a submenu of the avatar's Expression Menu. |
| `N controls in the Expression Menu drive 'Pyrite9/SleeveRoll/Hold' — one is enough` | The menu has two or more switches for the sleeves (for example a toggle you made yourself with 0.1.0). Delete your own toggle, or turn **Menu** off and keep only yours. |
| Skin shows through the rolled cloth | Raise **Rolled shape > Gap to skin**. |
| The rolled sleeve is too thick or too thin | Adjust **Sleeve width** (far left hugs the arm). |
| On a layered outfit the inner cloth shows through the outer one | Check that both garments are picked (an unpicked one doesn't roll and stays as it is). Raising **Gap to skin** for the outer one a little also helps. If the inner one failed with `is thinner than the skin`, see the row below. |
| You want to roll only the outer garment (a blazer and the like, with the inner one shrunk with shape keys) | Pick **only the outer one**. The inner garment is released only as far as the rolled sleeve leaves room. If the inner one's own sleeve is thicker than the outer one, the rolled part can look thick (that garment's own volume). On such an outfit, picking both can make the inner one fail with `is thinner than the skin` — if both roll fine when picked together, leave it as it is. |
| `this sleeve's PhysBone stops moving (…)` | This sleeve's PhysBone can't be kept safely (reason in the brackets), so the sleeve follows the hand with no physics. It still rolls. To keep the physics, remove the cause in the brackets. |
| `a PhysBone on the forearm (…) holds this sleeve and cannot be held still while rolled (…)` | This sleeve does not roll, because its physics can't be held still. Remove the cause in the second brackets. |
| `another PhysBone (…) also moves … — the two fight over it` | Two of the outfit's PhysBones move the same bone (the same PhysBone added twice, or one covering the other). It is the outfit's own setting, so it sways the same without Sleeve Roll, and the sleeve still rolls as it is. To fix it, remove one, or put the inner PhysBone's first bone in the outer PhysBone's Ignore Transforms. |
| `a PhysBone under the upper arm (…) holds this sleeve down to t=… — it cannot be rolled` | A PhysBone on the upper arm holds the sleeve down to the cuff. This outfit can't be rolled. |
| The sleeve jumps once when it starts to roll | Normal. The rolled shape is worked out from the PhysBone's rest pose, so a drooping sleeve goes back to it. It sways again when lowered. |
| The rolled cuff bends with the wrist | Check that **Advanced > Wrist Proxy** is on. |
| The forearm bulges when you turn an outfit off from the menu with the sleeves rolled | Fixed in 0.1.3. If it still bulges, that outfit toggle was made with a tool that builds its FX after Sleeve Roll (VRCFury and the like) — Sleeve Roll can't follow it. Toggles made with Modular Avatar (Object Toggle, Shape Changer) and animations put straight in FX are followed. |
| The forearm looks off for one frame the moment an outfit is turned off or on | Known behavior: releasing the shrink follows the outfit toggle one frame late. |
| A thin arm shows inside the rolled sleeve (the outfit shrinks the arm and it isn't released) | First check that **Unhide When Rolled** is on. While rolled, it releases the shrink that is on at build, and also a forearm shrink that is 0 in the scene but turned on by an FX animation (an outfit toggle and the like) — including an avatar uploaded with the outfit off, and a shrink turned on through a blend tree. Still not released: ① a shrink at 0 at build that hides only the hand (doesn't move the forearm) — turn that shape on in the scene to the value the outfit's animation sets, then build. ② a shrink turned on only by a layer other than FX (Gesture and the like) — move that animation to the FX layer. ③ with **Unhide When Rolled** off, where it stops is set only by the shrink that is on at build, so a thin arm can show when a shrink at 0 at build is turned on — turn that shape on in the scene, then build. A shrink with in-between frames can also sit a little off (the `has … frames` row above). |
| The arm under the rolled sleeve comes back thicker than set (a body shape at 0 at build that an FX animation turns on to make the arm thinner) | Known limit: Sleeve Roll can't tell a shape that makes the forearm thinner from one that hides it under the outfit, so while rolled it is released where the sleeve is rolled. To avoid it, turn **Unhide When Rolled** off — then no shrink is released, and if a shrink is on at build the sleeve stops before the shrunk arm. |
| Rolling while wearing another outfit makes the arm poke through that outfit's sleeve | Known limit: releasing the forearm shrink while rolled doesn't tell which outfit turned the shrink on (another outfit using the same body shape, or another outfit's own shrink shape turned on by the FX). Turn the roll on only while wearing the rolled garment. |
| Sleeve whose cuff hangs below the arm (e.g. a sleeve sliding off one side) | If the sleeve end is open, it is corrected automatically at build; the build log shows `normalized`. Only the Foxy outfit family was tested. A sleeve with a closed end, a detached cuff, a large separate part (such as a ribbon) or a slit along the top may not be corrected, or may lose its shape. |

## Update / remove

- **Update:** import the new package over the old one (everything checked, Import). Your settings are on the Sleeve Roll object in your scene and stay. Don't edit files inside `Assets/Pyrite9/SleeveRoll/` — updates overwrite them.
- **Remove from an avatar:** delete the avatar's Sleeve Roll object.
- **Remove from the project:** first remove it from your avatars, then delete the `Assets/Pyrite9/SleeveRoll/` folder.
- The inspector's changelog is read from this README — keep the README files in the package folder.

## Changelog

- **0.1.3** (2026-11-15): More outfits can be rolled. Garments weighted only to outfit bones that Modular Avatar keeps instead of merging, to bones under the forearm (a twist bone, or bones an outfit conversion tool leaves behind) or to copies of the hand bone placed under it now roll and get the wrist helper bone — before, they failed with `the garment is not weighted to the forearm bone`, or the rolled cuff came away from the arm when the wrist bent. Fixed the rolled cuff sagging or not rolling far enough on outfits whose sleeve is also weighted to bones that move on their own (a PhysBone sleeve under the upper arm and the like). The rolled shape is now worked out from the garment as it is worn, with the shape keys that are turned on, and a warning shows when an animation changes those shape keys. Fixed the forearm bulging when an outfit is turned off from the menu with the sleeves rolled — releasing the shrink now follows the shrink's value. On such avatars the shapes are named `SleeveRoll_Unhide_L/R__<shape name>`, one FX layer is added, and the arm can look off for one frame the moment the outfit is turned off. Fixed nail and hand shape keys going back to default while rolled, the lowered sleeve changing on avatars with a rescaled hand bone, and the lowered cuff not following a bent thumb. A body mesh with forearm skin only near the wrist now rolls the sleeve as far as the skin reaches and warns instead of failing, and fixed sleeves barely rolling on bodies whose forearm skin is weighted to a twist bone. When a side fails, the build log shows diagnostic lines (`diag:`), and the inspector gives more advice under warnings. An outer garment (a blazer and the like) worn over an inner one shrunk with shape keys now rolls with only the outer one picked — the inner one is released only as far as the rolled sleeve leaves room. A forearm shrink that is 0 at build but turned on by an FX animation (an outfit toggle and the like) is now released while rolled, so no thin arm shows inside the rolled sleeve on an avatar uploaded with the outfit off. Also fixed: the build changing a garment's mesh on another avatar when a Sleeve Roll copied from that avatar still pointed at its garment (it is now skipped with a warning), Sleeve Roll stopping partway on an internal error when the avatar has a mesh whose bone list is longer than its bind poses, so the avatar was built with the sleeves half set up and no warning, and the sleeve stretching toward the hand instead of rolling when Roll To sits just above the end of the sleeve (rolling only a little, or a short sleeve) and Start At is set close to it. When the rolled shape comes out broken (NaN, or vertices thrown off the arm), only that side is left unrolled with a warning, and an internal error during the build is now also kept as a warning. Known limits: releasing the shrink can't follow outfit toggles made by tools that build their FX after Sleeve Roll (VRCFury and the like); a body shape that the FX turns on to slim the arm is also released where the sleeve is rolled; and releasing the shrink doesn't tell which outfit turned it on, so if you roll while wearing another outfit the arm can poke through that outfit's sleeve (see Troubleshooting).
- **0.1.2** (2026-11-09): Fixed one sleeve not rolling on outfits that put the left and right sleeve PhysBones on one object (**Last build** said `a second PhysBone on one object`). On outfits where a sleeve PhysBone starts inside another, two PhysBones share a starting bone, or a sleeve PhysBone leaves some bones out with Ignore Transforms, the whole rolled sleeve now stays still, and the lowered sleeve sways as it does without Sleeve Roll (before, part of it kept swaying while rolled, or the cuff followed the wrist). A new build warning appears when two PhysBones move the same bone. The Preview line now says that for an outfit put on at build time the PhysBone check is decided by the build. Other outfits build the same as 0.1.1.
- **0.1.1** (2026-11-08): The inspector's **Menu** section gets **Install to** (the submenu the toggle goes in, empty = the root; the avatar's menu is shown as a tree to click) and **Saved** (keep the toggle's state after rejoining or changing avatars, off by default). **Last build** shows where the toggle is and whether it is saved, and the inspector and the build warn when Install to is not in the avatar's menu. The build also warns when the menu has two or more toggles for the same parameter. Added 'Making the toggle yourself' to the README. With the default settings the built avatar is the same as 0.1.0.
- **0.1.0** (2026-11-07): First release.

## Notes

- The rolled shape is worked out again from the garment's mesh on every build. Change or edit the outfit and the next build fits it.
- A sleeve PhysBone is checked at build time and kept only when it's safe to hold still while rolled. It isn't kept (and you get a warning) if it also moves other bones, has Is Animated on, uses Max Stretch or Max Squish, is used by Avatar Optimizer Merge PhysBone, is moved by another animation (the PhysBone or its bones), or its rest pose differs from the mesh. **Last build** shows how many were kept.
- If the garment has its own short-sleeve shape key, both can be on at once. On the tested outfits (Kipfel's shirt, Milfy's cardigan) that didn't break anything noticeable.
- The FX layers follow the avatar's Write Defaults (Modular Avatar "Match Avatar Write Defaults"), except the `Sleeve Roll Unhide` layer that releases a shrink an animation turns on and off: it has Write Defaults on whatever the avatar uses (last in FX, the same way as Modular Avatar's Merge Blend Tree). The Action, Base and Gesture layers are not used.
- When you open the Sleeve Roll inspector in the Unity Editor, it asks GitHub (`api.github.com`) for this package's latest version once a day. No personal data is sent; click the version text to check again right away.

## Contact

- Bug reports and questions: a Booth message (https://pyrite9.booth.pm/) or X **@Pyrite9_VRC**.
- Please include: the version (shown in the inspector), the warnings in **Last build** or the Console lines starting with `[Pyrite9 Sleeve Roll]`, `Logs/Pyrite9_SleeveRoll_build.txt` from the project folder, the garment's name, and your Unity, VRChat SDK, Modular Avatar and NDMF versions.

## License (summary — full text in `LICENSE.txt`)

- Free. You may use and modify it and upload avatars that use it (public or private). The outfit's and avatar's own terms (such as no public uploads) still apply on their own.
- You may use those avatars in videos, streams and screenshots, including monetised content.
- Installing it on someone else's avatar (commissions, etc.) is fine only if that person got this package themselves.
- Redistributing, sharing or selling the files, modified or not, is not allowed (including in avatar data or assets you distribute or sell).
- Credit is not required, but appreciated ("Pyrite9 Sleeve Roll © Pyrite9").
- The package is also on GitHub: https://github.com/Pyrite9/vrchat-sleeve-roll (viewing and forking it there are allowed).
- Provided as is, without warranty. The terms in the version you obtained apply to that version.

© Pyrite9
