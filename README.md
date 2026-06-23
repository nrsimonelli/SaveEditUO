# Unicorn Overlord — Save Data Editor

![DL Count](https://img.shields.io/github/downloads/nrsimonelli/SaveEditUO/total.svg)

Save data editor for the Nintendo Switch version of **Unicorn Overlord**.  
Fork of [turtle-insect/UnicornOverlord](https://github.com/turtle-insect/UnicornOverlord) with additional features.

## Links

- **Game portal:** <https://unicorn-overlord.com/>

## Requirements

- Windows
- [.NET 10](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- Ability to export and re-import save data on your Switch (e.g. homebrew)

## Build (developers)

- Windows 10 (64-bit) or later
- [Visual Studio 2022](https://visualstudio.microsoft.com/) (or .NET 10 SDK)

## Getting Started

1. Download the latest `SaveEditUO-vX.X.X.zip` from the [Releases](https://github.com/nrsimonelli/SaveEditUO/releases) page
2. Extract the zip to a folder of your choice
3. Run `UnicornOverlord.exe`

> **Note:** If Windows shows a SmartScreen warning, click **More info → Run anyway**. This is expected for unsigned applications.

## Basic workflow

1. **Export** your save from the Switch (e.g. `UCSAVEFILE01.DAT`).
2. **Open** the save in the editor (File → Open, or Ctrl+O).  
   A timestamped **backup** is created automatically in the `backup` folder.
3. **Edit** as needed (see Features below).
4. **Save** (Ctrl+S) or **Save As** (Ctrl+Shift+S).
5. **Import** the modified save back to the Switch.

## Features

### Basic

- Edit **Money** and **Fame**
- Toggle the **ZENOIRA** flag for True Zenoiran unlock

### Characters

- Edit **Use**, **Class**, **Level**, **Exp**, **Growth Type 1 & 2**, and all dews
- **Export** — Save the selected character to a `.uocd` file
- **Import** — Overwrite a character slot from a `.uocd` file
- **Insert** — Add one or more characters from `.uocd` files (multi-select supported)

### Character — Equipment

- View **Slots 1–4** with resolved item names
- **Morph** — Transform the equipped item in a slot to a different item
- **Delete** — Unequip and remove the item from inventory
- **Create & Equip** — Create new equipment and equip it into an empty slot

### Character — Tactics

Displays up to 8 skill slots per character, matching the in-game tactics screen layout (Action · Condition 1 · Condition 2). Active skills are indicated with a red dot, passive skills with a blue dot.

- **Edit conditions** — Click any Condition cell to open the condition picker
- **Add Skill** — Open the skill picker and add the selected skill to your unit's list
- **Delete** — Remove any skill slot and its tactics

### Character — Bond

- View and edit bond entries (ID and Value)
- **Count Max** — Set all bond values for the selected character to 1000

### Items

- Edit consumables **Count** and use **…** to pick an item by name
- **Append** — Add new items
- **Count Max** — Set all consumable counts to 99

### Equipment (global inventory)

- Append and delete equipment entries
- Deleting an item updates all character slot references automatically

---

## Bug fixes & improvements

### Equipment

- **Delete safety** — Deleting equipment now updates every character's equipped slots so nothing points at a removed item. Inventory is compacted and slot references stay valid, preventing save corruption.
- **Manage Unit Equipment** — Create, Edit, and Delete items all directly from a unit's tab without the fear of creating an invalid inventory state.

### Skill and Tactics

- **Full tactics** — The complete 16-byte tactic entry layout was implemented, including the `isValid`, `isUnusable`, skill ID encoding, and the universal one-behind condition storage rule (conditions for entry _i_ are always stored in entry _i−1_, with entry 0 conditions at `charAddr+92`).
- **Class skill resolution** — Class-relative slot IDs (3–10) are resolved to skill names using a complete `ClassSkillSlots` table covering all 73 classes including all promotions, variants, and Dark Marquess forms.
- **Item skill ID offset** — Item skills store their ID as `actual_id − 15`. This is correctly applied on read and write.
- **Condition picker** — Searchable picker built from the full 201-entry `UcFactorList` condition table, pre-selecting the current value and supporting case-insensitive search.
- **Skill add/delete** — Inserting and removing skill entries correctly shifts the tactic array and rewrites all condition associations to preserve the one-behind rule throughout.

### Character

- **Import / Insert cleanup** — Imported characters are sanitized so formation, equipment slots, and status flags don't leave the save in an inconsistent state.
- **Growth types** — Growth Type 1 and Growth Type 2 are decoded from the character block and exposed as labelled dropdowns (Hardy, Offensive, Defensive, Precise, Lucky, Keen, Guardian, Go-getter, All-rounder).

---

## Special Thanks

- Original project: [turtle-insect/UnicornOverlord](https://github.com/turtle-insect/UnicornOverlord)
- Algo for all of their help and feedback
