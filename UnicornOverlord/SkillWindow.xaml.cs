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
            // Build master list from Info.Skill, annotate each with active/passive badge
            foreach (var entry in Info.Instance().Skill)
            {
                int id = (int)entry.Value;
                bool isPassive = SkillInfo.IsPassive(id);
                bool isActive  = SkillInfo.IsActive(id);

                string badge;
                string badgeColor;
                if (isActive)
                {
                    badge      = "A";
                    badgeColor = "#C13A3A";
                }
                else if (isPassive)
                {
                    badge      = "P";
                    badgeColor = "#4A7FC1";
                }
                else
                {
                    // Unclassified (enemy/scenario skills) — neutral grey, still selectable
                    badge      = "?";
                    badgeColor = "#888888";
                }

                mAllEntries.Add(new SkillEntry(id, entry.Name, badge, badgeColor));
            }

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

        private record SkillEntry(int Id, string Name, string Badge, string BadgeColor);
    }
}
