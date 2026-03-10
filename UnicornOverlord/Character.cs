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
