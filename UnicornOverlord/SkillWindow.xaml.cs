using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace UnicornOverlord
{
    /// <summary>
    /// Skill picker window — filter + listbox with A/P badge per skill.
    /// Caller sets nothing before ShowDialog(); reads SkillId back after OK.
    /// SkillId is the raw skill.txt ID (not the tactic storage offset).
    /// </summary>
    public partial class SkillWindow : Window
    {
        /// <summary>The selected skill's skill.txt ID. 0 if nothing selected.</summary>
        public int SkillId { get; private set; }

        private readonly List<SkillEntry> mAllEntries = new();

        public SkillWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            foreach (var entry in Info.Instance().Skill)
            {
                int id = (int)entry.Value;
                string badgeColor = SkillInfo.IsPassive(id) ? "#4A7FC1"
                                  : SkillInfo.IsActive(id)  ? "#C13A3A"
                                  : "#888888";

                mAllEntries.Add(new SkillEntry(id, entry.Name, badgeColor));
            }

            // Alphabetical order
            mAllEntries.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

            PopulateList(string.Empty);
        }

        private void TextBoxFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            PopulateList(TextBoxFilter.Text);
        }

        private void ListBoxSkill_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ButtonOK.IsEnabled = ListBoxSkill.SelectedIndex >= 0;
        }

        private void ButtonOK_Click(object sender, RoutedEventArgs e)
        {
            if (ListBoxSkill.SelectedItem is SkillEntry se)
                SkillId = se.Id;
            DialogResult = true;
            Close();
        }

        private void PopulateList(string filter)
        {
            ListBoxSkill.Items.Clear();
            foreach (var entry in mAllEntries)
            {
                if (string.IsNullOrEmpty(filter) ||
                    entry.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    ListBoxSkill.Items.Add(entry);
                }
            }
        }

        private record SkillEntry(int Id, string Name, string BadgeColor);
    }
}
