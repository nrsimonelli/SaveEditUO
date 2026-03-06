using System.ComponentModel;

namespace UnicornOverlord
{
    internal class EquippedSlot : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public int SlotNumber { get; }
        public string SlotLabel => $"Slot {SlotNumber + 1}";

        private string mItemName = "(empty)";
        public string ItemName
        {
            get => mItemName;
            set
            {
                mItemName = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ItemName)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEmpty)));
            }
        }

        private uint mItemIndex;
        public uint ItemIndex
        {
            get => mItemIndex;
            set
            {
                mItemIndex = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ItemIndex)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEmpty)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasItem)));
            }
        }

        // True if this slot is locked (slot 4 for unpromoted characters)
        public bool IsLocked { get; set; }

        public bool IsEmpty => mItemIndex == 0;
        public bool HasItem => mItemIndex != 0;

        // True if an item can be created and equipped into this slot
        public bool CanCreate => IsEmpty && !IsLocked;

        public EquippedSlot(int slotNumber)
        {
            SlotNumber = slotNumber;
        }
    }
}
