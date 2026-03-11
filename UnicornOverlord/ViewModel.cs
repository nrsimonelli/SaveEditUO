using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace UnicornOverlord
{
	internal class ViewModel : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler? PropertyChanged;

		private readonly Info Info = Info.Instance();
		public ICommand OpenFileCommand { get; set; }
		public ICommand SaveFileCommand { get; set; }
		public ICommand SaveAsFileCommand { get; set; }
		public ICommand ChoiceItemCommand { get; set; }
		public ICommand ChoiceEquipmentCommand { get; set; }
		public ICommand ChoiceClassCommand { get; set; }
		public ICommand AppendItemCommand { get; set; }
		public ICommand AppendEquipmentCommand { get; set; }
		public ICommand DeleteEquipmentCommand { get; set; }
		public ICommand ExportCharacterCommand { get; set; }
		public ICommand ImportCharacterCommand { get; set; }
		public ICommand InsertCharacterCommand { get; set; }
		public ICommand ChangeItemCountMaxCommand { get; set; }
		public ICommand ChangeCharacterBondMaxCommand { get; set; }
		public ICommand MorphSlotCommand { get; set; }
		public ICommand DeleteSlotCommand { get; set; }
		public ICommand CreateAndEquipCommand { get; set; }
		public ICommand EditCondition1Command { get; set; }
		public ICommand EditCondition2Command { get; set; }
		public ICommand AddSkillCommand { get; set; }
		public ICommand DeleteTacticEntryCommand { get; set; }

		public Basic Basic { get; set; } = new Basic();
		public ObservableCollection<Character> Characters { get; set; } = new ObservableCollection<Character>();
		public ObservableCollection<Item> Items { get; set; } = new ObservableCollection<Item>();
		public ObservableCollection<Item> Equipments { get; set; } = new ObservableCollection<Item>();
		public ObservableCollection<Unit> Units { get; set; } = new ObservableCollection<Unit>();
		public ObservableCollection<EquippedSlot> EquippedSlots { get; set; } = new ObservableCollection<EquippedSlot>();
		public ObservableCollection<TacticEntry> TacticEntries { get; set; } = new ObservableCollection<TacticEntry>();

		private Character? mSelectedCharacter;
		public Character? SelectedCharacter
		{
			get => mSelectedCharacter;
			set
			{
				mSelectedCharacter = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedCharacter)));
				RefreshEquippedSlots();
				RefreshTacticEntries();
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanAddSkill)));
			}
		}

		public ViewModel()
		{
			OpenFileCommand = new ActionCommand(OpenFile);
			SaveFileCommand = new ActionCommand(SaveFile);
			SaveAsFileCommand = new ActionCommand(SaveAsFile);
			ChoiceItemCommand = new ActionCommand(ChoiceItem);
			ChoiceEquipmentCommand = new ActionCommand(ChoiceEquipment);
			ChoiceClassCommand = new ActionCommand(ChoiceClass);
			AppendItemCommand = new ActionCommand(AppendItem);
			AppendEquipmentCommand = new ActionCommand(AppendEquipment);
			DeleteEquipmentCommand = new ActionCommand(DeleteEquipment);
			ExportCharacterCommand = new ActionCommand(ExportCharacter);
			ImportCharacterCommand = new ActionCommand(ImportCharacter);
			InsertCharacterCommand = new ActionCommand(InsertCharacter);
			ChangeItemCountMaxCommand = new ActionCommand(ChangeItemCountMax);
			ChangeCharacterBondMaxCommand = new ActionCommand(ChangeCharacterBondMax);
			MorphSlotCommand = new ActionCommand(MorphSlot);
			DeleteSlotCommand = new ActionCommand(DeleteSlot);
			CreateAndEquipCommand = new ActionCommand(CreateAndEquip);
			EditCondition1Command = new ActionCommand(EditCondition1);
			EditCondition2Command = new ActionCommand(EditCondition2);
			AddSkillCommand = new ActionCommand(AddSkill);
			DeleteTacticEntryCommand = new ActionCommand(DeleteTacticEntry);
		}

		private void Initialize()
		{
			Characters.Clear();
			Items.Clear();
			Equipments.Clear();
			Units.Clear();

			// create bond
			var bondDictionary = new Dictionary<uint, ObservableCollection<Bond>>();
			for (uint index = 0; index < 164; index++)
			{
				uint baseAddress = Util.calcBondAddress(index);
				uint id = SaveData.Instance().ReadNumber(baseAddress, 4);
				if (id == 0xFFFFFFFF) break;

				var bonds = new ObservableCollection<Bond>();
				bondDictionary.Add(id, bonds);
				for (uint count = 0; count < 164; count++)
				{
					uint address = baseAddress + 4 + count * 8;
					id = SaveData.Instance().ReadNumber(address, 4);
					if (id == 0xFFFFFFFF) break;

					bonds.Add(new Bond(address));
				}
			}

			// create character
			// counter ??
			for (uint i = 0; i < 500; i++)
			{
				var ch = new Character(Util.calcCharacterAddress(i));
				if (ch.ID == 0xFFFFFFFF) break;

				if(bondDictionary.ContainsKey(ch.ID))
				{
					ch.Bonds = bondDictionary[ch.ID];
				}

				Characters.Add(ch);
			}

			// create item
			for (uint i = 0; i < 3800; i++)
			{
				var item = new Item(0xA0 + i * 20);
				if (item.Index == 0) break;

				if(item.Count== 0)
					Equipments.Add(item);
				else
					Items.Add(item);
			}

			// create unit
			for (uint i = 0; i < 10; i++)
			{
				var unit = new Unit(0x10D89A + i * 1720);
				Units.Add(unit);
			}

			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Basic)));
		}

		private void OpenFile(object? parameter)
		{
			var dlg = new OpenFileDialog();
			dlg.Filter = "UCSAVEFILE|UCSAVEFILE*.DAT";
			if (dlg.ShowDialog() == false) return;

			SaveData.Instance().Open(dlg.FileName);
			Initialize();
		}

		private void SaveFile(object? parameter)
		{
			SaveData.Instance().Save();
		}

		private void SaveAsFile(object? parameter)
		{
			var dlg = new SaveFileDialog();
			dlg.Filter = "UCSAVEFILE|UCSAVEFILE*.DAT";
			if (dlg.ShowDialog() == false) return;

			SaveData.Instance().SaveAs(dlg.FileName);
		}

		private void ChoiceItem(object? parameter)
		{
			Item? item = parameter as Item;
			if(item == null) return;

			ChoiceItem(ChoiceWindow.eType.eItem, item);
		}

		private void ChoiceEquipment(object? parameter)
		{
			Item? item = parameter as Item;
			if (item == null) return;

			ChoiceItem(ChoiceWindow.eType.eEquipment, item);
		}

		private void ChoiceItem(ChoiceWindow.eType type, Item item)
		{
			var dlg = new ChoiceWindow();
			dlg.Type = type;
			dlg.ID = item.ID;
			dlg.ShowDialog();
			item.ID = dlg.ID;
			item.Status = 4;
		}


		private static readonly HashSet<uint> MaleOnlyClasses = new()
		{
			1, 2, 3, 4, 7, 8, 13, 14, 15, 16, 19, 20, 23, 24, 25, 26, 29, 30, 33, 34, 45, 47, 51, 52, 60, 61, 62, 65, 66, 67, 68, 69, 71, 72,
		};

		// Classes confirmed female-only from save data analysis.
		private static readonly HashSet<uint> FemaleOnlyClasses = new()
		{
      21, 22, 27, 28, 31, 32, 35, 36, 37, 38, 39, 40, 41, 42, 46, 48, 49, 50, 53, 54, 55, 56, 57, 58, 59, 63, 64, 70, 73,
		};

		private void ChoiceClass(object? parameter)
		{
			Character? ch = parameter as Character;
			if (ch == null) return;

			var dlg = new ChoiceWindow();
			dlg.Type = ChoiceWindow.eType.eClass;
			dlg.ID = ch.Class;
			dlg.ShowDialog();
			ch.Class = dlg.ID;

			// Auto-update gender if the new class is gender-locked
			if (MaleOnlyClasses.Contains(dlg.ID))
				ch.Gender = 1;
			else if (FemaleOnlyClasses.Contains(dlg.ID))
				ch.Gender = 2;
		}

		private void AppendItem(object? parameter)
		{
			var item = AppendItem(ChoiceWindow.eType.eItem);
			if (item == null) return;

			item.Count = 1;
			Items.Add(item);
		}

		private void AppendEquipment(object? parameter)
		{
			var item = AppendItem(ChoiceWindow.eType.eEquipment);
			if (item == null) return;

			Equipments.Add(item);
		}

		private void DeleteEquipment(object? parameter)
		{
			Item? item = parameter as Item;
			if (item == null) return;

			// item.Index is the 1-based inventory position — use it to find exact save slot
			uint deletedItemIndex = item.Index;
			int saveSlot = (int)(deletedItemIndex - 1); // 0-based position in save array
			int totalSlots = Items.Count + Equipments.Count;

			// Shift all slots after the deleted one forward by one
			for (int i = saveSlot; i < totalSlots - 1; i++)
			{
				uint srcAddr = (uint)(0xA0 + (i + 1) * 20);
				uint dstAddr = (uint)(0xA0 + i * 20);

				// Read the original Index from the source BEFORE copying
				uint srcIndex = SaveData.Instance().ReadNumber(srcAddr + 4, 4);

				// Copy the whole slot
				var buffer = SaveData.Instance().ReadValue(srcAddr, 20);
				SaveData.Instance().WriteValue(dstAddr, buffer);

				// Write the decremented Index into the destination
				SaveData.Instance().WriteNumber(dstAddr + 4, 4, srcIndex - 1);
			}

			// Zero out the last slot (now vacated)
			uint lastAddr = (uint)(0xA0 + (totalSlots - 1) * 20);
			SaveData.Instance().WriteValue(lastAddr, new byte[20]);

			// Update character equipment slot references
			for (uint charIdx = 0; charIdx < 500; charIdx++)
			{
				uint charAddr = Util.calcCharacterAddress(charIdx);
				uint charId = SaveData.Instance().ReadNumber(charAddr, 4);
				if (charId == 0xFFFFFFFF) break;

				for (uint slot = 0; slot < 4; slot++)
				{
					uint slotAddr = charAddr + 76 + slot * 4;
					uint slotVal = SaveData.Instance().ReadNumber(slotAddr, 4);

					if (slotVal == deletedItemIndex)
					{
						// This character had the deleted item equipped — clear the slot
						SaveData.Instance().WriteNumber(slotAddr, 4, 0);
					}
					else if (slotVal > deletedItemIndex && slotVal != 0xFFFFFFFF)
					{
						// Shift reference down by 1 to match the compacted item list
						SaveData.Instance().WriteNumber(slotAddr, 4, slotVal - 1);
					}
				}
			}

			// Rebuild both collections since item positions all changed
			Items.Clear();
			Equipments.Clear();
			for (uint i = 0; i < 3800; i++)
			{
				var slot = new Item(0xA0 + i * 20);
				if (slot.Index == 0) break;
				if (slot.Count == 0)
					Equipments.Add(slot);
				else
					Items.Add(slot);
			}
		}

		private Item? AppendItem(ChoiceWindow.eType type)
		{
			uint index = (uint)(Items.Count + Equipments.Count);
			if (index >= 3800) return null;

			var dlg = new ChoiceWindow();
			dlg.Type = type;
			dlg.ShowDialog();
			if (dlg.ID == 0) return null;

			var item = new Item(0xA0 + index * 20);
			item.ID = dlg.ID;
			item.Index = index + 1;
			item.Status = 4;
			item.Equipment1 = 0xFF;
			item.Equipment2 = 0xFF;

			return item;
		}

		private void ExportCharacter(object? parameter)
		{
			if (parameter == null) return;

			int index = Convert.ToInt32(parameter);
			if (index == -1) return;

			var dlg = new SaveFileDialog();
			dlg.Filter = "Unicorn Overlord Character's Dump|*.uocd";
			if (dlg.ShowDialog() == false) return;

			uint address = Util.calcCharacterAddress((uint)index);
			Byte[] buffer = SaveData.Instance().ReadValue(address, 464);

			System.IO.File.WriteAllBytes(dlg.FileName, buffer);
		}

		private void ImportCharacter(object? parameter)
		{
			if (parameter == null) return;

			int index = Convert.ToInt32(parameter);
			if (index == -1) return;

			var dlg = new OpenFileDialog();
			dlg.Filter = "Unicorn Overlord Character's Dump|*.uocd";
			if (dlg.ShowDialog() == false) return;

			Byte[] buffer = System.IO.File.ReadAllBytes(dlg.FileName);
			if (buffer.Length != 464) return;
			buffer = ProcessingCharacter(buffer);

			uint address = Util.calcCharacterAddress((uint)index);

			// use original id
			uint id = SaveData.Instance().ReadNumber(address, 4);
			Array.Copy(BitConverter.GetBytes(id), buffer, 4);

			SaveData.Instance().WriteValue(address, buffer);

			// swap
			Characters.RemoveAt(index);
			Characters.Insert(index, new Character(address));
		}

		private void InsertCharacter(object? parameter)
		{
			uint count = (uint)Characters.Count;
			if (count >= 500) return;

			var dlg = new OpenFileDialog();
			dlg.Multiselect = true;
			dlg.Filter = "Unicorn Overlord Character's Dump|*.uocd";
			if (dlg.ShowDialog() == false) return;

			foreach (String filename in dlg.FileNames)
			{
				count = (uint)Characters.Count;
				if (count >= 500) break;

				Byte[] buffer = System.IO.File.ReadAllBytes(filename);
				if (buffer.Length != 464) continue;

				buffer = ProcessingCharacter(buffer);
				uint id = SaveData.Instance().ReadNumber(0x63980, 4) + 1;
				Array.Copy(BitConverter.GetBytes(id), buffer, 4);
				uint address = Util.calcCharacterAddress(count);
				SaveData.Instance().WriteValue(address, buffer);

				SaveData.Instance().WriteNumber(0x63980, 4, id);
				count = SaveData.Instance().ReadNumber(0x63984, 4);
				SaveData.Instance().WriteNumber(0x63984, 4, count + 1);

				InsertFriendship(id);

				var ch = new Character(Util.calcCharacterAddress((uint)Characters.Count));
				if (ch.ID == 0xFFFFFFFF) continue;
				Characters.Add(ch);
			}
		}

		private void ChangeItemCountMax(object? parameter)
		{
			foreach(var item in Items)
			{
				if (item.ID <= 4) continue;
				item.Count = 99;
			}
		}

		private void ChangeCharacterBondMax(object? parameter)
		{
			Character? ch = parameter as Character;
			if (ch == null) return;
			if (ch.Bonds == null) return;

			foreach (var bond in ch.Bonds)
			{
				bond.Value = 1000;
			}
		}

		private static readonly HashSet<uint> BaseClassIds = new HashSet<uint>
		{
			1, 3, 5, 7, 9, 11, 13, 15, 17, 19, 21, 23, 25, 27, 29, 31, 33, 35, 37, 39, 41, 53, 55
		};

		private void RefreshEquippedSlots()
		{
			EquippedSlots.Clear();
			if (mSelectedCharacter == null) return;

			bool isPromoted = !BaseClassIds.Contains(mSelectedCharacter.Class);

			// Build a lookup from item Index -> item name
			var indexToName = new Dictionary<uint, string>();
			foreach (var eq in Equipments)
			{
				var nameInfo = Info.Search(Info.Item, eq.ID);
				indexToName[eq.Index] = nameInfo?.Name ?? eq.ID.ToString();
			}

			for (int slot = 0; slot < 4; slot++)
			{
				uint itemIndex = mSelectedCharacter.GetEquipmentSlot(slot);
				var equippedSlot = new EquippedSlot(slot);

				if (slot == 3 && !isPromoted)
				{
					equippedSlot.IsLocked = true;
					equippedSlot.ItemName = "(promote to unlock)";
				}
				else if (itemIndex != 0 && indexToName.TryGetValue(itemIndex, out var name))
				{
					equippedSlot.ItemIndex = itemIndex;
					equippedSlot.ItemName = name;
				}

				EquippedSlots.Add(equippedSlot);
			}
		}

		// ── Tactic display ──────────────────────────────────────────────────────
		//
		// Confirmed entry layout (16 bytes, from 010 Editor Save_CharData.bt template):
		//   +0x00  u16  SkillID    — 0=Standard Attack; 1-8=class slot; ≥9=absolute skill ID
		//   +0x02  u8   Unk1
		//   +0x03  u8   Unk2
		//   +0x04  s16  isValid    — (-1) = active entry
		//   +0x06  s16  Unk_0x6
		//   +0x08  u32  isUnusable — 0=available; non-zero=locked/PP cost
		//   +0x0C  u16  CondA      — primary condition (index into FactorNames, base 4992)
		//   +0x0E  u16  CondB      — secondary condition (same base); 0 = none

		/// <summary>Resolve a condition index to a display string.</summary>
		private static string LookupCondition(uint val) => FactorNames.Lookup(val);

		/// <summary>Resolve an absolute skill ID to a display name via the skill list.</summary>
		private string LookupSkill(uint skillId)
		{
			if (skillId == 0 || skillId >= 0xFFFF) return string.Empty;
			var s = Info.Search(Info.Skill, skillId);
			return s != null ? s.Name : $"Skill#{skillId}";
		}

		/// <summary>
		/// Per-class skill slot assignments.
		/// Key:   class ID (matches Character.ClassID / save byte at charAddr+40)
		/// Value: Dictionary mapping tactic SkillID (1–9) → absolute skill ID in the skill list.
		///
		/// The game stores class-relative slot numbers (1–9) rather than absolute IDs for
		/// skills that come with a class. Slot numbers are the same across all characters of
		/// the same class. This table resolves them to named skills for display purposes.
		///
		/// Slot semantics (consistent across all classes):
		///   Slot 3 = primary offensive skill
		///   Slot 4 = secondary offensive skill
		///   Slot 5 = tertiary offensive skill (elite classes only)
		///   Slot 7 = passive / guard skill
		///   Slot 8 = support / special skill
		///   Slot 9 = unique elite skill (advanced classes only)
		///
		/// NOTE: Knight (cls=25) is confirmed from save + in-game screenshot.
		/// All other entries are derived from game knowledge and may contain errors.
		/// Unknown/unmapped slots fall back to "[Class Skill N]" display.
		/// </summary>
		//   slot3 = 1st active skill (lowest learn level)
		//   slot4 = 2nd active skill
		//   slot5 = 3rd active skill
		//   slot7 = 1st passive skill (lowest learn level)
		//   slot8 = 2nd passive skill
		//   slot9 = 3rd passive skill
		// Promoted classes inherit their base class skills and merge them by level.
		private static readonly Dictionary<int, Dictionary<int, int>> ClassSkillSlots = new()
		{
			[1]  = new() { [3]=43,  [4]=44,  [7]=467, [8]=439 },
			// cls= 2 High Lord (base=Lord + Spinning Edge, Rapid Order)
			[2]  = new() { [3]=43,  [4]=44,  [5]=45,  [7]=467, [8]=439, [9]=274 },
			// cls= 3 Fighter
			[3]  = new() { [3]=47,  [4]=48,  [7]=437, [8]=461 },
			// cls= 4 Vanguard       (base=Fighter + Defender lv20, Provoke lv25p)
			[4]  = new() { [3]=47,  [4]=48,  [5]=153, [7]=437, [8]=461, [9]=360 },
			// cls= 5 Soldier
			[5]  = new() { [3]=78,  [4]=79,  [7]=312, [8]=416 },
			// cls= 6 Sergeant       (base=Soldier + Honed Spear lv20, Active Gift lv25p)
			[6]  = new() { [3]=78,  [4]=79,  [5]=81,  [7]=312, [8]=416, [9]=343 },
			// cls= 7 Housecarl
			[7]  = new() { [3]=106, [4]=107, [7]=306, [8]=353 },
			// cls= 8 Viking         (base=Housecarl + Wide Breaker lv20, War Horn lv25p)
			[8]  = new() { [3]=106, [4]=107, [5]=108, [7]=306, [8]=353, [9]=276 },
			// cls= 9 Swordfighter
			[9]  = new() { [3]=50,  [4]=51,  [7]=297, [8]=482 },
			// cls=10 Swordmaster    (base=Swordfighter + Meteor Slash lv20, Charged Impetus lv25p)
			[10] = new() { [3]=50,  [4]=51,  [5]=56,  [7]=297, [8]=482, [9]=317 },
			// cls=11 Sellsword
			[11] = new() { [3]=59,  [4]=60,  [7]=351 },
			// cls=12 Landsknecht    (base=Sellsword + Bastard's Cross lv20, Vengeful Guard lv25p, Bull Force lv30p)
			[12] = new() { [3]=59,  [4]=60,  [5]=61,  [7]=351, [8]=469, [9]=318 },
			// cls=13 Hoplite
			[13] = new() { [3]=85,  [7]=433, [8]=457 },
			// cls=14 Legionnaire    (base=Hoplite + Row Protection lv25, Greatshield lv30, Row Cover lv20p)
			[14] = new() { [3]=85,  [4]=249, [5]=251, [7]=433, [8]=457, [9]=436 },
			// cls=15 Gladiator
			[15] = new() { [3]=113, [7]=345, [8]=360 },
			// cls=16 Berserker      (base=Gladiator + Mounting Charge lv20, Grand Smash lv30, Berserk lv25p)
			[16] = new() { [3]=113, [4]=254, [5]=114, [7]=345, [8]=360, [9]=346 },
			// cls=17 Warrior
			[17] = new() { [3]=93,  [4]=94,  [7]=339 },
			// cls=18 Breaker        (base=Warrior + Row Smash lv30, Enrage lv20p, Heavy Counter lv25p)
			[18] = new() { [3]=153, [4]=94,  [5]=95,  [7]=339, [8]=355, [9]=350 },
			// cls=19 Hunter
			[19] = new() { [3]=169, [4]=170, [7]=334, [8]=391 },
			// cls=20 Sniper         (base=Hunter + Row Shot lv20, Aerial Snipe lv25p)
			[20] = new() { [3]=169, [4]=170, [5]=171, [7]=334, [8]=391, [9]=352 },
			// cls=21 Arbalist
			[21] = new() { [3]=186, [4]=187, [7]=313, [8]=326 },
			// cls=22 Shield Shooter (base=Arbalist + Heavy Bolt lv20, Aid Cover lv25p)
			[22] = new() { [3]=186, [4]=187, [5]=188, [7]=313, [8]=326, [9]=440 },
			// cls=23 Thief
			[23] = new() { [3]=52,  [4]=54,  [7]=475},
			// cls=24 Rogue          (base=Thief + Shadowbite lv20, Active Steal lv30, Sneaking Edge lv25p)
			[24] = new() { [3]=52,  [4]=54,  [5]=55,  [6]=53,  [7]=475, [8]=300 },
			// cls=25 Knight
			[25] = new() { [3]=82,  [4]=83,  [7]=461, [8]=331 },
			// cls=26 Great Knight   (base=Knight + Pile Thrust lv20, Knight's Pursuit lv25p)
			[26] = new() { [3]=82,  [4]=83,  [5]=84,  [7]=461, [8]=331, [9]=393 },
			// cls=27 Radiant Knight
			[27] = new() { [3]=69,  [4]=213, [7]=428, [8]=470 },
			// cls=28 Sainted Knight (base=Radiant Knight + Saint's Blade lv25, Row Barrier lv20p)
			[28] = new() { [3]=69,  [4]=213, [5]=70,  [7]=428, [8]=470, [9]=429 },
			// cls=29 Dark Knight
			[29] = new() { [3]=109, [4]=110, [7]=454, [8]=340 },
			// cls=30 Doom Knight    (base=Dark Knight + Dark Flame lv20, Demonic Pact lv25p)
			[30] = new() { [3]=109, [4]=110, [5]=111, [7]=454, [8]=340, [9]=319 },
			// cls=31 Cleric
			[31] = new() { [3]=211, [7]=370, [8]=383, [9]=452 },
			// cls=32 Bishop         (base=Cleric + Sacred Heal lv25, Parting Resurrection lv20p)
			[32] = new() { [3]=211, [4]=214, [7]=370, [8]=383, [9]=452, [10]=316 },
			// cls=33 Wizard
			[33] = new() { [3]=191, [7]=357, [8]=392 },
			// cls=34 Warlock        (base=Wizard + Thunderous Strike lv20, Volcano lv30, Concentrate lv25p)
			[34] = new() { [3]=191, [4]=197, [5]=198, [7]=357, [8]=392, [9]=385 },
			// cls=35 Witch
			[35] = new() { [3]=192, [4]=193, [7]=445, [8]=414 },
			// cls=36 Sorceress      (base=Witch + Ice Coffin lv30, Quick Cast lv25p)
			[36] = new() { [3]=192, [4]=193, [5]=194, [7]=445, [8]=414, [9]=272 },
			// cls=37 Shaman
			[37] = new() { [3]=235, [4]=238, [7]=408 },
			// cls=38 Druid          (base=Shaman + Defensive Curse lv20, Compounding Curse lv30, Cursed Swamp lv25p)
			[38] = new() { [3]=235, [4]=238, [5]=239, [6]=240, [7]=408, [8]=282 },
			// cls=39 Wyvern Knight
			[39] = new() { [3]=89,  [7]=349, [8]=481 },
			// cls=40 Wyvern Master  (base=Wyvern Knight + Fire Breath lv20, Tempest Dive lv30, Dragon's Roar lv25p)
			[40] = new() { [3]=89,  [4]=90,  [5]=91,  [7]=349, [8]=481, [9]=280 },
			// cls=41 Gryphon Knight
			[41] = new() { [3]=97,  [7]=315, [8]=332 },
			// cls=42 Gryphon Master (base=Gryphon Knight + Fatal Dive lv20, Aerial Smite lv30, Gryphon Glide lv25p)
			[42] = new() { [3]=97,  [4]=99,  [5]=98,  [7]=315, [8]=332, [9]=480 },
			// cls=43 Elven Fencer
			[43] = new() { [3]=57,  [4]=255, [5]=58,  [7]=421, [8]=388, [9]=460 },
			// cls=44 Elven Archer
			[44] = new() { [3]=181, [4]=257, [5]=182, [7]=384, [8]=373, [9]=292 },
			// cls=45 Werewolf
			[45] = new() { [3]=62,  [4]=63,  [5]=64,  [7]=299, [8]=330, [9]=400 },
			// cls=46 Werefox
			[46] = new() { [3]=86,  [4]=87,  [5]=88,  [7]=478, [8]=401, [9]=406 },
			// cls=47 Werebear
			[47] = new() { [3]=103, [4]=104, [5]=105, [7]=462, [8]=327, [9]=309 },
			// cls=48 Wereowl
			[48] = new() { [3]=222, [4]=231, [5]=218, [7]=342, [8]=386, [9]=425 },
			// cls=49 Feathersword
			[49] = new() { [3]=65,  [4]=66,  [5]=67,  [7]=347, [8]=471, [9]=338 },
			// cls=50 Featherbow
			[50] = new() { [3]=183, [4]=184, [5]=185, [7]=394, [8]=325, [9]=410 },
			// cls=51 Featherstaff
			[51] = new() { [3]=219, [4]=226, [5]=227, [7]=295, [8]=411, [9]=412 },
			// cls=52 Feathershield
			[52] = new() { [3]=68, [4]=250, [5]=252, [7]=407, [8]=443, [9]=390 },
			// cls=53 Priestess
			[53] = new() { [3]=199, [4]=224, [7]=374 },
			// cls=54 High Priestess (base=Priestess + Innocent Ray lv30, Saint's Barrier lv25p, Divine Blessing lv35p)
			[54] = new() { [3]=199, [4]=224, [5]=200, [7]=374, [8]=427, [9]=294 },
			// cls=55 Crusader
			[55] = new() { [3]=73,  [4]=74,  [7]=356 },
			// cls=56 Valkyria       (base=Crusader + Brandish lv30, Iron Veil lv25p, Undying Will lv35p)
			[56] = new() { [3]=73,  [4]=74,  [5]=75,  [7]=356, [8]=291, [9]=333 },
			// cls=57 Elven Sibyl
			[57] = new() { [3]=123, [4]=221, [5]=126, [7]=371, [8]=296, [9]=320 },
			// cls=58 Elven Augur
			[58] = new() { [3]=123, [4]=256, [5]=126, [7]=371, [8]=304, [9]=320 },
			// cls=59 Snow Ranger
			[59] = new() { [3]=175, [4]=176, [5]=174, [7]=334, [8]=361, [9]=398 },
			// cls=60 Werelion
			[60] = new() { [3]=113, [4]=254, [5]=114, [7]=345, [8]=360, [9]=346 },
			// cls=61 Paladin
			[61] = new() { [3]=71,  [4]=211, [5]=72,  [7]=426, [8]=470, [9]=391 },
			// cls=62 Prince
			[62] = new() { [3]=127, [4]=234, [5]=233, [7]=274, [8]=444, [9]=415 },
			// cls=63 Dreadnought
			[63] = new() { [3]=59,  [4]=147, [5]=258, [7]=462, [8]=405, [9]=335 },
			// ── Dark Marquess variants (cls=70–73) ───────────────────────────────────
			// cls=69 Dark Marquess (Sword)
			[69] = new() { [3]=76,  [4]=102, [5]=77,  [7]=441, [8]=359, [9]=310 },
			// cls=70 Dark Marquess (Axe)
			[70] = new() { [3]=100, [4]=102, [5]=101, [7]=399, [8]=283, [9]=453 },
			// cls=71 Dark Marquess (Lance)
			[71] = new() { [3]=121, [4]=122, [5]=253, [7]=279, [8]=442, [9]=479 },
			[72] = new() { [3]=121, [4]=122, [5]=253, [7]=279, [8]=442, [9]=479 },
			// cls=73 Dark Marquess (Staff)
			[73] = new() { [3]=235, [4]=195, [5]=196, [7]=450, [8]=409, [9]=284 },
		};

		/// <summary>
		/// Resolve a class-relative skill slot to a skill name.
		/// Returns the skill name if the class/slot mapping is known, otherwise a "[Class Skill N]" placeholder.
		/// </summary>
		private string ResolveClassSlot(int classId, int slotNum)
		{
			if (ClassSkillSlots.TryGetValue(classId, out var slots) &&
				slots.TryGetValue(slotNum, out var skillId))
			{
				var s = Info.Search(Info.Skill, (uint)skillId);
				return s != null ? s.Name : $"Skill#{skillId}";
			}
			return $"[Class Skill {slotNum}]";
		}

		private void RefreshTacticEntries()
		{
			TacticEntries.Clear();
			if (mSelectedCharacter == null) return;

			int classId = (int)mSelectedCharacter.Class;

			// ── Condition storage — universal one-behind rule ────────────────
			//
			// Every tactic entry's player-set conditions are stored in the entry
			// IMMEDIATELY BEFORE IT in the tactic array:
			//
			//   Conditions for entry[0]   → charAddr+92  [lo u16 = cond1, hi u16 = cond2]
			//   Conditions for entry[i>0] → entry[i-1].CondA / entry[i-1].CondB
			//
			// This is universal — it applies to both class skill entries and item skill
			// entries (unusable==4) with no exceptions.
			//
			// Consequence: each entry's own CondA/CondB fields contain the conditions
			// for the NEXT entry, not for itself. We therefore carry a "pending" pair
			// forward as we walk the array.
			//
			// ── Item skill IDs ───────────────────────────────────────────────
			//
			// Item skill entries (isUnusable == 4) store their skill ID as
			// (actual_skill.txt_id − 15).  Add 15 before lookup.
			// Class-relative slot IDs (1–9) and empty entries (sid==0) are unchanged.
			//
			// ── Active vs passive ────────────────────────────────────────────
			// Class entries:   slot 3/4/5 = active (red badge), 7/8/9 = passive (blue)
			// Item entries:    isValid==0 = active item skill, isValid==2 = passive item skill

			// Seed "previous" conditions from charAddr+92 (the slot before entry[0])
			var (seed92a, seed92b) = mSelectedCharacter.GetFirstTacticConditions();
			uint pendingCondA = seed92a;
			uint pendingCondB = seed92b;

			for (int i = 0; i < 16; i++)
			{
				var (skillId, isValid, isUnusable, condA, condB) = mSelectedCharacter.GetTacticRaw(i);

				// Conditions destined for THIS entry were stored by the previous iteration
				uint myCondA = pendingCondA;
				uint myCondB = pendingCondB;

				// This entry's own condA/condB are conditions for the NEXT entry
				pendingCondA = condA;
				pendingCondB = condB;

				// ── Item skill entry (isUnusable == 4) ──────────────────────
				if (isUnusable == 4)
				{
					if (skillId == 0) continue;
					bool isActiveItem = (isValid == 0);
					TacticEntries.Add(new TacticEntry
					{
						SkillID       = (ushort)skillId,
						IsValid       = (short)isValid,
						IsUnusable    = isUnusable,
						CondA         = (ushort)myCondA,
						CondB         = (ushort)myCondB,
						Action        = LookupSkill(skillId + 15),
						Condition1    = LookupCondition(myCondA),
						Condition2    = LookupCondition(myCondB),
						IsActiveSkill = isActiveItem,
					ArrayIndex    = i,
					});
					continue;
				}

				// ── Class skill entry ────────────────────────────────────────
				if (isValid != -1) continue; // skip inactive / empty entries
				if (skillId == 0) continue;  // skip bare Standard Attack rows

				// Active slots are 3–6 (passive slots are 7–9).
				// Rather than hardcoding a range, we ask the slot dict: if the slot
				// number is < 7 it's an active skill, >= 7 it's passive.
				bool isActive = skillId < 7;

				string action = skillId <= 9
					? ResolveClassSlot(classId, (int)skillId)
					: LookupSkill(skillId + 15);

				TacticEntries.Add(new TacticEntry
				{
					SkillID       = (ushort)skillId,
					IsValid       = (short)isValid,
					IsUnusable    = isUnusable,
					CondA         = (ushort)myCondA,
					CondB         = (ushort)myCondB,
					Action        = action,
					Condition1    = LookupCondition(myCondA),
					Condition2    = LookupCondition(myCondB),
					IsActiveSkill = isActive,
					ArrayIndex    = i,
				});
			}
		}

		private void EditCondition1(object? parameter)
		{
			if (parameter is not TacticEntry entry) return;
			if (mSelectedCharacter == null) return;

			var dlg = new ConditionWindow
			{
				Owner          = Application.Current.MainWindow,
				ConditionValue = entry.CondA,
			};
			if (dlg.ShowDialog() != true) return;

			uint newA = dlg.ConditionValue;
			mSelectedCharacter.SetTacticConditions(entry.ArrayIndex, newA, entry.CondB);
			RefreshTacticEntries();
		}

		private void EditCondition2(object? parameter)
		{
			if (parameter is not TacticEntry entry) return;
			if (mSelectedCharacter == null) return;

			var dlg = new ConditionWindow
			{
				Owner          = Application.Current.MainWindow,
				ConditionValue = entry.CondB,
			};
			if (dlg.ShowDialog() != true) return;

			uint newB = dlg.ConditionValue;
			mSelectedCharacter.SetTacticConditions(entry.ArrayIndex, entry.CondA, newB);
			RefreshTacticEntries();
		}

		/// <summary>
		/// True when the Add Skill button should be enabled.
		/// Disabled when no character is selected or all 8 tactic slots are occupied.
		/// </summary>
		public bool CanAddSkill =>
			mSelectedCharacter != null &&
			TacticEntries.Count < Character.MaxTacticEntries;

		private void AddSkill(object? parameter)
		{
			if (mSelectedCharacter == null) return;
			if (TacticEntries.Count >= Character.MaxTacticEntries) return;

			var dlg = new SkillWindow { Owner = Application.Current.MainWindow };
			if (dlg.ShowDialog() != true) return;

			int skillId = dlg.SkillId;
			if (skillId <= 0) return;

			int classId = (int)mSelectedCharacter.Class;
			bool isClassSkill = false;
			int  classSlotId  = 0;

			// Check if this skill belongs to the character's class slot table
			if (ClassSkillSlots.TryGetValue(classId, out var slots))
			{
				foreach (var kv in slots)
				{
					if (kv.Value == skillId)
					{
						isClassSkill = true;
						classSlotId  = kv.Key;
						break;
					}
				}
			}

			// Determine active vs passive for insertion point
			bool isPassive = isClassSkill
				? classSlotId >= 7
				: SkillInfo.IsPassive(skillId);

			// Find insertion index:
			//   Active  → after the last active entry (or 0 if none)
			//   Passive → after the last passive entry (or after last active if no passives yet)
			int insertAt = FindInsertIndex(isPassive);

			mSelectedCharacter.InsertTacticEntry(insertAt, skillId, isClassSkill, classSlotId);
			RefreshTacticEntries();

			// Notify button state
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanAddSkill)));
		}

		/// <summary>
		/// Returns the raw array index at which to insert a new entry.
		/// For actives: one past the last active entry in the display list.
		/// For passives: one past the last passive entry, or one past the last active if no passives exist.
		/// </summary>
		private int FindInsertIndex(bool isPassive)
		{
			int lastActive  = -1;
			int lastPassive = -1;

			foreach (var entry in TacticEntries)
			{
				if (entry.IsActiveSkill)
					lastActive  = entry.ArrayIndex;
				else
					lastPassive = entry.ArrayIndex;
			}

			if (!isPassive)
			{
				// Insert after last active; if no actives yet, insert at position 0
				return lastActive >= 0 ? lastActive + 1 : 0;
			}
			else
			{
				// Insert after last passive; fall back to after last active; fall back to 0
				if (lastPassive >= 0) return lastPassive + 1;
				if (lastActive  >= 0) return lastActive  + 1;
				return 0;
			}
		}

		private void DeleteTacticEntry(object? parameter)
		{
			if (parameter is not TacticEntry entry) return;
			if (mSelectedCharacter == null) return;

			mSelectedCharacter.DeleteTacticEntry(entry.ArrayIndex);
			RefreshTacticEntries();

			// Notify button state
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanAddSkill)));
		}

		private void MorphSlot(object? parameter)
		{
			EquippedSlot? slot = parameter as EquippedSlot;
			if (slot == null || slot.IsEmpty || slot.IsLocked) return;

			var item = Equipments.FirstOrDefault(e => e.Index == slot.ItemIndex);
			if (item == null) return;

			var dlg = new ChoiceWindow();
			dlg.Type = ChoiceWindow.eType.eEquipment;
			dlg.ID = item.ID;
			dlg.ShowDialog();
			if (dlg.ID == 0) return;

			item.ID = dlg.ID;
			item.Status = 4;

			RefreshEquippedSlots();
		}

		private void DeleteSlot(object? parameter)
		{
			EquippedSlot? slot = parameter as EquippedSlot;
			if (slot == null || slot.IsEmpty || slot.IsLocked) return;

			var item = Equipments.FirstOrDefault(e => e.Index == slot.ItemIndex);
			if (item == null) return;

			DeleteEquipment(item);
			RefreshEquippedSlots();
		}

		private void CreateAndEquip(object? parameter)
		{
			EquippedSlot? slot = parameter as EquippedSlot;
			if (slot == null || slot.IsLocked || !slot.IsEmpty || mSelectedCharacter == null) return;

			int charIdx = Characters.IndexOf(mSelectedCharacter);
			if (charIdx < 0) return;

			var item = AppendItem(ChoiceWindow.eType.eEquipment);
			if (item == null) return;

			item.Equipment1 = (uint)slot.SlotNumber;
			item.Equipment2 = (uint)charIdx;

			uint charAddr = Util.calcCharacterAddress((uint)charIdx);
			uint slotAddr = charAddr + 76 + (uint)(slot.SlotNumber * 4);
			SaveData.Instance().WriteNumber(slotAddr, 4, item.Index);

			Equipments.Add(item);
			RefreshEquippedSlots();
		}

		private Byte[] ProcessingCharacter(Byte[] buffer)
		{
			// formation clear
			Array.Copy(BitConverter.GetBytes(0xFFFFFFFF), 0, buffer, 4, 4);
			buffer[32] = 0xFF;

			// buffer[460]
			// character's status
			// 1Bit => formation join
			// 3Bit => join
			// 4Bit => mercenary?
			// 5Bit => use
			buffer[460] &= 0xFE;

			// equipment clear
			// elements => 4Byte
			// count => 4
			// (or Append Item)
			Array.Clear(buffer, 76, 16);

			// update uint?
			/*
			buffer[456] = 9;
			buffer[458] = 9;
			*/
			return buffer;
		}

		private void InsertFriendship(uint id)
		{
			for (uint index = 0; index < 164; index++)
			{
				uint baseAddress = Util.calcBondAddress(index);
				var current_id = SaveData.Instance().ReadNumber(baseAddress, 4);

				// chack blank character
				if(current_id == 0xFFFFFFFF)
				{
					// insert new character
					SaveData.Instance().WriteNumber(baseAddress, 4, id);
					for (uint count = 0; count < Characters.Count; count++)
					{
						uint address = baseAddress + 4 + count * 8;
						// insert existing character
						SaveData.Instance().WriteNumber(address, 4, Characters[(int)count].ID);
					}
					return;
				}

				// existing character
				for (uint count = 0; count < 164; count++)
				{
					uint address = baseAddress + 4 + count * 8;
					if (SaveData.Instance().ReadNumber(address, 4) == 0xFFFFFFFF)
					{
						// insert new character
						SaveData.Instance().WriteNumber(address, 4, id);
						break;
					}
				}
			}
		}
	}
}
