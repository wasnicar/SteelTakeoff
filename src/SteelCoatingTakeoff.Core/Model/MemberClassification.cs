namespace SteelCoatingTakeoff.Core.Model
{
    /// <summary>What the member is, structurally. Informational — no cost/quantity effect.</summary>
    public enum MemberKind
    {
        /// <summary>Not classified.</summary>
        Unspecified = 0,
        Column = 1,
        Beam = 2
    }

    /// <summary>
    /// What a COLUMN supports — a floor or a roof. Only meaningful when the member is a
    /// column; ignored otherwise. The supplier uses it to specify the WFT.
    /// </summary>
    public enum SupportKind
    {
        /// <summary>Not specified.</summary>
        Unspecified = 0,
        Floor = 1,
        Roof = 2
    }

    /// <summary>Formatting for the member classification, shared by the reports and Sage.</summary>
    public static class MemberClassification
    {
        /// <summary>
        /// Compact label, e.g. "Column/Floor", "Column", "Beam", or "" when untyped.
        /// The support half is shown only for columns (it is meaningless on a beam).
        /// </summary>
        public static string ShortLabel(MemberKind kind, SupportKind support)
        {
            switch (kind)
            {
                case MemberKind.Column:
                    return support == SupportKind.Floor ? "Column/Floor"
                         : support == SupportKind.Roof ? "Column/Roof"
                         : "Column";
                case MemberKind.Beam:
                    return "Beam";
                default:
                    return "";
            }
        }

        /// <summary>The support word for a column ("Floor"/"Roof"), else "".</summary>
        public static string SupportLabel(MemberKind kind, SupportKind support)
        {
            if (kind != MemberKind.Column) return "";
            return support == SupportKind.Floor ? "Floor"
                 : support == SupportKind.Roof ? "Roof"
                 : "";
        }

        public static string KindLabel(MemberKind kind)
        {
            switch (kind)
            {
                case MemberKind.Column: return "Column";
                case MemberKind.Beam: return "Beam";
                default: return "";
            }
        }
    }
}
