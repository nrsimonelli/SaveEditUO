namespace UnicornOverlord
{
	/// <summary>
	/// One row in the per-character tactic display.
	///
	/// Matches the in-game tactic screen layout:
	///   Action | Condition 1 | Condition 2
	///
	/// Confirmed entry layout (16 bytes):
	///   +0x00  u16  SkillID    — 0=Standard Attack; 1-9=class-relative slot; ≥10=absolute skill ID
	///   +0x02  u8   Unk1
	///   +0x03  u8   Unk2
	///   +0x04  s16  isValid    — (-1)=active class skill; 0=active item skill; 2=passive item skill
	///   +0x06  s16  Unk_0x6
	///   +0x08  u32  isUnusable — 0=available; 4=item skill marker; other=locked/PP cost
	///   +0x0C  u16  CondA      — primary condition index (FactorNames key)
	///   +0x0E  u16  CondB      — secondary condition index; 0=none
	///
	/// Item skill condition storage (confirmed by save diff):
	///   Active  (isUnusable==4, isValid==0):  conditions at charAddr+92
	///   Passive (isUnusable==4, isValid==2):  conditions in entry[i-1].CondA/CondB
	/// </summary>
	internal class TacticEntry
	{
		// ── Raw fields (populated by ViewModel) ────────────────────────────
		public ushort SkillID    { get; set; }
		public short  IsValid    { get; set; }
		public uint   IsUnusable { get; set; }
		public ushort CondA      { get; set; }
		public ushort CondB      { get; set; }

		// ── Resolved display strings (populated by ViewModel) ──────────────

		/// <summary>The skill name shown in the Action column.</summary>
		public string Action { get; set; } = string.Empty;

		/// <summary>Primary condition text (Condition 1 column).</summary>
		public string Condition1 { get; set; } = string.Empty;

		/// <summary>Secondary condition text (Condition 2 column). Empty when none.</summary>
		public string Condition2 { get; set; } = string.Empty;

		/// <summary>
		/// The 0-based index of this entry in the raw 16-entry tactic array.
		/// Used by the write path to know which slot to update when conditions change.
		/// Conditions for entry[i] are written to entry[i-1].CondA/CondB (or charAddr+92 if i==0).
		/// </summary>
		public int ArrayIndex { get; set; }

		/// <summary>
		/// Set by ViewModel: true for active skills (class slots 3/4/5 or active item skills),
		/// false for passive skills (class slots 7/8/9 or passive item skills).
		/// Drives badge colour.
		/// </summary>
		public bool IsActiveSkill { get; set; }

		/// <summary>Badge colour: red for active skills, blue for passive skills.</summary>
		public string BadgeColor => IsActiveSkill ? "#C13A3A" : "#4A7FC1";

		/// <summary>
		/// True when this entry is an item skill (isUnusable == 4).
		/// </summary>
		public bool IsItemSkill => IsUnusable == 4;

		/// <summary>All slotted skills are deletable — the player put them there.</summary>
		public bool IsDeletable => true;
	}
}
