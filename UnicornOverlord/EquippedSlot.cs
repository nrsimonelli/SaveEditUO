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
            }
        }

        public bool IsEmpty => mItemIndex == 0;

        public EquippedSlot(int slotNumber)
        {
            SlotNumber = slotNumber;
        }
    }
}
