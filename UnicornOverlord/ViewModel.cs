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

		public Basic Basic { get; set; } = new Basic();
		public ObservableCollection<Character> Characters { get; set; } = new ObservableCollection<Character>();
		public ObservableCollection<Item> Items { get; set; } = new ObservableCollection<Item>();
		public ObservableCollection<Item> Equipments { get; set; } = new ObservableCollection<Item>();
		public ObservableCollection<Unit> Units { get; set; } = new ObservableCollection<Unit>();
		public ObservableCollection<EquippedSlot> EquippedSlots { get; set; } = new ObservableCollection<EquippedSlot>();

		private Character? mSelectedCharacter;
		public Character? SelectedCharacter
		{
			get => mSelectedCharacter;
			set
			{
				mSelectedCharacter = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedCharacter)));
				RefreshEquippedSlots();
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

		private void ChoiceClass(object? parameter)
		{
			Character? ch = parameter as Character;
			if (ch == null) return;

			var dlg = new ChoiceWindow();
			dlg.Type = ChoiceWindow.eType.eClass;
			dlg.ID = ch.Class;
			dlg.ShowDialog();
			ch.Class = dlg.ID;
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

		private void RefreshEquippedSlots()
		{
			EquippedSlots.Clear();
			if (mSelectedCharacter == null) return;

			// Build a lookup from item Index -> item name from the full equipment list
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

				if (itemIndex != 0 && indexToName.TryGetValue(itemIndex, out var name))
				{
					equippedSlot.ItemIndex = itemIndex;
					equippedSlot.ItemName = name;
				}

				EquippedSlots.Add(equippedSlot);
			}
		}

		private void MorphSlot(object? parameter)
		{
			EquippedSlot? slot = parameter as EquippedSlot;
			if (slot == null || slot.IsEmpty) return;

			// Find the Item in Equipments by its Index value
			var item = Equipments.FirstOrDefault(e => e.Index == slot.ItemIndex);
			if (item == null) return;

			// Reuse the existing choice dialog to pick a new ID
			var dlg = new ChoiceWindow();
			dlg.Type = ChoiceWindow.eType.eEquipment;
			dlg.ID = item.ID;
			dlg.ShowDialog();
			if (dlg.ID == 0) return;

			item.ID = dlg.ID;
			item.Status = 4;

			// Refresh display
			RefreshEquippedSlots();
		}

		private void DeleteSlot(object? parameter)
		{
			EquippedSlot? slot = parameter as EquippedSlot;
			if (slot == null || slot.IsEmpty) return;

			var item = Equipments.FirstOrDefault(e => e.Index == slot.ItemIndex);
			if (item == null) return;

			// Reuse the full delete logic
			DeleteEquipment(item);

			// Refresh display
			RefreshEquippedSlots();
		}

		private void CreateAndEquip(object? parameter)
		{
			EquippedSlot? slot = parameter as EquippedSlot;
			if (slot == null || mSelectedCharacter == null) return;
			if (!slot.IsEmpty) return; // slot already occupied

			int charIdx = Characters.IndexOf(mSelectedCharacter);
			if (charIdx < 0) return;

			// Append a new equipment item via the choice dialog
			var item = AppendItem(ChoiceWindow.eType.eEquipment);
			if (item == null) return;

			// Set equipment ownership fields
			item.Equipment1 = (uint)slot.SlotNumber; // slot within character (0-3)
			item.Equipment2 = (uint)charIdx;          // character array index (0-based)

			// Write the item's Index into the character's slot in the save
			uint charAddr = Util.calcCharacterAddress((uint)charIdx);
			uint slotAddr = charAddr + 76 + (uint)(slot.SlotNumber * 4);
			SaveData.Instance().WriteNumber(slotAddr, 4, item.Index);

			Equipments.Add(item);

			// Refresh display
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
