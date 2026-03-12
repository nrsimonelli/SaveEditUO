using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace UnicornOverlord
{
    /// <summary>
    /// Imports team formation data from a JSON file into the save, replacing
    /// game units 01–04 (one per player) with the characters described in the JSON.
    ///
    /// JSON shape expected:
    ///   playerTeams   : { "1": { formation: [unitId|null, ...×6] }, ... }
    ///   playerRosters : { "1": { [unitId]: { classKey, level, growths, dews,
    ///                                        equipment, skillSlots } }, ... }
    ///
    /// Only units that appear in the formation array are imported.
    /// The rest of the save (characters 4+, other units, inventory) is untouched.
    /// </summary>
    internal static class TeamImporter
    {
        // ── Save-file constants ───────────────────────────────────────────────

        // Address of the running character-ID counter (u32)
        private const uint AddrIdCounter   = 0x63980;
        // Address of the character-count field (u32)
        private const uint AddrCharCount   = 0x63984;

        // Unit 01 is stored at a fixed address outside the unit block array.
        private const uint Unit01Base      = 0x10D870;
        // Units 02–05 are stored in blocks of 1720 bytes starting here.
        private const uint UnitBlockBase   = 0x10D89A;
        private const uint UnitBlockStride = 1720;

        // Within a unit block: offset to the 6 formation slot charIDs (u32 each).
        private const uint UnitSlotOffset  = 0x692;
        // Within a unit block: offset to the unit identifier field (u32).
        private const uint UnitIdOffset    = 0x68E;

        // Pre-set unit identifier values (fixed in every save).
        // These are written into char+0x04 to assign a character to a unit.
        private static readonly uint[] UnitIdentifiers = { 2, 11, 16, 29 };

        // Character block size in bytes.
        private const uint CharBlockSize   = 464;

        // Maximum number of characters the save supports.
        private const uint MaxCharacters   = 500;

        // ── Gender-locked class sets (mirrors ViewModel.cs) ──────────────────
      	private static readonly HashSet<uint> MaleOnlyClasses = new()
		{
			1, 2, 3, 4, 7, 8, 13, 14, 15, 16, 19, 20, 23, 24, 25, 26, 29, 30, 33, 34, 45, 47, 51, 52, 60, 61, 62, 65, 66, 67, 68, 69, 71, 72,
		};

		private static readonly HashSet<uint> FemaleOnlyClasses = new()
		{
            21, 22, 27, 28, 31, 32, 35, 36, 37, 38, 39, 40, 41, 42, 46, 48, 49, 50, 53, 54, 55, 56, 57, 58, 59, 63, 64, 70, 73,
		};

        // ── Lookup tables ─────────────────────────────────────────────────────

        private static readonly Dictionary<string, uint> ClassKeyToId = new()
        {
            ["Lord"] = 1, ["High Lord"] = 2, ["Fighter"] = 3, ["Vanguard"] = 4,
            ["Soldier"] = 5, ["Sergeant"] = 6, ["Housecarl"] = 7, ["Viking"] = 8,
            ["Swordfighter"] = 9, ["Swordmaster"] = 10, ["Sellsword"] = 11, ["Landsknecht"] = 12,
            ["Hoplite"] = 13, ["Legionnaire"] = 14, ["Gladiator"] = 15, ["Berserker"] = 16,
            ["Warrior"] = 17, ["Breaker"] = 18, ["Hunter"] = 19, ["Sniper"] = 20,
            ["Arbalist"] = 21, ["Shieldshooter"] = 22, ["Thief"] = 23, ["Rogue"] = 24,
            ["Knight"] = 25, ["Great Knight"] = 26, ["Radiant Knight"] = 27, ["Sainted Knight"] = 28,
            ["Dark Knight"] = 29, ["Doom Knight"] = 30, ["Cleric"] = 31, ["Bishop"] = 32,
            ["Wizard"] = 33, ["Warlock"] = 34, ["Witch"] = 35, ["Sorceress"] = 36,
            ["Shaman"] = 37, ["Druid"] = 38, ["Wyvern Knight"] = 39, ["Wyvern Master"] = 40,
            ["Gryphon Knight"] = 41, ["Gryphon Master"] = 42, ["Elven Fencer"] = 43, ["Elven Archer"] = 44,
            ["Werewolf"] = 45, ["Werefox"] = 46, ["Werebear"] = 47, ["Wereowl"] = 48,
            ["Feathersword"] = 49, ["Featherbow"] = 50, ["Featherstaff"] = 51, ["Feathershield"] = 52,
            ["Priestess"] = 53, ["High Priestess"] = 54, ["Crusader"] = 55, ["Valkyria"] = 56,
            ["Elven Sibyl"] = 57, ["Elven Augur"] = 58, ["Snow Ranger"] = 59, ["Werelion"] = 60,
            ["Paladin"] = 61, ["Prince"] = 62, ["Dreadnought"] = 63,
            ["Dark Marquess (Sword)"] = 69, ["Dark Marquess (Axe)"] = 70,
            ["Dark Marquess (Lance)"] = 72, ["Dark Marquess (Staff)"] = 73,
        };

        // Level (5-step) -> EXP; levels not in table use 0.
        private static readonly Dictionary<int, uint> LevelToExp = new()
        {
            [5] = 1000, [10] = 4650, [15] = 12350, [20] = 28000, [25] = 56400,
            [30] = 109200, [35] = 203500, [40] = 364000, [45] = 638900, [50] = 1057900,
        };

        private static readonly Dictionary<string, int> SkillKeyToId = new()
        {
            ["ironCrusher"] = 74, ["heavySlash"] = 59, ["leanEdge"] = 43, ["cavalrySlayer"] = 44,
            ["spinningEdge"] = 45, ["verticalEdge"] = 73, ["longThrust"] = 78, ["javelin"] = 79,
            ["brandish"] = 75, ["sanguineAttack"] = 156, ["shadowThrust"] = 145, ["magicAttack"] = 157,
            ["trueThrust"] = 143, ["passiveShatterSpear"] = 168, ["phantomAttack"] = 158,
            ["lightningShaker"] = 146, ["icicleDart"] = 144, ["rampage"] = 149, ["lethalVenom"] = 148,
            ["flameJavelin"] = 80, ["elfslayer"] = 159, ["freezingThrust"] = 125,
            ["thunderousThrust"] = 124, ["dragoonDive"] = 92, ["wardingSlash"] = 47,
            ["shieldBash"] = 48, ["defender"] = 49, ["honedSpear"] = 81, ["smash"] = 106,
            ["rollingAxe"] = 107, ["wideBreaker"] = 108, ["keenEdge"] = 50, ["impale"] = 51,
            ["meteorSlash"] = 56, ["killingChain"] = 60, ["bastardsCross"] = 61, ["sting"] = 85,
            ["rowProtection"] = 249, ["greatshield"] = 251, ["wideSmash"] = 113,
            ["mountingCharge"] = 254, ["grandSmash"] = 114, ["heavySmash"] = 93,
            ["assaultingBlow"] = 94, ["rowSmash"] = 95, ["singleShot"] = 169, ["dualShot"] = 170,
            ["rowShot"] = 171, ["powerBolt"] = 186, ["toxicBolt"] = 187, ["heavyBolt"] = 188,
            ["passiveSteal"] = 52, ["toxicThrow"] = 54, ["shadowbite"] = 55, ["activeSteal"] = 53,
            ["assaultingLance"] = 82, ["wildRush"] = 83, ["pileThrust"] = 84, ["hache"] = 69,
            ["rowHeal"] = 213, ["saintsBlade"] = 70, ["vengefulAxe"] = 109, ["venomAxe"] = 110,
            ["darkFlame"] = 111, ["heal"] = 211, ["sacredHeal"] = 214, ["passiveCurse"] = 235,
            ["offensiveCurse"] = 238, ["defensiveCurse"] = 239, ["compoundingCurse"] = 240,
            ["fireball"] = 191, ["thunderousStrike"] = 197, ["volcano"] = 198, ["icebolt"] = 192,
            ["magicMissile"] = 193, ["iceCoffin"] = 194, ["divingThrust"] = 89, ["fireBreath"] = 90,
            ["tempestDive"] = 91, ["highSwing"] = 97, ["fatalDive"] = 99, ["aerialSmite"] = 98,
            ["lightningBlade"] = 57, ["naturesWrath"] = 255, ["mirageStab"] = 58, ["windArrow"] = 181,
            ["mysticConferral"] = 257, ["icicleArrow"] = 182, ["finishingStab"] = 62,
            ["decimate"] = 63, ["wildFang"] = 64, ["piercingLance"] = 86, ["passiveHold"] = 87,
            ["venomThrust"] = 88, ["bearCrush"] = 103, ["roundSwing"] = 104, ["earthshaker"] = 105,
            ["auroraVeil"] = 222, ["nightVision"] = 231, ["extraHeal"] = 218, ["spiralSword"] = 65,
            ["shieldSmite"] = 66, ["honedSlash"] = 67, ["delayingShot"] = 183, ["saintsShot"] = 184,
            ["photonArrow"] = 185, ["overheal"] = 219, ["honedHealing"] = 226, ["holyCradle"] = 227,
            ["impulse"] = 68, ["rowResistance"] = 250, ["mysticShield"] = 252, ["slice"] = 71,
            ["divineCross"] = 72, ["banishingStab"] = 127, ["defensiveOrder"] = 234,
            ["offensiveOrder"] = 233, ["assassinsNail"] = 138, ["doubleBlast"] = 137,
            ["grislyFire"] = 133, ["grislyPoison"] = 132, ["activeShatter"] = 166,
            ["sonicBlast"] = 136, ["fireSlash"] = 131, ["poisonSlash"] = 130,
            ["provokingSlash"] = 129, ["leapingSlash"] = 128, ["beastslayer"] = 160,
            ["banishingSmite"] = 155, ["painfulSmash"] = 154, ["giantSwing"] = 153,
            ["powerfulImpact"] = 151, ["icyCrush"] = 142, ["desperation"] = 112,
            ["groundStrike"] = 96, ["execution"] = 150, ["icyBlow"] = 141, ["spikedBlow"] = 140,
            ["crush"] = 139, ["penetrate"] = 147, ["spikedBolt"] = 190, ["harpoonBolt"] = 189,
            ["checkmate"] = 177, ["arrowRain"] = 173, ["tripleShatter"] = 172,
            ["magicShatter"] = 165, ["armorShatterII"] = 164, ["strengthShatterII"] = 162,
            ["flameArrow"] = 179, ["poisonArrow"] = 178, ["passiveShatter"] = 167,
            ["armorShatter"] = 163, ["strengthShatter"] = 161, ["reincarnation"] = 229,
            ["radiantHeal"] = 216, ["sandstorm"] = 242, ["trinityRain"] = 206,
            ["earthquake"] = 203, ["phantomVeil"] = 223, ["resurrection"] = 228,
            ["limitedHeal"] = 220, ["activeHeal"] = 217, ["circleHeal"] = 215,
            ["dispelAll"] = 241, ["holySmite"] = 245, ["cursedStrike"] = 244, ["gravity"] = 205,
            ["blizzard"] = 204, ["skywardThrust"] = 202, ["sanguineDarts"] = 201,
            ["doubleHeal"] = 212, ["fireCurse"] = 237, ["poisonCurse"] = 236, ["magicWall"] = 248,
            ["wall"] = 246, ["bulwark"] = 247, ["holyLight"] = 199, ["curingCall"] = 224,
            ["innocentRay"] = 200, ["primusEdge"] = 123, ["sylphicWind"] = 256,
            ["elementalRoar"] = 126, ["faerieHeal"] = 221, ["mysticArrow"] = 175,
            ["sonicShaft"] = 176, ["glacialRain"] = 174, ["omegaShatter"] = 258,
            ["burningEdge"] = 76, ["deathSpin"] = 102, ["inferno"] = 77, ["carnage"] = 100,
            ["viciousTorment"] = 101, ["flameThrust"] = 121, ["deadlyRush"] = 122,
            ["darkMist"] = 253, ["aquaVenom"] = 195, ["maelstrom"] = 196, ["healingWind"] = 225,
            ["sorcerousBlow"] = 243, ["glowingLight"] = 232, ["shadowArrow"] = 180,
            ["firstAid"] = 312, ["keenCall"] = 416, ["luminousCover"] = 439, ["rapidOrder"] = 274,
            ["nobleGuard"] = 467, ["arrowCover"] = 437, ["quickGuard"] = 461, ["provoke"] = 277,
            ["activeGift"] = 343, ["partingBlow"] = 306, ["frenziedStrike"] = 353,
            ["warHorn"] = 276, ["hastenedStrike"] = 297, ["parry"] = 482,
            ["chargedImpetus"] = 317, ["followingSlash"] = 351, ["vengefulGuard"] = 469,
            ["bullForce"] = 318, ["heavyCover"] = 433, ["guardian"] = 457, ["rowCover"] = 436,
            ["bulkUp"] = 345, ["wideCounter"] = 360, ["berserk"] = 346, ["bindingGuard"] = 339,
            ["enrage"] = 355, ["heavyCounter"] = 350, ["eagleEye"] = 334, ["pursuit"] = 391,
            ["aerialSnipe"] = 352, ["medicalAid"] = 313, ["quickReload"] = 326,
            ["aidCover"] = 440, ["evade"] = 475, ["sneakingEdge"] = 300, ["cavalierCall"] = 331,
            ["knightsPursuit"] = 393, ["magicBarrier"] = 428, ["holyGuard"] = 470,
            ["rowBarrier"] = 429, ["vengeance"] = 454, ["sanguineArts"] = 340,
            ["demonicPact"] = 319, ["quickHeal"] = 370, ["refresh"] = 383, ["lifesaver"] = 452,
            ["partingResurrection"] = 316, ["quickCurse"] = 408, ["cursedSwamp"] = 282,
            ["magicCounter"] = 357, ["magicPursuit"] = 392, ["concentrate"] = 328,
            ["magicConferral"] = 445, ["focusSight"] = 414, ["quickCast"] = 272,
            ["groundCounter"] = 349, ["deflect"] = 481, ["dragonsRoar"] = 280,
            ["wingRest"] = 315, ["feathering"] = 332, ["gryphonGlide"] = 480,
            ["sylphicBarrier"] = 421, ["removeWeakness"] = 388, ["evasiveImpetus"] = 460,
            ["quickCure"] = 384, ["selflessHeal"] = 373, ["pureField"] = 292,
            ["nocturnalStrike"] = 299, ["bestralHowl"] = 330, ["killingPursuit"] = 400,
            ["nocturnalEvade"] = 478, ["shadowPursuit"] = 401, ["weaknessHunter"] = 406,
            ["heavyGuard"] = 462, ["nocturnalRest"] = 327, ["lifeBlow"] = 309,
            ["restore"] = 342, ["quickDispel"] = 386, ["circleBarrier"] = 425,
            ["accelerate"] = 347, ["diurnalGuard"] = 471, ["discharge"] = 338,
            ["aerialPursuit"] = 394, ["tailwind"] = 325, ["shiningLight"] = 410,
            ["hastenedHeal"] = 295, ["preemptiveHeal"] = 411, ["holyBreath"] = 412,
            ["sacrifice"] = 443, ["reflectMagic"] = 407, ["mirrorWeakness"] = 390,
            ["holyBarrier"] = 426, ["guardOrder"] = 444, ["snipingOrder"] = 415,
            ["artenieStrike"] = 298, ["shatteringPursuit"] = 397, ["phalanxShift"] = 367,
            ["aerialShift"] = 368, ["healingHunter"] = 403, ["rapidShot"] = 301,
            ["magicBurst"] = 363, ["protection"] = 424, ["quickBarrier"] = 423,
            ["sacredCure"] = 385, ["reheal"] = 293, ["magicShell"] = 266,
            ["hastenedCurse"] = 281, ["guardHunter"] = 404, ["poisonBurst"] = 308,
            ["fireBurst"] = 307, ["hastenedCover"] = 435, ["enduringGuard"] = 463,
            ["radiantCover"] = 438, ["quickCover"] = 432, ["nocturnalGuard"] = 472,
            ["royalGuard"] = 468, ["bestralGuard"] = 466, ["aerialGuard"] = 465,
            ["cavalryGuard"] = 464, ["lifeshare"] = 375, ["enduringCover"] = 434,
            ["invincible"] = 474, ["provokingWall"] = 278, ["bunkerStance"] = 263,
            ["maidensHammer"] = 356, ["ironVeil"] = 291, ["undyingWill"] = 333,
            ["passiveMiracle"] = 374, ["saintsBarrier"] = 427, ["divineBlessing"] = 294,
            ["curingHeal"] = 371, ["rageOfTheFaeries"] = 304, ["elementalImpetus"] = 320,
            ["boonOfTheFaeries"] = 296, ["tripleCounter"] = 361, ["snowWhiteStrike"] = 398,
            ["wildKick"] = 405, ["hawkEye"] = 335, ["advanceCover"] = 441,
            ["phantomCounter"] = 359, ["partingDeath"] = 310, ["sanguinePursuit"] = 399,
            ["eyeOfTheWarriorPrincess"] = 283, ["painbringer"] = 453, ["scornfulDead"] = 279,
            ["evilCover"] = 442, ["darkStep"] = 479, ["darkConferral"] = 450,
            ["cursedGaol"] = 409, ["ominousWall"] = 284, ["reinforce"] = 420,
            ["quickImpetus"] = 344, ["recast"] = 329, ["hastenedCharge"] = 269,
            ["hastenedCast"] = 273, ["hastenedAction"] = 271, ["sanguineConferral"] = 449,
            ["thunderousConferral"] = 448, ["iceConferral"] = 447, ["flameConferral"] = 446,
            ["dynamicGlide"] = 477, ["phantomStep"] = 476, ["mightyShield"] = 430,
            ["grantEvade"] = 422, ["wideInspiration"] = 419, ["passiveGift"] = 341,
            ["nocturnalImpetus"] = 322, ["diurnalImpetus"] = 321, ["bearsDen"] = 290,
            ["wolfPack"] = 289, ["dawnHorn"] = 275, ["relicHeal"] = 372, ["partyAid"] = 314,
            ["widePursuit"] = 396, ["hastenedShatter"] = 302, ["magicConversion"] = 483,
            ["guardingImpetus"] = 459, ["vengefulImpetus"] = 458, ["guardingFighter"] = 456,
            ["nimbleFighter"] = 455, ["toughness"] = 451, ["inspiration"] = 418,
            ["powerfulCall"] = 417, ["sorcerousConnection"] = 413, ["cursedImpetus"] = 389,
            ["passiveCure"] = 382, ["guardCure"] = 381, ["stunCure"] = 379, ["blindCure"] = 380,
            ["freezeCure"] = 378, ["poisonCure"] = 376, ["fireCure"] = 377,
            ["aerialWing"] = 337, ["bladeDance"] = 324, ["graveImpetus"] = 323,
            ["unitedFront"] = 288, ["aerialAlignment"] = 287, ["heavyAlignment"] = 286,
            ["quickAction"] = 270, ["warCry"] = 265, ["impetusStance"] = 264,
            ["healingPursuit"] = 402, ["selfCare"] = 369, ["banishingCounter"] = 358,
            ["vanitas"] = 354, ["counter"] = 348, ["banishingPursuit"] = 395,
        };

        private static readonly Dictionary<string, uint> ConditionKeyToId = new()
        {
            ["Highest HP"] = 12, ["Lowest HP"] = 13, ["Highest % HP"] = 14, ["Lowest % HP"] = 15,
            ["Target HP is >25%"] = 19, ["Target HP is >50%"] = 20, ["Target HP is >75%"] = 21,
            ["Target HP is >100%"] = 22, ["Target HP is <25%"] = 16, ["Target HP is <50%"] = 17,
            ["Target HP is <75%"] = 18, ["Target HP is <100%"] = 23,
            ["Average HP is >25%"] = 27, ["Average HP is >50%"] = 28, ["Average HP is >75%"] = 29,
            ["Average HP is >100%"] = 30, ["Average HP is <25%"] = 24, ["Average HP is <50%"] = 25,
            ["Average HP is <75%"] = 26, ["Average HP is <100%"] = 31,
            ["Own HP is <25%"] = 105, ["Own HP is <50%"] = 106, ["Own HP is <75%"] = 107,
            ["Own HP is <100%"] = 112, ["Own HP is >25%"] = 108, ["Own HP is >50%"] = 109,
            ["Own HP is >75%"] = 110, ["Own HP is >100%"] = 111,
            ["Most AP"] = 113, ["Least AP"] = 114, ["0 AP"] = 115,
            ["1 or Less AP"] = 116, ["2 or Less AP"] = 117, ["3 or Less AP"] = 118,
            ["1 or More AP"] = 119, ["2 or More AP"] = 120, ["3 or More AP"] = 121, ["4 or More AP"] = 122,
            ["Most PP"] = 123, ["Least PP"] = 124, ["0 PP"] = 125,
            ["1 or Less PP"] = 126, ["2 or Less PP"] = 127, ["3 or Less PP"] = 128,
            ["1 or More PP"] = 129, ["2 or More PP"] = 130, ["3 or More PP"] = 131, ["4 or More PP"] = 132,
            ["Own AP is 0"] = 133, ["Own AP is 1 or Less"] = 134, ["Own AP is 2 or Less"] = 135,
            ["Own AP is 3 or Less"] = 136, ["Own AP is 1 or More"] = 137, ["Own AP is 2 or More"] = 138,
            ["Own AP is 3 or More"] = 139, ["Own AP is 4 or More"] = 140,
            ["Own PP is 0"] = 141, ["Own PP is 1 or Less"] = 142, ["Own PP is 2 or Less"] = 143,
            ["Own PP is 3 or Less"] = 144, ["Own PP is 1 or More"] = 145, ["Own PP is 2 or More"] = 146,
            ["Own PP is 3 or More"] = 147, ["Own PP is 4 or More"] = 148,
            ["Prioritize Infantry"] = 54, ["Prioritize Cavalry"] = 55, ["Prioritize Flying"] = 56,
            ["Prioritize Armored"] = 57, ["Prioritize Scout"] = 58, ["Prioritize Archer"] = 59,
            ["Prioritize Caster"] = 60, ["Prioritize Elven"] = 61, ["Prioritize Bestral"] = 62,
            ["Prioritize Angel"] = 63,
            ["Infantry"] = 64, ["Cavalry"] = 65, ["Flying"] = 66, ["Armored"] = 67,
            ["Scout"] = 68, ["Archer"] = 69, ["Caster"] = 70, ["Elven"] = 71,
            ["Bestral"] = 72, ["Angel"] = 73,
            ["Prioritize Buffed"] = 32, ["Prioritize Debuffed"] = 33,
            ["Buffed"] = 34, ["Debuffed"] = 35, ["Afflicted"] = 36,
            ["Poisoned"] = 37, ["Burning"] = 38, ["Frozen"] = 39, ["Stunned"] = 40,
            ["Blinded"] = 41, ["Passive Sealed"] = 42, ["Guard Sealed"] = 43,
            ["Not Buffed"] = 44, ["Not Debuffed"] = 45, ["Not Afflicted"] = 46,
            ["Not Poisoned"] = 47, ["Not Burning"] = 48, ["Not Frozen"] = 49,
            ["Not Stunned"] = 50, ["Not Blinded"] = 51, ["Not Passive Sealed"] = 52,
            ["Not Guard Sealed"] = 53,
            ["No Infantry Enemies"] = 168, ["No Cavalry Enemies"] = 169, ["No Flying Enemies"] = 170,
            ["No Armored Enemies"] = 171, ["No Scout Enemies"] = 172, ["No Archer Enemies"] = 173,
            ["No Caster Enemies"] = 174, ["No Elven Enemies"] = 175, ["No Bestral Enemies"] = 176,
            ["No Angel Enemies"] = 177,
            ["Infantry Enemies Present"] = 158, ["Cavalry Enemies Present"] = 159,
            ["Flying Enemies Present"] = 160, ["Armored Enemies Present"] = 161,
            ["Scout Enemies Present"] = 162, ["Archer Enemies Present"] = 163,
            ["Caster Enemies Present"] = 164, ["Elven Enemies Present"] = 165,
            ["Bestral Enemies Present"] = 166, ["Angel Enemies Present"] = 167,
            ["Prioritize Front Row"] = 1, ["Prioritize Back Row"] = 2,
            ["Front Row"] = 3, ["Back Row"] = 4, ["Full Column"] = 5,
            ["Row with Most Combatants"] = 6, ["Row with Least Combatants"] = 7,
            ["Row with 2+ Combatants"] = 8, ["Row with 3+ Combatants"] = 9,
            ["Daytime"] = 10, ["Nighttime"] = 11,
            ["Physically Attacked"] = 74, ["Magically Attacked"] = 75,
            ["Row is Attacked"] = 76, ["Column is Attacked"] = 77,
            ["All Allies are Attacked"] = 78,
            ["Attacked by Infantry"] = 79, ["Attacked by Cavalry"] = 80, ["Attacked by Flying"] = 81,
            ["Attacked by Armored"] = 82, ["Attacked by Scout"] = 83, ["Attacked by Archer"] = 84,
            ["Attacked by Caster"] = 85, ["Attacked by Elven"] = 86, ["Attacked by Bestral"] = 87,
            ["Attacked by Angel"] = 88,
            ["2 or More Enemies"] = 89, ["3 or More Enemies"] = 90, ["4 or More Enemies"] = 91,
            ["5 or More Enemies"] = 92, ["1 or Fewer Enemies"] = 93, ["2 or Fewer Enemies"] = 94,
            ["3 or Fewer Enemies"] = 95, ["4 or Fewer Enemies"] = 96,
            ["2 or More Allies"] = 97, ["3 or More Allies"] = 98, ["4 or More Allies"] = 99,
            ["5 or More Allies"] = 100, ["1 or Fewer Allies"] = 101, ["2 or Fewer Allies"] = 102,
            ["3 or Fewer Allies"] = 103, ["4 or Fewer Allies"] = 104,
            ["User"] = 149, ["Other Combatants"] = 150,
            ["User is Buffed"] = 151, ["User is Debuffed"] = 152,
            ["First Action"] = 153, ["Second Action"] = 154, ["Third Action"] = 155,
            ["Fourth Action"] = 156, ["Fifth Action"] = 157,
            ["Highest Max AP"] = 179, ["Highest Max PP"] = 180, ["Highest Max HP"] = 178,
            ["Highest Phys. ATK"] = 181, ["Highest Phys. DEF"] = 183,
            ["Highest Mag. ATK"] = 182, ["Highest Mag. DEF"] = 184,
            ["Highest Accuracy"] = 186, ["Highest Evasion"] = 187,
            ["Highest Crit. Rate"] = 188, ["Highest Guard Rate"] = 189, ["Highest Initiative"] = 185,
            ["Lowest Max AP"] = 191, ["Lowest Max PP"] = 192, ["Lowest Max HP"] = 190,
            ["Lowest Phys. ATK"] = 193, ["Lowest Phys. DEF"] = 195,
            ["Lowest Mag. ATK"] = 194, ["Lowest Mag. DEF"] = 196,
            ["Lowest Accuracy"] = 198, ["Lowest Evasion"] = 199,
            ["Lowest Crit. Rate"] = 200, ["Lowest Guard Rate"] = 201, ["Lowest Initiative"] = 197,
        };

        private static readonly Dictionary<string, uint> ItemKeyToId = new()
        {
            ["banditLongsword"] = 335, ["barbariansMight"] = 319, ["baroqueSword"] = 284,
            ["blackIronSword"] = 287, ["bronzeSword"] = 282, ["carnatSword"] = 288,
            ["carnelianBlade"] = 312, ["crimsonEpee"] = 321, ["cutthroatsBoon"] = 327,
            ["dancersDelight"] = 320, ["dragonboneBlade"] = 296, ["flamberge"] = 318,
            ["galecutter"] = 324, ["greatsword"] = 337, ["greatwoodSword"] = 289,
            ["hailstormEdge"] = 325, ["hallowedBlade"] = 311, ["heavenswingSword"] = 305,
            ["holyUnicornBlade"] = 348, ["huntersClaymore"] = 310, ["icefallBlade"] = 298,
            ["ironSword"] = 286, ["kingsbladeCornix"] = 344, ["meteoriteSword"] = 313,
            ["moonlightRapier"] = 328, ["notosSword"] = 345, ["phantomKnightsSword"] = 302,
            ["pursuantsBlade"] = 326, ["recruitsShortsword"] = 283, ["roseKnightSword"] = 334,
            ["rosularisSword"] = 346, ["royalSaber"] = 336, ["runicSword"] = 330,
            ["sacralSword"] = 347, ["sanguineBlade"] = 329, ["scorpionsSting"] = 316,
            ["searingRapier"] = 317, ["spellsteelSword"] = 291, ["steelSword"] = 290,
            ["stingray"] = 333, ["sylphsBane"] = 323, ["templarsSword"] = 301,
            ["thornBlade"] = 297, ["viperfang"] = 315, ["vorpalSword"] = 292,
            ["wingcrestBlade"] = 299, ["wyvernRazor"] = 332, ["zenoiranSword"] = 303,
            ["zenoiranKnightsSword"] = 304, ["banditsHandAxe"] = 438, ["banishingHammer"] = 460,
            ["barbariansAxe"] = 452, ["baroqueAxe"] = 417, ["blackIronAxe"] = 420,
            ["boareasAxe"] = 477, ["bronzeAxe"] = 415, ["carnatAxe"] = 421,
            ["carnelianAxe"] = 446, ["crushingAxe"] = 449, ["darkKnightCleaver"] = 461,
            ["dragonboneAxe"] = 429, ["eliminator"] = 456, ["frozenBattleAxe"] = 451,
            ["giantsGreatAxe"] = 459, ["goldenRamAxe"] = 439, ["greatwoodAxe"] = 422,
            ["headsmansAxe"] = 455, ["helleborusAxe"] = 478, ["hoarfrostAxe"] = 450,
            ["huntersHalberd"] = 444, ["icefallAxe"] = 431, ["ironAxe"] = 419,
            ["juggernaut"] = 474, ["kingsAxeDrakenash"] = 476, ["knotOfRuin"] = 445,
            ["labrys"] = 468, ["masonsHammer"] = 453, ["meteoriteAxe"] = 447,
            ["morningstar"] = 457, ["phantomKnightsAxe"] = 435, ["recruitsHandAxe"] = 416,
            ["roseKnightAxe"] = 466, ["roseKnightHammer"] = 467, ["sacralAxe"] = 479,
            ["sanguineAxe"] = 463, ["silvermoonAxe"] = 458, ["spellsteelAxe"] = 424,
            ["spikedHammer"] = 454, ["steelAxe"] = 423, ["templarsAxe"] = 434,
            ["thornAxe"] = 430, ["vorpalAxe"] = 425, ["warhammer"] = 469,
            ["wingcrestAxe"] = 432, ["zenoiranAxe"] = 436, ["zenoiranKnightAxe"] = 437,
            ["adeptsShortspear"] = 381, ["baroqueSpear"] = 351, ["blackIronSpear"] = 354,
            ["bloodySpear"] = 397, ["bronzeSpear"] = 349, ["canyonLance"] = 394,
            ["carnatSpear"] = 355, ["carnelianSpear"] = 383, ["corrodedSpear"] = 388,
            ["crimsonSarissa"] = 403, ["deathPillar"] = 405, ["dragonboneSpear"] = 363,
            ["dragoonsWarspear"] = 396, ["elfeater"] = 392, ["flameJavelin"] = 404,
            ["generalsPike"] = 393, ["glaive"] = 380, ["greatwoodSpear"] = 356,
            ["helixSpear"] = 395, ["icefallSpear"] = 365, ["ironSpear"] = 353,
            ["kingslanceElhal"] = 410, ["meteoriteLance"] = 384, ["namelessGuardsSpear"] = 368,
            ["orchisSpear"] = 412, ["phantomKnightsSpear"] = 371, ["recruitsShortspear"] = 350,
            ["runicSpear"] = 398, ["sacralSpear"] = 413, ["silverTrident"] = 402,
            ["spellsteelSpear"] = 358, ["steelSpear"] = 357, ["stormOfHail"] = 387,
            ["surgingShortspear"] = 390, ["templarsSpear"] = 370, ["testarossa"] = 408,
            ["thornSpear"] = 364, ["twinnedBoughIce"] = 400, ["twinnedBoughLightning"] = 401,
            ["unwaveringSpear"] = 386, ["valkyriesPartisan"] = 389, ["vorpalSpear"] = 359,
            ["watchmansLongspear"] = 367, ["wingcrestSpear"] = 366, ["zenoiranSpear"] = 372,
            ["zenoiranKnightsSpear"] = 373, ["zephyrosSpear"] = 411, ["alminster"] = 546,
            ["apeliotiesBow"] = 543, ["baroqueBow"] = 483, ["blackIronBow"] = 486,
            ["bowOfSwiftness"] = 527, ["bronzeBow"] = 481, ["carnatBow"] = 487,
            ["carnelianBow"] = 510, ["craneBow"] = 530, ["desertBow"] = 537,
            ["dragonboneBow"] = 495, ["flameBow"] = 516, ["frostbloomBow"] = 538,
            ["gallianCrossbow"] = 532, ["generalsLongbow"] = 529, ["greatwoodBow"] = 488,
            ["greatwoodSpiritbow"] = 536, ["icefallBow"] = 497, ["infamousBow"] = 512,
            ["interrogatorsBow"] = 525, ["ironBow"] = 485, ["kingsbowBastorik"] = 542,
            ["meteoriteBow"] = 511, ["namelessGuardsBow"] = 500, ["phantomKnightsBow"] = 503,
            ["pheasantBow"] = 524, ["piercingBow"] = 521, ["piercingStrongbow"] = 522,
            ["quinceBow"] = 544, ["recruitsShortbow"] = 482, ["sacralBow"] = 545,
            ["salamandersGreatbow"] = 517, ["sanguineBow"] = 533, ["silvermoonBow"] = 528,
            ["spellsealBow"] = 523, ["spellsteelBow"] = 490, ["starlessBow"] = 518,
            ["steelBow"] = 489, ["templarsBow"] = 502, ["theInquisitor"] = 526,
            ["thornBow"] = 496, ["thunderingBow"] = 519, ["thunderingStrongbow"] = 520,
            ["trappersWarbow"] = 531, ["viperBow"] = 515, ["vorpalBow"] = 491,
            ["watchmansLongbow"] = 499, ["wingcrestBow"] = 498, ["woodpecker"] = 539,
            ["zenoiranBow"] = 504, ["zenoiranKnightsBow"] = 505, ["baroqueRod"] = 549,
            ["blackIronStaff"] = 552, ["bloodmoonRod"] = 584, ["bronzeStaff"] = 547,
            ["cardinalsMace"] = 622, ["carnatRod"] = 553, ["carnelianStaff"] = 576,
            ["chiropteranStaff"] = 610, ["chlorotic"] = 613, ["clericsCane"] = 592,
            ["defendersMace"] = 591, ["dragonboneStaff"] = 561, ["dustboundStaff"] = 606,
            ["einSeeker"] = 615, ["eurosStaff"] = 625, ["flameHexStaff"] = 605,
            ["gleamingMace"] = 608, ["grandMagusStaff"] = 587, ["greatwoodStaff"] = 554,
            ["hallowedMace"] = 594, ["icefallStaff"] = 563, ["icestormStaff"] = 588,
            ["ironStaff"] = 551, ["kingstaffAlbiore"] = 624, ["libera"] = 621,
            ["liberatorsStaff"] = 602, ["lifebloodStaff"] = 599, ["lupinusStaff"] = 626,
            ["lyricalWand"] = 593, ["meteoriteRod"] = 577, ["milleniumScepter"] = 620,
            ["motherLaeliasStaff"] = 611, ["papalCrosier"] = 628, ["pestilentStaff"] = 583,
            ["phantomKnightsStaff"] = 567, ["phosphorescentStaff"] = 597,
            ["poisonHexStaff"] = 604, ["preciousRod"] = 598, ["purifier"] = 600,
            ["quartzRod"] = 589, ["recruitsStaff"] = 548, ["redBarkStaff"] = 612,
            ["ringedStaff"] = 596, ["sacralRod"] = 627, ["shamansBonestaff"] = 607,
            ["speedhexStaff"] = 603, ["spellsteelStaff"] = 556, ["staffOfSuccor"] = 595,
            ["steelStaff"] = 555, ["telluricStaff"] = 586, ["templarsStaff"] = 566,
            ["thawingScepter"] = 582, ["thornStaff"] = 562, ["vorpalRod"] = 557,
            ["wardingStaff"] = 573, ["windScepter"] = 585, ["wingcrestStaff"] = 564,
            ["zenoiranStaff"] = 568, ["zenoiranKnightsStaff"] = 569,
            ["angelHuntersBuckler"] = 688, ["azureCrestShield"] = 671, ["baroqueShield"] = 644,
            ["battlersShield"] = 645, ["beastHuntersBuckler"] = 689, ["blackIronShield"] = 647,
            ["blessedRoundshield"] = 674, ["blueRoseShield"] = 699, ["bronzeShield"] = 642,
            ["carnatShield"] = 648, ["cavalryHuntersBuckler"] = 687, ["chivalricShield"] = 682,
            ["dragonboneShield"] = 656, ["goldenRamShield"] = 667, ["greatwoodShield"] = 649,
            ["guardsShield"] = 684, ["heavenwingShield"] = 668, ["hoarfrostShield"] = 678,
            ["holyKnightsShield"] = 686, ["holyUnicornShield"] = 706, ["huntersBuckler"] = 673,
            ["icefallShield"] = 658, ["ironShield"] = 646, ["ironcladBuckler"] = 694,
            ["kaikaiasShield"] = 705, ["luminousShield"] = 679, ["manalithBuckler"] = 695,
            ["mercenarysShield"] = 685, ["moonlightShield"] = 690, ["namelessGuardsShield"] = 661,
            ["parryingShield"] = 696, ["phantomKnightsShield"] = 664, ["recruitsShield"] = 643,
            ["scarletCrestShield"] = 672, ["searingShield"] = 677, ["spellsteelShield"] = 651,
            ["squiresShield"] = 692, ["steelShield"] = 650, ["templarsShield"] = 663,
            ["thornShield"] = 657, ["twinDragonShield"] = 680, ["unfetteredShield"] = 681,
            ["unyieldingShield"] = 691, ["viperShield"] = 676, ["vorpalShield"] = 652,
            ["watchmansBuckler"] = 660, ["whiteKnightsShield"] = 697, ["whiteRidersShield"] = 700,
            ["wingcrestShield"] = 659, ["zenoiranShield"] = 665, ["zenoiranKnightsShield"] = 666,
            ["azureCrestGreatshield"] = 737, ["baroqueGreatshield"] = 712,
            ["beastslayerGreatshield"] = 752, ["blackIronGreatshield"] = 715,
            ["blacksilverPavise"] = 758, ["bronzeGreatshield"] = 710, ["bulwarkTowershield"] = 756,
            ["carnatGreatshield"] = 716, ["championsTowershield"] = 768,
            ["citadelGuardsGreatshield"] = 747, ["converyableGreatshield"] = 713,
            ["dragonboneGreatshield"] = 724, ["drakestoneIceshield"] = 762, ["granBaris"] = 769,
            ["greatwoodGreatshield"] = 717, ["heavensMirrorGreatshield"] = 761,
            ["herosGreatshield"] = 767, ["hoarfrostGreatshield"] = 744,
            ["horsekillerGreatshield"] = 750, ["icefallGreatshield"] = 726,
            ["ironGreatshield"] = 714, ["ironcladTowershield"] = 755,
            ["luminousGreatshield"] = 745, ["mirageGreatshield"] = 739,
            ["moonlightGreatshield"] = 748, ["panopliedGreatshield"] = 754,
            ["phantomKnightsGreatshield"] = 730, ["recruitsGreatshield"] = 711,
            ["searingGreatshield"] = 743, ["skironsGreatshield"] = 766,
            ["spellsteelGreatshield"] = 719, ["squiresGreatshield"] = 753,
            ["steelGreatshield"] = 718, ["templarsGreatshield"] = 729,
            ["thornGreatshield"] = 725, ["unyieldingGreatshield"] = 749,
            ["viperGreatshield"] = 742, ["vorpalGreatshield"] = 720,
            ["whiteKnightsGreatshield"] = 757, ["wingclipperGreatshield"] = 751,
            ["wingcrestGreatshield"] = 727, ["zenoiranGreatshield"] = 731,
            ["zenoiranKnightsGreatshield"] = 732,
            ["acrobatsShoes"] = 854, ["amethystPendant"] = 788, ["angelPlume"] = 807,
            ["archbishopsMitre"] = 900, ["armoredGauntlets"] = 887, ["auroraRing"] = 867,
            ["awakeningAmulet"] = 929, ["badgerGauntlets"] = 859, ["barbedRibbon"] = 938,
            ["battlelineStandard"] = 855, ["blackCatEarHood"] = 955, ["bloodbrandTome"] = 874,
            ["bloodmoonEarrings"] = 844, ["blueSpectacles"] = 811, ["bronzeBangle"] = 790,
            ["bronzeCirclet"] = 793, ["brownBeret"] = 796, ["carnelianPendant"] = 784,
            ["celestialTalisman"] = 951, ["charmOfLandAndSea"] = 833, ["charmOfSunAndMoon"] = 834,
            ["charmOfWarding"] = 868, ["citadelGuardsSabatons"] = 920, ["chloesCharm"] = 942,
            ["clearsightAmulet"] = 930, ["clericsBracelet"] = 893, ["clericsMedallion"] = 831,
            ["clothGauntlets"] = 819, ["coolingBandana"] = 937, ["counterBelt"] = 894,
            ["coursersReins"] = 913, ["crudeTasset"] = 852, ["dancersBracelet"] = 876,
            ["dawnRobes"] = 905, ["defensiveGauntlets"] = 909, ["defensiveRing"] = 866,
            ["defrostingAmulet"] = 928, ["demonsShackles"] = 810, ["detoxifyingAmulet"] = 926,
            ["dovePlume"] = 805, ["druidsRobes"] = 921, ["duskRobes"] = 906,
            ["earringsOfPursuit"] = 846, ["eliteBeret"] = 798, ["eliteStandard"] = 857,
            ["erveldasTalisman"] = 804, ["familiarsChoker"] = 879, ["firebrandTome"] = 871,
            ["firstAidKit"] = 861, ["fluffyCape"] = 940, ["frostbrandTome"] = 872,
            ["gamblersCoin"] = 877, ["gauntlets"] = 818, ["glacialRing"] = 935,
            ["glorySash"] = 875, ["goldBangle"] = 792, ["goldCirclet"] = 795,
            ["goldenEgg"] = 944, ["gravekeeperBoots"] = 841, ["gravekeeperLantern"] = 840,
            ["greenBeret"] = 797, ["guardianGloves"] = 918, ["heavensteedReins"] = 914,
            ["heavenwyvernReins"] = 886, ["herosMedallion"] = 832, ["holyBroach"] = 822,
            ["holyUnicornSignet"] = 933, ["illusoryCloak"] = 912, ["ironShackles"] = 808,
            ["knightsMedallion"] = 830, ["lapisBell"] = 903, ["lapisPendant"] = 786,
            ["largeAidKit"] = 863, ["leafBroach"] = 820, ["leatherHood"] = 799,
            ["liberatorsBracelet"] = 890, ["lifebloodTalisman"] = 803, ["lionheart"] = 835,
            ["lipssRing"] = 849, ["luckyCoin"] = 817, ["magiaHeart"] = 953,
            ["magiaSoul"] = 952, ["magesGloves"] = 897, ["mastersGauntlets"] = 917,
            ["mercenaryEyepatch"] = 815, ["mirroredSpectacles"] = 812, ["misersBracelet"] = 945,
            ["mistletoeCharm"] = 836, ["necromancersLantern"] = 843, ["outlawsBracelet"] = 891,
            ["parryingAmulet"] = 931, ["phoenixPlume"] = 924, ["powerBelt"] = 908,
            ["prisonersShackles"] = 809, ["pursuantsBracelet"] = 889, ["quenchingAmulet"] = 927,
            ["ravenPlume"] = 806, ["retaliationEarrings"] = 845, ["ringOfTheMaiden"] = 964,
            ["ringOfTheUnicorn"] = 961, ["rookieEgg"] = 943, ["roseBroach"] = 821,
            ["royalScarf"] = 814, ["rubyPendant"] = 785, ["sacralBroach"] = 823,
            ["sagesHood"] = 801, ["sapphirePendant"] = 787, ["scarlettsRibbon"] = 826,
            ["selfAidKit"] = 902, ["shawlOfRepose"] = 864, ["silkHood"] = 800,
            ["silkenScarf"] = 813, ["silverBangle"] = 791, ["silverCirclet"] = 794,
            ["silverGoblet"] = 946, ["skillfulAmulet"] = 932, ["snipersLens"] = 883,
            ["snipersAmberLens"] = 884, ["soothingPlume"] = 923, ["sorcerersGauntlets"] = 922,
            ["sorcerersMedallion"] = 829, ["spiritsNecklace"] = 901, ["squallBracelet"] = 851,
            ["tailwindCape"] = 848, ["thievesBell"] = 870, ["thievesMantle"] = 910,
            ["thunderbrandTome"] = 873, ["twilightCloak"] = 911, ["undeadRing"] = 842,
            ["vengefulCaligae"] = 919, ["veteransEyepatch"] = 816, ["vitalityTalisman"] = 802,
            ["warriorsMedallion"] = 828, ["watchmansHorn"] = 839, ["watchmansLantern"] = 838,
            ["whiteCatEarHood"] = 954, ["winglineStandard"] = 856, ["wolfpackGauntlets"] = 858,
            ["woolyMittens"] = 939, ["wyvernClaw"] = 896, ["wyvernReins"] = 885,
            ["lamplightRing"] = 837, ["oldWitchsRing"] = 915, ["dirtyGamblersCoin"] = 878,
            ["dancersAnklet"] = 904, ["liberatorsBelt"] = 898, ["onyxPendant"] = 789,
            ["salamanderRing"] = 934, ["thunderclapRing"] = 936, ["dreamCrown"] = 956,
            ["ancientCrown"] = 957, ["monksMitre"] = 899, ["windFaeriesBell"] = 869,
            ["sageOwlsShawl"] = 881, ["goldGoblet"] = 947,
        };

        // ── Growth type name → value ──────────────────────────────────────────

        private static readonly Dictionary<string, uint> GrowthTypeNames = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Hardy"]       = 1, ["Offensive"]  = 2, ["Defensive"] = 3,
            ["Precise"]     = 4, ["Lucky"]       = 5, ["Keen"]      = 6,
            ["Guardian"]    = 7, ["Go-getter"]   = 8, ["All-rounder"] = 9,
        };

        // ── RNG for generic name index ────────────────────────────────────────

        private static readonly Random Rng = new();

        // ── Public entry point ────────────────────────────────────────────────

        /// <summary>
        /// Reads the JSON at <paramref name="jsonPath"/> and writes teams 1–4
        /// into the save.  Returns a list of warning strings (unknown keys etc.).
        /// Throws on unrecoverable errors (bad JSON, missing playerTeams etc.).
        /// </summary>
        public static (List<string> Warnings, List<uint> NewCharacterIds) Import(string jsonPath)
        {
            var warnings = new List<string>();
            var newIds = new List<uint>();
            string raw = System.IO.File.ReadAllText(jsonPath);
            var root = JsonNode.Parse(raw) ?? throw new Exception("Invalid JSON");

            var playerTeams   = root["playerTeams"]   as JsonObject
                ?? throw new Exception("Missing 'playerTeams' in JSON");
            var playerRosters = root["playerRosters"] as JsonObject
                ?? throw new Exception("Missing 'playerRosters' in JSON");

            DeleteCharactersInUnits1To4(warnings);

            // Process players 1–4 → game units 01–04
            for (int player = 1; player <= 4; player++)
            {
                string key = player.ToString();
                var teamNode   = playerTeams[key]   as JsonObject;
                var rosterNode = playerRosters[key] as JsonObject;
                if (teamNode == null || rosterNode == null)
                {
                    warnings.Add($"Player {player}: missing team or roster data, skipping.");
                    continue;
                }

                var formationNode = teamNode["formation"] as JsonArray;
                if (formationNode == null)
                {
                    warnings.Add($"Player {player}: missing 'formation' array, skipping.");
                    continue;
                }

                ImportUnit(player, formationNode, rosterNode, warnings, newIds);
            }

            return (warnings, newIds);
        }

        // ── Per-unit import ───────────────────────────────────────────────────

        private static void ImportUnit(
            int playerNum,
            JsonArray formationNode,
            JsonObject rosterNode,
            List<string> warnings,
            List<uint> newIds)
        {
            int unitIndex = playerNum - 1;  // 0-based index into UnitIdentifiers
            uint unitIdentifier = UnitIdentifiers[unitIndex];

            // Step 1: DeleteCharactersInUnits1To4 already ran at import start.
            // Step 2: For each formation slot, create the character and assign it.
            for (int slot = 0; slot < 6 && slot < formationNode.Count; slot++)
            {
                var unitIdNode = formationNode[slot];
                if (unitIdNode == null || unitIdNode.GetValueKind() == JsonValueKind.Null)
                    continue;

                string unitId = unitIdNode.ToString();
                var charDef = rosterNode[unitId] as JsonObject;
                if (charDef == null)
                {
                    warnings.Add($"Player {playerNum} slot {slot}: unit '{unitId}' not found in roster, skipping.");
                    continue;
                }

                uint charIdx = AllocateCharacterSlot(warnings);
                if (charIdx == uint.MaxValue)
                {
                    warnings.Add($"Player {playerNum} slot {slot}: no free character slots.");
                    break;
                }

                uint mappedSlot = slot < 3 ? (uint)slot + 3 : (uint)slot - 3;
                WriteCharacter(charIdx, charDef, unitIdentifier, mappedSlot, playerNum, warnings, newIds,
                    $"Player {playerNum} unit '{unitId}'");
                WriteUnitSlot(unitIndex, (int)mappedSlot, charIdx);
            }
        }

        // ── Unit block helpers ────────────────────────────────────────────────

        private const uint InventoryBase   = 0xA0;
        private const uint InventoryStride = 20;
        private const uint InventorySlots  = 3800;

        /// <summary>
        /// Frees inventory entries for the given character by zeroing each equipped
        /// item slot (first 4 bytes = 0 marks the slot as free). Equipment indices
        /// are read from char+76, 80, 84, 88 (1-based). Inventory at +12 stores
        /// char slot index as 1 byte; slot indices &gt; 255 are ambiguous.
        /// </summary>
        private static void FreeCharacterEquipment(SaveData sd, uint charSlotIndex)
        {
            uint charAddr = Util.calcCharacterAddress(charSlotIndex);
            for (int s = 0; s < 4; s++)
            {
                uint invIdx = sd.ReadNumber(charAddr + 76 + (uint)(s * 4), 4);
                if (invIdx == 0 || invIdx > InventorySlots) continue;
                uint itemAddr = InventoryBase + (invIdx - 1) * InventoryStride;
                sd.WriteNumber(itemAddr, 4, 0);
            }
        }

        /// <summary>
        /// Deletes one character: frees their equipped inventory entries, then zeros
        /// the full 464-byte block and sets id to 0xFFFFFFFF so the slot is empty.
        /// </summary>
        private static void DeleteCharacter(SaveData sd, uint charSlotIndex)
        {
            FreeCharacterEquipment(sd, charSlotIndex);
            uint charAddr = Util.calcCharacterAddress(charSlotIndex);
            sd.WriteValue(charAddr, new byte[CharBlockSize]);
            sd.WriteNumber(charAddr, 4, 0xFFFFFFFF);
        }

        /// <summary>
        /// Deletes all characters assigned to units 1-4 (identifiers 2, 11, 16, 29),
        /// frees their equipment in inventory, clears the four unit formation slots,
        /// and recomputes AddrCharCount.
        /// </summary>
        private static void DeleteCharactersInUnits1To4(List<string> warnings)
        {
            var sd = SaveData.Instance();
            for (uint ci = 0; ci < MaxCharacters; ci++)
            {
                uint charAddr = Util.calcCharacterAddress(ci);
                uint id = sd.ReadNumber(charAddr, 4);
                if (id == 0xFFFFFFFF) break;

                uint assignedUnit = sd.ReadNumber(charAddr + 4, 4);
                if (assignedUnit == 2 || assignedUnit == 11 || assignedUnit == 16 || assignedUnit == 29)
                    DeleteCharacter(sd, ci);
            }

            for (int s = 0; s < 6; s++)
                sd.WriteNumber(Unit01Base + 4 + (uint)(s * 4), 4, 0xFFFFFFFF);
            for (int unitIndex = 1; unitIndex <= 3; unitIndex++)
            {
                uint blockBase = UnitBlockBase + (uint)(unitIndex - 1) * UnitBlockStride;
                for (int s = 0; s < 6; s++)
                    sd.WriteNumber(blockBase + UnitSlotOffset + (uint)(s * 4), 4, 0xFFFFFFFF);
            }

            uint maxSlotIndex = 0;
            bool anyUsed = false;
            for (uint ci = 0; ci < MaxCharacters; ci++)
            {
                uint id = sd.ReadNumber(Util.calcCharacterAddress(ci), 4);
                if (id == 0xFFFFFFFF) continue;
                anyUsed = true;
                maxSlotIndex = ci;
            }
            sd.WriteNumber(AddrCharCount, 4, anyUsed ? maxSlotIndex + 1 : 0);
        }

        /// <summary>
        /// Clears formation slots and unlinks any characters currently assigned
        /// to this unit.
        /// </summary>
        private static void ClearUnit(int unitIndex, uint unitIdentifier, List<string> warnings)
        {
            var sd = SaveData.Instance();

            // Walk all character slots and detach those belonging to this unit.
            for (uint ci = 0; ci < MaxCharacters; ci++)
            {
                uint charAddr = Util.calcCharacterAddress(ci);
                uint id = sd.ReadNumber(charAddr, 4);
                if (id == 0xFFFFFFFF) break;

                uint assignedUnit = sd.ReadNumber(charAddr + 4, 4);
                if (assignedUnit == unitIdentifier)
                {
                    // Clear unit assignment fields on the character.
                    sd.WriteNumber(charAddr + 4,  4, 0xFFFFFFFF);  // char+0x04: unit identifier
                    sd.WriteNumber(charAddr + 32, 1, 0xFF);         // char+0x20: formation slot
                    // Clear formation-join bit (bit 0 of byte 460).
                    uint status = sd.ReadNumber(charAddr + 460, 1);
                    sd.WriteNumber(charAddr + 460, 1, status & ~1u);
                }
            }

            // Zero all 6 formation slots in the unit block.
            if (unitIndex == 0)
            {
                // Unit 01: special fixed address
                for (int s = 0; s < 6; s++)
                    sd.WriteNumber(Unit01Base + 4 + (uint)(s * 4), 4, 0xFFFFFFFF);
            }
            else
            {
                uint blockBase = UnitBlockBase + (uint)(unitIndex - 1) * UnitBlockStride;
                for (int s = 0; s < 6; s++)
                    sd.WriteNumber(blockBase + UnitSlotOffset + (uint)(s * 4), 4, 0xFFFFFFFF);
            }
        }

        /// <summary>
        /// Writes the charID (1-based save ID, not slot index) into the unit block's
        /// formation slot.
        /// </summary>
        private static void WriteUnitSlot(int unitIndex, int slot, uint charSlotIndex)
        {
            // The unit slot stores the character's save ID (char+0x00), not the slot index.
            uint charAddr = Util.calcCharacterAddress(charSlotIndex);
            uint charId   = SaveData.Instance().ReadNumber(charAddr, 4);

            if (unitIndex == 0)
            {
                SaveData.Instance().WriteNumber(Unit01Base + 4 + (uint)(slot * 4), 4, charId);
            }
            else
            {
                uint blockBase = UnitBlockBase + (uint)(unitIndex - 1) * UnitBlockStride;
                SaveData.Instance().WriteNumber(blockBase + UnitSlotOffset + (uint)(slot * 4), 4, charId);
            }
        }

        // ── Character allocation ──────────────────────────────────────────────

        /// <summary>
        /// Returns the 0-based character slot index to write into.
        /// Finds the first slot where ID == 0xFFFFFFFF (empty).
        /// If no empty slot exists, returns the last slot (overwrite behaviour).
        /// Returns uint.MaxValue only if the save reports 0 characters.
        /// </summary>
        private static uint AllocateCharacterSlot(List<string> warnings)
        {
            var sd = SaveData.Instance();
            for (uint ci = 0; ci < MaxCharacters; ci++)
            {
                uint charAddr = Util.calcCharacterAddress(ci);
                uint id = sd.ReadNumber(charAddr, 4);
                if (id == 0xFFFFFFFF)
                    return ci;
            }
            // No free slot — return MaxCharacters-1 to overwrite the last slot.
            warnings.Add("Save is full (500 characters); overwriting last slot.");
            return MaxCharacters - 1;
        }

        // ── Character write ───────────────────────────────────────────────────

        private static void WriteCharacter(
            uint charSlotIndex,
            JsonObject def,
            uint unitIdentifier,
            uint formationSlot,
            int playerNum,
            List<string> warnings,
            List<uint> newIds,
            string ctx)
        {
            var sd = SaveData.Instance();
            uint addr = Util.calcCharacterAddress(charSlotIndex);

            // ── Assign a new unique character ID ──────────────────────────────
            uint newId = sd.ReadNumber(AddrIdCounter, 4) + 1;
            sd.WriteNumber(addr, 4, newId);
            sd.WriteNumber(AddrIdCounter, 4, newId);
            sd.WriteNumber(addr + 16, 4, 1); // char+0x10: must be 1 (observed on all valid chars)
            newIds.Add(newId);

            // Update character count if we're writing into a previously-empty slot.
            uint currentCount = sd.ReadNumber(AddrCharCount, 4);
            if (charSlotIndex >= currentCount)
                sd.WriteNumber(AddrCharCount, 4, charSlotIndex + 1);

            // ── Class ─────────────────────────────────────────────────────────
            uint classId = 5; // default: Soldier
            string? classKey = def["classKey"]?.GetValue<string>();
            if (classKey != null)
            {
                if (ClassKeyToId.TryGetValue(classKey, out var cid))
                    classId = cid;
                else
                    warnings.Add($"{ctx}: unknown classKey '{classKey}', defaulting to Soldier.");
            }
            sd.WriteNumber(addr + 40, 1, classId);

            // ── Gender (inferred from class) ──────────────────────────────────
            uint gender = MaleOnlyClasses.Contains(classId) ? 1u
                        : FemaleOnlyClasses.Contains(classId) ? 2u
                        : (uint)Rng.Next(1, 3); // random 1 or 2 for mixed classes
            sd.WriteNumber(addr + 0x30, 1, gender);

            // ── Colors ────────────────────────────────────────────────────────────
            // Base color is assigned per player (1-indexed, 0-based index):
            //   Player 1 → index 1 (color 1)
            //   Player 2 → index 2 (color 2)
            //   Player 3 → index 3 (color 4)
            //   Player 4 → index 4 (color 3)
            // Color indices are 0-based (0 = Flaxen/first palette entry).
            //
            // Hair, Accent 1, and Accent 2 are written as 0 (inherit base / no override).
            // A full per-class color range map is still needed to support meaningful
            // randomization of hair/accent slots — that work is deferred.
            uint baseColor = playerNum == 3 ? 4 : playerNum == 4 ? 3 : (uint)playerNum;
            sd.WriteNumber(addr + 0x2C, 1, baseColor); // Base color (player-based)
            sd.WriteNumber(addr + 0x2D, 1, 0);               // Hair color  (0 = inherit base)
            sd.WriteNumber(addr + 0x2E, 1, 0);               // Accent color 1
            sd.WriteNumber(addr + 0x2F, 1, 0);               // Accent color 2
            // addr + 0x30 (gender) already written above

            // ── Voice ─────────────────────────────────────────────────────────────
            // Personality index (1–18): 6 types × 3 variants.
            byte randVoice = (byte)Rng.Next(1, 19);
            sd.WriteNumber(addr + 0x32, 1, randVoice);

            // Sample-set ID at +0x33 encodes personality + gender.
            // Formula verified against all observed chars:
            //   type = (voice-1)/3, variant = (voice-1)%3
            //   maleSampleBase = 94 + type*6 + (variant < 2 ? variant : 5)
            //   sampleId = maleSampleBase + (female ? 3 : 0)
            int voiceType      = (randVoice - 1) / 3;
            int voiceVariant   = (randVoice - 1) % 3;
            int maleSampleBase = 94 + voiceType * 6 + (voiceVariant < 2 ? voiceVariant : 5);
            sd.WriteNumber(addr + 0x33, 1, (uint)(maleSampleBase + (gender == 2 ? 3 : 0)));
            
            // ── Generic name index (random) ───────────────────────────────────
            int genderOffset = (gender == 2) ? 70 : 0;
            int culture = Rng.Next(0, 5);
            uint nameIdx = (uint)(culture * 140 + genderOffset + Rng.Next(0, 70));
            sd.WriteNumber(addr + 36, 4, nameIdx);

            // ── Level ─────────────────────────────────────────────────────────
            uint level = 1;
            if (def["level"] != null)
                level = (uint)Math.Max(1, def["level"]!.GetValue<int>());
            sd.WriteNumber(addr + 60, 2, level);

            // ── HP ────────────────────────────────────────────────────────────────
            // +0x3E (addr+62): max HP read by the game for deployment eligibility.
            uint hp = 1;
            if (def["maxHP"] != null)
                hp = (uint)def["maxHP"]!.GetValue<int>();
            sd.WriteNumber(addr + 62, 2, hp);

           uint stamina = classId switch
            {
                39 or 41 or 48 => 4,
                22 or 25 or 27 or 28 or 33 or 35 or 36 or 43 => 5,
                _ => 6,
            };
            uint unk1C0 = classId switch
            {
                6 or 45 or 46 or 47 or 48 => 1,
                43 => 4,
                11 or 22 or 39 or 53 or 58 => 3,
                _ => 2,
            };
            sd.WriteNumber(addr + 0x1C0, 4, unk1C0);   // unknown – must not be 0xFFFFFFFF
            sd.WriteNumber(addr + 0x1C8, 2, stamina);   // Stamina current
            sd.WriteNumber(addr + 0x1CA, 2, stamina);   // Stamina max

            // ── Exp (from level table; 5-step increments, 0 if not in table) ──
            uint exp = LevelToExp.TryGetValue((int)level, out var expVal) ? expVal : 0;
            sd.WriteNumber(addr + 56, 4, exp);

            // ── Growth types ──────────────────────────────────────────────────
            var growthsNode = def["growths"] as JsonArray;
            uint g1 = 1, g2 = 1; // default: Hardy / Hardy
            if (growthsNode != null)
            {
                if (growthsNode.Count > 0)
                    g1 = ParseGrowthType(growthsNode[0]?.GetValue<string>(), warnings, ctx);
                if (growthsNode.Count > 1)
                    g2 = ParseGrowthType(growthsNode[1]?.GetValue<string>(), warnings, ctx);
            }
            sd.WriteNumber(addr + 0x29, 1, g1);
            sd.WriteNumber(addr + 0x2A, 1, g2);

            // ── Stat dews (plus values) ───────────────────────────────────────
            var dewsNode = def["dews"] as JsonObject;
            sd.WriteNumber(addr + 64, 1, ReadDew(dewsNode, "HP",   warnings, ctx));
            sd.WriteNumber(addr + 65, 1, ReadDew(dewsNode, "PATK", warnings, ctx));
            sd.WriteNumber(addr + 66, 1, ReadDew(dewsNode, "PDEF", warnings, ctx));
            sd.WriteNumber(addr + 67, 1, ReadDew(dewsNode, "MATK", warnings, ctx));
            sd.WriteNumber(addr + 68, 1, ReadDew(dewsNode, "MDEF", warnings, ctx));
            sd.WriteNumber(addr + 69, 1, ReadDew(dewsNode, "ACC",  warnings, ctx));
            sd.WriteNumber(addr + 70, 1, ReadDew(dewsNode, "EVA",  warnings, ctx));
            sd.WriteNumber(addr + 71, 1, ReadDew(dewsNode, "CRT",  warnings, ctx));
            sd.WriteNumber(addr + 72, 1, ReadDew(dewsNode, "GRD",  warnings, ctx));
            sd.WriteNumber(addr + 73, 1, ReadDew(dewsNode, "INIT", warnings, ctx));

            // ── Formation assignment ──────────────────────────────────────────
            sd.WriteNumber(addr + 4,  4, unitIdentifier); // char+0x04: unit identifier
            uint mappedSlot = formationSlot < 3 ? formationSlot + 3 : formationSlot - 3;
            sd.WriteNumber(addr + 32, 1, mappedSlot);  // char+0x20: formation slot index

            // ── Status flags ──────────────────────────────────────────────────
            sd.WriteNumber(addr + 460, 1, 0x18); // bit0=in formation, bit3=permanent, bit4=hired generic

            // ── Clear equipment slots ─────────────────────────────────────────
            for (int s = 0; s < 4; s++)
                sd.WriteNumber(addr + 76 + (uint)(s * 4), 4, 0);

            // ── Equipment ────────────────────────────────────────────────────
            var equipNode = def["equipment"] as JsonArray;
            if (equipNode != null)
                WriteEquipment(charSlotIndex, addr, equipNode, warnings, ctx);

            // ── Tactic block (zero entire 16×16 area first) ───────────────────
            for (int i = 0; i < 16; i++)
            {
                uint eBase = addr + 96 + (uint)(i * 16);
                sd.WriteNumber(eBase + 0,  2, 0);
                sd.WriteNumber(eBase + 4,  2, 0);
                sd.WriteNumber(eBase + 8,  4, 0);
                sd.WriteNumber(eBase + 12, 2, 0);
                sd.WriteNumber(eBase + 14, 2, 0);
            }
            // Zero entry-0 conditions store (charAddr+92)
            sd.WriteNumber(addr + 92, 4, 0);

            // ── Skills / tactics ─────────────────────────────────────────────
            var skillsNode = def["skillSlots"] as JsonArray;
            if (skillsNode != null)
                WriteTactics(addr, skillsNode, warnings, ctx);
        }

        // ── Equipment ────────────────────────────────────────────────────────

        private static void WriteEquipment(
            uint charSlotIndex,
            uint charAddr,
            JsonArray equipArray,
            List<string> warnings,
            string ctx)
        {
            var sd = SaveData.Instance();
            int saveSlot = 0;

            foreach (var node in equipArray)
            {
                if (saveSlot > 3)
                {
                    warnings.Add($"{ctx}: more than 4 equipment items, extras skipped.");
                    break;
                }

                var eq = node as JsonObject;
                if (eq == null) { saveSlot++; continue; }

                string? itemKey = eq["itemId"]?.GetValue<string>();
                if (itemKey == null)
                {
                    saveSlot++; // empty slot — advance without writing
                    continue;
                }

                if (!ItemKeyToId.TryGetValue(itemKey, out uint itemTypeId))
                {
                    warnings.Add($"{ctx}: unknown item key '{itemKey}', skipping.");
                    saveSlot++; // still advance so subsequent items land in the right slot
                    continue;
                }

                // Count items currently in the save to find the next free inventory slot.
                uint invCount = 0;
                for (uint i = 0; i < 3800; i++)
                {
                    uint itemAddr = 0xA0 + i * 20;
                    if (sd.ReadNumber(itemAddr, 4) == 0) break;
                    invCount = i + 1;
                }

                if (invCount >= 3800)
                {
                    warnings.Add($"{ctx}: inventory full, cannot add item '{itemKey}'.");
                    saveSlot++;
                    continue;
                }

                uint newItemAddr    = 0xA0 + invCount * 20;
                uint inventoryIndex = invCount + 1; // 1-based

                sd.WriteNumber(newItemAddr + 0,  4, itemTypeId);        // item type ID
                sd.WriteNumber(newItemAddr + 4,  4, inventoryIndex);     // 1-based inventory index
                sd.WriteNumber(newItemAddr + 8,  3, 0);                  // count = 0 (equipment)
                sd.WriteNumber(newItemAddr + 11, 1, (uint)saveSlot);     // Equipment1: which char slot
                sd.WriteNumber(newItemAddr + 12, 1, charSlotIndex);      // Equipment2: char index
                sd.WriteNumber(newItemAddr + 16, 4, 5);                  // Status = 5 (equipped)

                // Write inventory index into character's equipment slot.
                sd.WriteNumber(charAddr + 76 + (uint)(saveSlot * 4), 4, inventoryIndex);

                saveSlot++;
            }
        }

        // ── Tactics ──────────────────────────────────────────────────────────

        private static void WriteTactics(
            uint charAddr,
            JsonArray skillSlots,
            List<string> warnings,
            string ctx)
        {
            var sd = SaveData.Instance();

            // We build the tactic array entries in order.
            // Active skills first (in JSON order), then passive skills.
            // The game expects actives before passives in the array.

            var activeEntries  = new List<(uint sid, int isValid, uint isUnusable, uint c1, uint c2)>();
            var passiveEntries = new List<(uint sid, int isValid, uint isUnusable, uint c1, uint c2)>();

            foreach (var node in skillSlots)
            {
                var slot = node as JsonObject;
                if (slot == null) continue;

                string? skillKey = slot["skillId"]?.GetValue<string>();
                if (skillKey == null) continue;

                if (!SkillKeyToId.TryGetValue(skillKey, out int skillId))
                {
                    warnings.Add($"{ctx}: unknown skillId '{skillKey}', skipping.");
                    continue;
                }

                // Parse conditions from the tactics array (up to 2).
                uint condA = 0, condB = 0;
                var tacticsArr = slot["tactics"] as JsonArray;
                if (tacticsArr != null)
                {
                    for (int ti = 0; ti < tacticsArr.Count && ti < 2; ti++)
                    {
                        var tac = tacticsArr[ti] as JsonObject;
                        string? condKey = tac?["key"]?.GetValue<string>();
                        if (condKey == null) continue;

                        if (!ConditionKeyToId.TryGetValue(condKey, out uint condId))
                        {
                            warnings.Add($"{ctx} skill '{skillKey}': unknown condition '{condKey}', skipping.");
                            continue;
                        }
                        if (ti == 0) condA = condId;
                        else         condB = condId;
                    }
                }

                // Build the tactic entry (always as injected item-skill format).
                uint  sid        = (uint)(skillId - 15);
                int   isValid    = SkillInfo.GetIsValid(skillId);
                uint  isUnusable = 4;

                if (SkillInfo.IsPassive(skillId))
                    passiveEntries.Add((sid, isValid, isUnusable, condA, condB));
                else
                    activeEntries.Add((sid, isValid, isUnusable, condA, condB));
            }

            // Combine: actives first, then passives (max 8 total).
            var allEntries = activeEntries.Concat(passiveEntries).Take(8).ToList();

            if (allEntries.Count == 0) return;

            // Write entries using the one-behind condition rule:
            //   Entry[0]'s conditions → charAddr+92 (packed u32)
            //   Entry[i>0]'s conditions → entry[i-1].CondA / CondB

            // First pass: write skill data for all entries (conditions written in second pass).
            for (int i = 0; i < allEntries.Count; i++)
            {
                var (sid, isValid, isUnusable, _, _) = allEntries[i];
                uint eBase = charAddr + 96 + (uint)(i * 16);
                sd.WriteNumber(eBase + 0, 2, sid);
                sd.WriteNumber(eBase + 4, 2, (uint)(short)isValid);
                sd.WriteNumber(eBase + 8, 4, isUnusable);
            }

            // Second pass: write conditions using one-behind rule.
            for (int i = 0; i < allEntries.Count; i++)
            {
                var (_, _, _, condA, condB) = allEntries[i];
                if (i == 0)
                {
                    uint packed = (condA & 0xFFFF) | ((condB & 0xFFFF) << 16);
                    sd.WriteNumber(charAddr + 92, 4, packed);
                }
                else
                {
                    uint prevBase = charAddr + 96 + (uint)((i - 1) * 16);
                    sd.WriteNumber(prevBase + 12, 2, condA);
                    sd.WriteNumber(prevBase + 14, 2, condB);
                }
            }
        }

        // ── Small helpers ─────────────────────────────────────────────────────

        private static uint ParseGrowthType(string? name, List<string> warnings, string ctx)
        {
            if (name == null) return 1;
            if (GrowthTypeNames.TryGetValue(name, out var v)) return v;
            warnings.Add($"{ctx}: unknown growth type '{name}', defaulting to Hardy.");
            return 1;
        }

        private static uint ReadDew(JsonObject? dews, string key, List<string> warnings, string ctx)
        {
            if (dews == null) return 0;
            var node = dews[key];
            if (node == null) return 0;
            int val = node.GetValue<int>();
            if (val < 0 || val > 5)
            {
                warnings.Add($"{ctx}: dew '{key}' value {val} out of range (0–5), clamping.");
                val = Math.Clamp(val, 0, 5);
            }
            return (uint)val;
        }
    }
}
