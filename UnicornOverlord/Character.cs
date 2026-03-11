using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.SymbolStore;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnicornOverlord
{
	internal class Character : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler? PropertyChanged;

		public ObservableCollection<Bond>? Bonds {  get; set; }

		private readonly uint mAddress;

		public Character(uint address)
		{
			mAddress = address;
		}

		public uint ID
		{
			get => SaveData.Instance().ReadNumber(mAddress, 4);
		}

		public uint Class
		{
			get => SaveData.Instance().ReadNumber(mAddress + 40, 1);
			set
			{
				SaveData.Instance().WriteNumber(mAddress + 40, 1, value);
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Class)));
			}
		}

		public uint Name
		{
			get => SaveData.Instance().ReadNumber(mAddress + 52, 2);
		}

		public uint GenericNameIndex
		{
			get => SaveData.Instance().ReadNumber(mAddress + 36, 2);
		}

		// Returns the item Index value stored in a given equipment slot (0-3)
		// 0 means empty
		public uint GetEquipmentSlot(int slot)
		{
			return SaveData.Instance().ReadNumber(mAddress + 76 + (uint)(slot * 4), 4);
		}

		// Returns raw tactic field values for entry at entryIndex (0-15).
		//
		// Confirmed layout (16 bytes) from 010 Editor Save_CharData.bt template:
		//   +0x00  u16  SkillID    — 0=Standard Attack; 1-9=class-relative slot; ≥10=absolute skill ID
		//   +0x02  u8   Unk1
		//   +0x03  u8   Unk2
		//   +0x04  s16  isValid    — (-1)=active class skill; 0=active item skill; 2=passive item skill
		//   +0x06  s16  Unk_0x6
		//   +0x08  u32  isUnusable — 0=available; 4=item skill marker; other=locked/PP cost
		//   +0x0C  u16  CondA      — primary condition index (base 4992 in UcFactorList)
		//   +0x0E  u16  CondB      — secondary condition index; 0 = none
		//
		// Item skill entries (isUnusable == 4):
		//   Active  (isValid == 0): player-set conditions stored at charAddr+92 [cond1 u16][cond2 u16]
		//   Passive (isValid == 2): player-set conditions stored in entry[i-1].CondA / entry[i-1].CondB
		public (uint skillId, int isValid, uint isUnusable, uint condA, uint condB) GetTacticRaw(int entryIndex)
		{
			uint base_ = mAddress + 96 + (uint)(entryIndex * 16);
			uint skillId    = SaveData.Instance().ReadNumber(base_ + 0,  2);
			int  isValid    = (short)SaveData.Instance().ReadNumber(base_ + 4, 2);
			uint isUnusable = SaveData.Instance().ReadNumber(base_ + 8,  4);
			uint condA      = SaveData.Instance().ReadNumber(base_ + 12, 2);
			uint condB      = SaveData.Instance().ReadNumber(base_ + 14, 2);
			return (skillId, isValid, isUnusable, condA, condB);
		}

		// Returns the player-set conditions for the FIRST tactic entry (entry[0]).
		// Under the universal one-behind rule, entry[0]'s conditions are stored at
		// charAddr+92 as a packed u32: low u16 = cond1, high u16 = cond2.
		// All subsequent entries get their conditions from the previous entry's CondA/CondB,
		// which GetTacticRaw already returns — no special accessor needed for those.
		public (uint condA, uint condB) GetFirstTacticConditions()
		{
			uint packed = SaveData.Instance().ReadNumber(mAddress + 92, 4);
			return (packed & 0xFFFF, (packed >> 16) & 0xFFFF);
		}

		// Writes player-set conditions for the tactic at display array index entryIndex.
		// Mirrors the universal one-behind read rule:
		//   entryIndex == 0 → write packed u32 to charAddr+92 [cond1 lo-u16, cond2 hi-u16]
		//   entryIndex  > 0 → write cond1/cond2 to entry[entryIndex-1].CondA / CondB
		public void SetTacticConditions(int entryIndex, uint condA, uint condB)
		{
			if (entryIndex == 0)
			{
				uint packed = (condA & 0xFFFF) | ((condB & 0xFFFF) << 16);
				SaveData.Instance().WriteNumber(mAddress + 92, 4, packed);
			}
			else
			{
				uint prevBase = mAddress + 96 + (uint)((entryIndex - 1) * 16);
				SaveData.Instance().WriteNumber(prevBase + 12, 2, condA);
				SaveData.Instance().WriteNumber(prevBase + 14, 2, condB);
			}
		}

		// ── Tactic array write helpers ────────────────────────────────────────────

		// Maximum number of tactic entries the game supports per character.
		public const int MaxTacticEntries = 8;

		// Returns the count of active (non-empty, non-PP-locked) tactic entries.
		// unusable==2 entries are empty PP-locked placeholder slots — excluded.
		public int GetTacticEntryCount()
		{
			int count = 0;
			for (int i = 0; i < 16; i++)
			{
				var (skillId, isValid, isUnusable, _, _) = GetTacticRaw(i);
				if (isUnusable == 2) continue;          // PP-locked placeholder
				if (skillId == 0 && isUnusable == 0) break; // end of array
				count++;
			}
			return count;
		}

		// Inserts a new tactic entry at raw array index insertAt, shifting all
		// subsequent entries down by one. Writes the new skill with zero conditions.
		// Caller is responsible for choosing the correct insertAt index.
		//
		// For class skills (isClassSkill=true):  writes sid=slotOrAbsoluteId, isValid=-1, isUnusable=0
		// For injected skills (isClassSkill=false): writes sid=(skillId-15), isValid from SkillInfo, isUnusable=4
		//
		// Condition storage (one-behind rule):
		//   The new entry's conditions will be stored in the slot before it.
		//   We zero those out so the new entry starts with no conditions.
		//   The entry that was previously at insertAt-1 had its CondA/CondB pointing
		//   to the old entry[insertAt]'s conditions — those are preserved in the shift.
		public void InsertTacticEntry(int insertAt, int skillId, bool isClassSkill, int classSlotId = 0)
		{
			// Read all 16 entries first
			var entries = new (uint sid, int isValid, uint isUnusable, uint condA, uint condB)[16];
			for (int i = 0; i < 16; i++)
				entries[i] = GetTacticRaw(i);

			// Shift entries from insertAt..14 down by one (entry 15 is discarded)
			for (int i = 15; i > insertAt; i--)
				entries[i] = entries[i - 1];

			// Build the new entry
			uint newSid;
			int  newIsValid;
			uint newIsUnusable;

			if (isClassSkill)
			{
				newSid        = (uint)classSlotId;
				newIsValid    = -1;
				newIsUnusable = 0;
			}
			else
			{
				newSid        = (uint)(skillId - 15);
				newIsValid    = SkillInfo.GetIsValid(skillId);
				newIsUnusable = 4;
			}

			entries[insertAt] = (newSid, newIsValid, newIsUnusable, 0, 0);

			// Write all 16 entries back
			for (int i = 0; i < 16; i++)
			{
				uint base_ = mAddress + 96 + (uint)(i * 16);
				SaveData.Instance().WriteNumber(base_ + 0,  2, entries[i].sid);
				SaveData.Instance().WriteNumber(base_ + 4,  2, (uint)(short)entries[i].isValid);
				SaveData.Instance().WriteNumber(base_ + 8,  4, entries[i].isUnusable);
				SaveData.Instance().WriteNumber(base_ + 12, 2, entries[i].condA);
				SaveData.Instance().WriteNumber(base_ + 14, 2, entries[i].condB);
			}

			// Zero out conditions for the new entry.
			// Under the one-behind rule, new entry[insertAt]'s conditions live in
			// the slot before it: charAddr+92 if insertAt==0, else entry[insertAt-1].CondA/B.
			SetTacticConditions(insertAt, 0, 0);
		}

		// Deletes the tactic entry at raw array index deleteAt, shifting all subsequent
		// entries up by one. The vacated last slot is zeroed. Conditions for the
		// deleted entry are cleared, and all condition associations are preserved
		// for the remaining entries since we shift both entries and their trailing condA/B.
		public void DeleteTacticEntry(int deleteAt)
		{
			var entries = new (uint sid, int isValid, uint isUnusable, uint condA, uint condB)[16];
			for (int i = 0; i < 16; i++)
				entries[i] = GetTacticRaw(i);

			// Before shifting, capture the conditions stored FOR the entry being deleted.
			// Those live in the preceding slot (or charAddr+92 for index 0).
			// After deletion we need to propagate them correctly.
			// Actually: we just shift entries up. The condA/condB of each entry are
			// the conditions for the NEXT entry — so shifting preserves all associations
			// except for deleteAt-1's condA/B which pointed to deleteAt's conditions.
			// We clear those since the deleted entry no longer exists.

			// Shift entries up
			for (int i = deleteAt; i < 15; i++)
				entries[i] = entries[i + 1];

			// Zero the last slot
			entries[15] = (0, 0, 0, 0, 0);

			// Write all back
			for (int i = 0; i < 16; i++)
			{
				uint base_ = mAddress + 96 + (uint)(i * 16);
				SaveData.Instance().WriteNumber(base_ + 0,  2, entries[i].sid);
				SaveData.Instance().WriteNumber(base_ + 4,  2, (uint)(short)entries[i].isValid);
				SaveData.Instance().WriteNumber(base_ + 8,  4, entries[i].isUnusable);
				SaveData.Instance().WriteNumber(base_ + 12, 2, entries[i].condA);
				SaveData.Instance().WriteNumber(base_ + 14, 2, entries[i].condB);
			}

			// Clear the conditions that were stored FOR the deleted entry
			// (they lived in the slot before deleteAt, which is now the slot before
			// what was deleteAt+1 — i.e. still deleteAt-1 / charAddr+92).
			// We zero them since the entry they belonged to is gone.
			SetTacticConditions(deleteAt, 0, 0);
		}

		public string DisplayName
		{
			get
			{
				// Try the story character name lookup first
				var storyName = Info.Instance().Search(Info.Instance().Name, Name);
				if (storyName != null)
					return storyName.Name;

				// Fall back to the generic name pool (offset +36)
				var genericName = Info.Instance().Search(Info.Instance().GenericName, GenericNameIndex);
				if (genericName != null)
					return genericName.Name;

				// Last resort: raw ID
				return Name.ToString();
			}
		}

		public uint Exp
		{
			get => SaveData.Instance().ReadNumber(mAddress + 56, 4);
			set => SaveData.Instance().WriteNumber(mAddress + 56, 4, value);
		}

		public uint GrowthType1
		{
			get => SaveData.Instance().ReadNumber(mAddress + 0x29, 1);
			set
			{
				SaveData.Instance().WriteNumber(mAddress + 0x29, 1, value);
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GrowthType1)));
			}
		}

		public uint GrowthType2
		{
			get => SaveData.Instance().ReadNumber(mAddress + 0x2A, 1);
			set
			{
				SaveData.Instance().WriteNumber(mAddress + 0x2A, 1, value);
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GrowthType2)));
			}
		}

		// 1 = Male, 2 = Female (charAddr + 0x30)
		public uint Gender
		{
			get => SaveData.Instance().ReadNumber(mAddress + 0x30, 1);
			set
			{
				SaveData.Instance().WriteNumber(mAddress + 0x30, 1, value);
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Gender)));
			}
		}

		public uint Lv
		{
			get => SaveData.Instance().ReadNumber(mAddress + 60, 2);
			set => SaveData.Instance().WriteNumber(mAddress + 60, 2, value);
		}

		public uint HPPlus
		{
			get => SaveData.Instance().ReadNumber(mAddress + 64, 1);
			set => SaveData.Instance().WriteNumber(mAddress + 64, 1, value);
		}

		public uint AttackPlus
		{
			get => SaveData.Instance().ReadNumber(mAddress + 65, 1);
			set => SaveData.Instance().WriteNumber(mAddress + 65, 1, value);
		}

		public uint DefensePlus
		{
			get => SaveData.Instance().ReadNumber(mAddress + 66, 1);
			set => SaveData.Instance().WriteNumber(mAddress + 66, 1, value);
		}

		public uint MagicAttackPlus
		{
			get => SaveData.Instance().ReadNumber(mAddress + 67, 1);
			set => SaveData.Instance().WriteNumber(mAddress + 67, 1, value);
		}

		public uint MagicDefensePlus
		{
			get => SaveData.Instance().ReadNumber(mAddress + 68, 1);
			set => SaveData.Instance().WriteNumber(mAddress + 68, 1, value);
		}

		public uint HitRatePlus
		{
			get => SaveData.Instance().ReadNumber(mAddress + 69, 1);
			set => SaveData.Instance().WriteNumber(mAddress + 69, 1, value);
		}

		public uint AVoidPlus
		{
			get => SaveData.Instance().ReadNumber(mAddress + 70, 1);
			set => SaveData.Instance().WriteNumber(mAddress + 70, 1, value);
		}

		public uint CriticalPlus
		{
			get => SaveData.Instance().ReadNumber(mAddress + 71, 1);
			set => SaveData.Instance().WriteNumber(mAddress + 71, 1, value);
		}

		public uint GuardPlus
		{
			get => SaveData.Instance().ReadNumber(mAddress + 72, 1);
			set => SaveData.Instance().WriteNumber(mAddress + 72, 1, value);
		}

		public uint SpeedPlus
		{
			get => SaveData.Instance().ReadNumber(mAddress + 73, 1);
			set => SaveData.Instance().WriteNumber(mAddress + 73, 1, value);
		}

		public bool Use
		{
			get => !SaveData.Instance().ReadBit(mAddress + 460, 5);
			set => SaveData.Instance().WriteBit(mAddress + 460, 5, !value);
		}
	}
}
