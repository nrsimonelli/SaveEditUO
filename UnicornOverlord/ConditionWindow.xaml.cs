using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace UnicornOverlord
{
	/// <summary>
	/// Condition picker window — same filter+listbox pattern as ChoiceWindow.
	/// Presents all entries from FactorNames (stored condition value → display name).
	/// The caller sets ConditionValue before ShowDialog(); reads it back after.
	/// A "None" entry (value 0) is always present at the top so conditions can be cleared.
	/// </summary>
	public partial class ConditionWindow : Window
	{
		/// <summary>The selected condition's stored value (key into FactorNames).</summary>
		public uint ConditionValue { get; set; }

		// Full sorted list built once at load time
		private readonly List<ConditionEntry> mAllEntries = new();

		public ConditionWindow()
		{
			InitializeComponent();
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			// Build master list: None first, then all FactorNames in order
			mAllEntries.Add(new ConditionEntry(0, "None"));
			foreach (var kv in FactorNames.All)
				mAllEntries.Add(new ConditionEntry(kv.Key, kv.Value));

			PopulateList(string.Empty);

			// Pre-select current value and scroll it into view
			foreach (var item in ListBoxCondition.Items)
			{
				if (item is ConditionEntry ce && ce.Value == ConditionValue)
				{
					ListBoxCondition.SelectedItem = item;
					ListBoxCondition.ScrollIntoView(item);
					break;
				}
			}
		}

		private void TextBoxFilter_TextChanged(object sender, TextChangedEventArgs e)
		{
			PopulateList(TextBoxFilter.Text);
		}

		private void ListBoxCondition_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			ButtonOK.IsEnabled = ListBoxCondition.SelectedIndex >= 0;
		}

		private void ButtonOK_Click(object sender, RoutedEventArgs e)
		{
			if (ListBoxCondition.SelectedItem is ConditionEntry ce)
				ConditionValue = ce.Value;
			DialogResult = true;
			Close();
		}

		private void PopulateList(string filter)
		{
			ListBoxCondition.Items.Clear();
			foreach (var entry in mAllEntries)
			{
				if (string.IsNullOrEmpty(filter) ||
				    entry.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					ListBoxCondition.Items.Add(entry);
				}
			}
		}

		// Simple display record used by the listbox
		private record ConditionEntry(uint Value, string Name)
		{
			public override string ToString() => Name;
		}
	}
}
