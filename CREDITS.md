# Credits & licence checklist

Working list of every third-party asset in the game, so the in-game Credits
screen can be built from it. First draft 2026-10-09, made by scanning
`Assets/Imports`, `Assets/Animations` and the Unity Asset Store download cache.

**How to use it**

1. Go through **Part 1** model by model. Find each one on sketchfab.com (or
   wherever it came from) and fill in Author, Licence and Link.
2. Status: `[ ]` = not checked yet · `[x]` = checked, OK to keep ·
   `[!]` = must be replaced (NonCommercial when selling/donations, Editorial,
   ripped, real brand, or source can't be found).
3. Licence quick guide:
   - **CC0** - free for anything, no credit needed (credit is still nice)
   - **CC-BY** - free for anything, **credit required**
   - **CC-BY-SA** - credit required + changes must use the same licence (fine for models in a game)
   - **CC-BY-NC** - credit required, **no money at all** (no sales, donations or ads)
   - **CC-BY-ND** - credit required; you may not modify it (re-texturing/editing counts)
   - **Editorial / "Standard" without a game licence / unknown** - replace it
4. When everything is ticked, Part 1 (CC-BY ones) + Part 3 become the in-game
   Credits screen. Part 2 can be listed too as a thank-you (not required).

---

## Part 1 - TO CHECK (probably Sketchfab or other free sites)

| Status | Folder (`Assets/Imports/...`) | What it is | Author | Licence | Link |
|---|---|---|---|---|---|
| [ ] | `All Buildings` | Rural buildings: diners, henhouse, pigsty, stable | | | |
| [ ] | `BillBoard_Posters` | Roadside billboard (Downloads: billboard-psd-layered-texture-maps-included.zip) | | | |
| [ ] | `Bridge` | Bridge (file name is a Sketchfab model ID: 531574b2f9f54fcd9cd2d08cd5689795) | | | |
| [ ] | `Buildings_1` | Rural building set | | | |
| [ ] | `Buildings_2` | Low-poly building | | | |
| [ ] | `Cargo Items` (1-6) | Cargo props (Mission 1) | | | |
| [ ] | `Dagger` | Knife_low - player's knife | | | |
| [ ] | `Dead_Body` | deadman / hobo body | | | |
| [ ] | `Dinosaurs` (1-6) | Triceratops and other dinosaur statues | | | |
| [ ] | `Fire Dept` | Fire department building (FireDep009.dae) | | | |
| [ ] | `Gas station` | Gas station + "6twelve" store (Downloads: gas-station.zip, 6twelve.zip) | | | |
| [ ] | `Grass_Dry` | Dry grass clumps | | | |
| [ ] | `Hospital` | Hospital building | | | |
| [ ] | `Hotel` | Hotel props: PC, printer, TV, briefcase, cabinet, car counter... | | | |
| [ ] | `HotelInterior` | Hotel interior | | | |
| [ ] | `Mob Room` | Mob leader's room (art, bed...) | | | |
| [ ] | `PlayerHouse` | Farm house (player's house) | | | |
| [ ] | `Police Station` | Police station (Downloads: city-police-station.zip) | | | |
| [ ] | `Rocks` | River rocks | | | |
| [ ] | `Tractor` | TractorMaster (maybe Asset Store "Old Antique Farm Tractor" by Gade Embossed - check) | | | |
| [ ] | `Train_Track` | Railroad track + "BigBoy" locomotive and cab. Big Boy is a real Union Pacific locomotive: OK as a generic steam train, just don't use the UP logo/name | | | |
| [ ] | `Trees` | Beech, cypress and other trees (maybe Asset Store "European Forests" by MysticForge - check) | | | |
| [ ] | `Tunnel` | Concrete tunnel sections | | | |
| [ ] | `garage` | Garage box | | | |
| [ ] | `glass_display` | Glass showcase (Shopping Complex) | | | |
| [ ] | `grassgreen` | Single grass mesh | | | |
| [ ] | `pebbles` | Ground pebbles | | | |
| [ ] | `transformer` | Electrical transformer (Persian file name) | | | |
| [ ] | `windmill` | Windmill | | | |
| [ ] | `wooden fence` | Wooden fence | | | |
| [ ] | `Road_Materials` | Road / concrete textures (highway-lanes: looks like freepbr.com - check) | | | |
| [ ] | `Fire Dept` textures `Bricks026`, `Concrete011` | Look like ambientCG (CC0) - confirm | ambientCG | CC0? | https://ambientcg.com |
| [ ] | `Sounds` | door-unlock, shutter clicks (file names look like Pixabay: free, no credit needed - confirm) | freesound_community, kauasilbershlachparodes | Pixabay Content Licence? | https://pixabay.com |
| [ ] | `image` | Map layout + pngegg.png. **pngegg images are often copied from elsewhere** - replace unless you made it | | | |

Also check the Sketchfab zips still in `Downloads` (carrier-car, diner, laundry) - if any of them ended up in the game, add a row.

**Not in the game (good):** `toyota-dyna-truck.zip` (real brand) and `_4076-.zip` (a Counter-Strike map - never use).

---

## Part 2 - Unity Asset Store (OK for commercial games, credit not required)

Covered by the standard Unity Asset Store EULA: allowed in a free or paid game,
just don't share the raw files.

| Folder | Package | Publisher |
|---|---|---|
| `Imports/ADG_Textures` | Outdoor Ground Textures | A dogs life software |
| `Imports/ALP_Assets` | Grass Flowers Pack Free | ALP |
| `Imports/ARCADE - FREE Racing Car` | ARCADE: FREE Racing Car | Mena |
| `Imports/PROMETEO - Car Controller` | PROMETEO: Car Controller (edited for key rebinding) | Mena |
| `Imports/Alstra Infinite`, `Imports/Aircraft` | Planes Choppers - PolyPack (Aircraft = prefabs made from it - check) | Alstra Infinite |
| `Imports/Gece Studio` | Rifle HK416 - Free | Gece Studio |
| `Imports/High Matters` | American Sedans Taxi Police Classic | High Matters |
| `Imports/IgniteCoders` | Simple Water Shader URP | IgniteCoders |
| `Imports/KE Statues - Lite` | KE Statues - Lite | KE 3D Assets |
| `Imports/Kevin Iglesias` | Human Character Dummy | Kevin Iglesias |
| `Imports/PretoriusLab` | Industry Props | Pretorius Lab |
| `Imports/Single_detailed_truck` | Single Detailed Truck | VIS Games |
| `Imports/Truck_LowPoly` | Truck low poly | SR Studios Kerala |
| `Imports/VFX` | Free Quick Effects Vol 1 | Gabriel Aguiar Prod |
| `Imports/asset_free_Ukraine_cars` | Ukraine free cars | KOS-store |
| `AssetsStore/Garage_props` | Garage Props Set | Abandoned World |
| `Visual Design Cafe` | Nature Renderer 6 Free | Visual Design Cafe |
| `TextMesh Pro`, URP, Input System | Unity packages | Unity Technologies |

Your Asset Store cache also has ~40 other packages that are **not** in this
project any more (EasyRoads3D, Soviet cars, Nature Starter Kit, ...). Nothing
to credit for those. If you re-import one, add it here.

---

## Part 3 - Characters & animations

| What | Source | Licence | Credit |
|---|---|---|---|
| Player (Ch06), Guards (Ch18), Mob Leader (Ch33), Receptionist (Ch13), Gas Station gang (Whiteclown N Hallin) | Adobe Mixamo | Royalty-free, commercial use OK | Not required ("Characters and animations: Mixamo" is a nice touch) |
| Hotel NPCs (`Characters/NPC` 1-10) | **Check** - Mixamo? | | |
| `Characters/Police` (empty folder) | - | - | - |
| All clips in `Assets/Animations` (walk, run, crouch, rifle, stab, reload, deaths, texting...) | Adobe Mixamo | Royalty-free, commercial use OK | Not required |

---

## Part 4 - In-game Credits text (fill in when Part 1 is done)

```
Game by Harsh Nara

3D models
  <Model name> by <Author> - <Licence> - <link>
  ...

Characters & animations: Adobe Mixamo
Unity Asset Store assets: Mena (PROMETEO, ARCADE Racing Car), Gece Studio, High Matters,
  VIS Games, SR Studios Kerala, KOS-store, Kevin Iglesias, ... (thank-you list)
Textures: ambientCG (CC0), ...
Sounds: Pixabay
Made with Unity
```
