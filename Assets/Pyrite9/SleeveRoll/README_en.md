# Pyrite9 Sleeve Roll

Version 0.1.3-beta2 (2026-11-10)

A sleeve-rolling gimmick for VRChat avatars. Drop the prefab into an avatar and pick the sleeved garment — when you build, the rolled-up sleeve is worked out from that garment's own mesh.
Turn the Expression Menu toggle on and both sleeves roll up, and others see it too. No shape keys or mesh data come with this package — the shape is made at build time from the garment you own.

한국어: `README_ko.md` / 日本語: `README_jp.md` / License: `LICENSE.txt`

## Features

- An Expression Menu toggle rolls both sleeves up in 0.2 s — others see it too (1 synced bit). Put the toggle in the root or any submenu, and choose whether its state is saved
- The rolled shape is worked out at build time from the garment you own — it measures the skin so the cloth doesn't sink into the arm, keeps the cloth's own thickness and some of its slack, and adds folds where it bunches up
- Where the outfit shrinks the arm under the sleeve, it releases the shrink while rolled — no thin arm shows inside the rolled cuff
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

For layered garments (a jacket over a shirt), pick both in one Sleeve Roll. The build tells inner from outer by the forearm girth (the thinner one is inside). To give each garment its own settings you can also add another Sleeve Roll and pick the other garment there — the layering still fits together, and one toggle rolls both.

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
| BlendShape | 2 on the garment (`SleeveUp_L`, `SleeveUp_R`), 2 on a body mesh that shrinks the arm under the outfit (`SleeveRoll_Unhide_L/R` — only when there is a shrink) |
| Bones | 2 (wrist helper bones `SleeveRoll_Wrist_L/R`), plus 1 per bone of a kept sleeve PhysBone that holds cloth (`SleeveRoll_Shadow_…`) |
| Constraints | 2 VRC Rotation Constraints, plus 1 per bone of a kept sleeve PhysBone that holds cloth |
| FX layers | 1 |
| Synced parameters | 1 bit (shared by every Sleeve Roll in the avatar) |
| Menu icon | 1 texture, 256×256 (only with the menu toggle) |

No meshes or materials are added, and no textures except the menu icon. No sleeve PhysBones are added either (the existing ones keep running).

## Settings (Sleeve Roll component)

- **Basics**: Menu Language · Garments (candidate buttons — several allowed — and a field for garments not listed) · Sleeves (Both / Left / Right).
- **Menu**: Install Menu (+ Menu Name · Install To) · Saved. With several Sleeve Rolls in an avatar, the first one with a garment picked decides.
- **How far it rolls** (0 = wrist · 1 = elbow, bigger rolls further): Roll To (Cuff position, default 0.30; the gauge's yellow (0.45+) and orange (0.60+) stretches can look odd, so check with **Preview**) · Unhide When Rolled (Arm shrink — off keeps the shrink and stops before the shrunk arm shows; if off, be sure to check inside the sleeve in-game).
- **Rolled shape** (mm, Sleeve width is a ratio 0–1): Margin (Gap to skin) · Looseness (Sleeve width) · Fold Amplitude (Fold depth) · Fold Length (Fold spacing) · Start At (Starts at, 0 = wrist · 1 = elbow; above this the sleeve doesn't move. Default 0.75. For fine-tuning: it changes the shape, so try it with **Preview**. Too close to the elbow, the sleeve looks shrunk rather than rolled, and on some outfits skin can poke through when the elbow bends, so check in-game).
- **Advanced**: Wrist Proxy (keep the rolled cuff still) · Body Meshes (where the skin is measured, empty = auto).
- **Last build**: that avatar's last build (per garment — inside out, an outer one shows what it sits over — and side: where the cuff ended up, whether it stopped at the shrink edge, how many vertices had the shrink released), where the menu toggle is and whether it is saved, warnings, Build log and README buttons.
- **Version**: once a day it asks GitHub for the latest version and tells you when there is a newer one (see "Notes").
- **Changelog**: this version's changes (read from this README).

## Troubleshooting

| Message or symptom | What to do |
|---|---|
| `Not a humanoid avatar — Sleeve Roll not built` | Check that the avatar's Rig is Humanoid. Only humanoids are supported. |
| `no garment picked — this Sleeve Roll does nothing` | Pick the sleeved garment in the inspector's **Garments**. |
| `the sleeve (… mm) is thinner than the skin (… mm) — the picked mesh looks like a body` | You picked a body mesh. Pick the garment. |
| `the garment is not weighted to the forearm bone` | This garment's sleeve isn't bound to the forearm (short sleeves, etc.). It can't be rolled. |
| `the sleeve is already shorter than Roll To` | The sleeve is already shorter than Cuff position. Raise **Cuff position** (toward the elbow). |
| `the body mesh has forearm skin only from t=… (cut away under the outfit)` | The body model has the forearm cut away under the outfit. Use a body mesh with the whole forearm. |
| `no skin (body) mesh found on the forearm` | The body to measure the skin on wasn't found. Put the body mesh in **Advanced > Body Meshes**. |
| `an MA Shape Changer in Delete mode removes vertices at build` | That body mesh loses vertices at build. If you see a hole where the sleeve is rolled, switch that Shape Changer to Set mode. |
| `the menu toggle is not in the built Expression Menu — Install to (…) is not a menu of this avatar` | The **Install to** menu is not part of this avatar's menu. Empty it (the root) or pick a submenu of the avatar's Expression Menu. |
| `N controls in the Expression Menu drive 'Pyrite9/SleeveRoll/Hold' — one is enough` | The menu has two or more switches for the sleeves (for example a toggle you made yourself with 0.1.0). Delete your own toggle, or turn **Menu** off and keep only yours. |
| Skin shows through the rolled cloth | Raise **Rolled shape > Gap to skin**. |
| The rolled sleeve is too thick or too thin | Adjust **Sleeve width** (far left hugs the arm). |
| On a layered outfit the inner cloth shows through the outer one | Check that both garments are picked (an unpicked one doesn't roll and stays as it is). Raising **Gap to skin** for the outer one a little also helps. |
| `this sleeve's PhysBone stops moving (…)` | This sleeve's PhysBone can't be kept safely (reason in the brackets), so the sleeve follows the hand with no physics. It still rolls. To keep the physics, remove the cause in the brackets. |
| `a PhysBone on the forearm (…) holds this sleeve and cannot be held still while rolled (…)` | This sleeve does not roll, because its physics can't be held still. Remove the cause in the second brackets. |
| `another PhysBone (…) also moves … — the two fight over it` | Two of the outfit's PhysBones move the same bone (the same PhysBone added twice, or one covering the other). It is the outfit's own setting, so it sways the same without Sleeve Roll, and the sleeve still rolls as it is. To fix it, remove one, or put the inner PhysBone's first bone in the outer PhysBone's Ignore Transforms. |
| `a PhysBone under the upper arm (…) holds this sleeve down to t=… — it cannot be rolled` | A PhysBone on the upper arm holds the sleeve down to the cuff. This outfit can't be rolled. |
| The sleeve jumps once when it starts to roll | Normal. The rolled shape is worked out from the PhysBone's rest pose, so a drooping sleeve goes back to it. It sways again when lowered. |
| The rolled cuff bends with the wrist | Check that **Advanced > Wrist Proxy** is on. |
| Sleeve whose cuff hangs below the arm (e.g. a sleeve sliding off one side) | If the sleeve end is open, it is corrected automatically at build; the build log shows `normalized`. Only the Foxy outfit family was tested. A sleeve with a closed end, a detached cuff, a large separate part (such as a ribbon) or a slit along the top may not be corrected, or may lose its shape. |

## Update / remove

- **Update:** import the new package over the old one (everything checked, Import). Your settings are on the Sleeve Roll object in your scene and stay. Don't edit files inside `Assets/Pyrite9/SleeveRoll/` — updates overwrite them.
- **Remove from an avatar:** delete the avatar's Sleeve Roll object.
- **Remove from the project:** first remove it from your avatars, then delete the `Assets/Pyrite9/SleeveRoll/` folder.
- The inspector's changelog is read from this README — keep the README files in the package folder.

## Changelog

- **0.1.3-beta2** (2026-11-10): Outfits whose sleeve is weighted only to bones under the forearm (a twist bone, or the original bones an outfit conversion tool leaves behind) instead of the forearm bone itself are now handled. When a side fails with `the garment is not weighted to the forearm bone` or `no sleeve vertices on the forearm`, or when the forearm had to be found from a bone under it, the build log now lists the bones the sleeve is weighted to (lines starting with `diag:`). The inspector's changelog now also shows test versions.
- **0.1.3-beta1** (2026-11-10): Fixed sleeves that rolled in the Preview but failed at build with `the garment is not weighted to the forearm bone`. It happened when Modular Avatar keeps an outfit's forearm, hand or upper-arm bone instead of merging it (for example a bone that a PhysBone, collider, contact or constraint points to, or a bone with an MA Scale Adjuster). From the same cause, the body mesh is now found when the avatar's own forearm has an MA Scale Adjuster, and the wrist proxy now works when the outfit's hand bone is kept. The Preview now places outfit bones the same way as the build. Other outfits build the same as 0.1.2.
- **0.1.2** (2026-11-09): Fixed one sleeve not rolling on outfits that put the left and right sleeve PhysBones on one object (**Last build** said `a second PhysBone on one object`). On outfits where a sleeve PhysBone starts inside another, two PhysBones share a starting bone, or a sleeve PhysBone leaves some bones out with Ignore Transforms, the whole rolled sleeve now stays still, and the lowered sleeve sways as it does without Sleeve Roll (before, part of it kept swaying while rolled, or the cuff followed the wrist). A new build warning appears when two PhysBones move the same bone. The Preview line now says that for an outfit put on at build time the PhysBone check is decided by the build. Other outfits build the same as 0.1.1.
- **0.1.1** (2026-11-08): The inspector's **Menu** section gets **Install to** (the submenu the toggle goes in, empty = the root; the avatar's menu is shown as a tree to click) and **Saved** (keep the toggle's state after rejoining or changing avatars, off by default). **Last build** shows where the toggle is and whether it is saved, and the inspector and the build warn when Install to is not in the avatar's menu. The build also warns when the menu has two or more toggles for the same parameter. Added 'Making the toggle yourself' to the README. With the default settings the built avatar is the same as 0.1.0.
- **0.1.0** (2026-11-07): First release.

## Notes

- The rolled shape is worked out again from the garment's mesh on every build. Change or edit the outfit and the next build fits it.
- A sleeve PhysBone is checked at build time and kept only when it's safe to hold still while rolled. It isn't kept (and you get a warning) if it also moves other bones, has Is Animated on, uses Max Stretch or Max Squish, is used by Avatar Optimizer Merge PhysBone, is moved by another animation (the PhysBone or its bones), or its rest pose differs from the mesh. **Last build** shows how many were kept.
- If the garment has its own short-sleeve shape key, both can be on at once. On the tested outfits (Kipfel's shirt, Milfy's cardigan) that didn't break anything noticeable.
- The FX layers follow the avatar's Write Defaults (Modular Avatar "Match Avatar Write Defaults"). The Action, Base and Gesture layers are not used.
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
