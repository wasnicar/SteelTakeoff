using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SteelCoatingTakeoff.Core;
using SteelCoatingTakeoff.Core.Model;
using SteelCoatingTakeoff.Core.Sage;

namespace SteelCoatingTakeoff.App.ViewModels
{
    /// <summary>
    /// One editable line in the takeoff grid. Mirrors <see cref="TakeoffLine"/> and
    /// recomputes SF/LF and area live as the user edits.
    /// </summary>
    public sealed class TakeoffRowViewModel : ObservableObject
    {
        /// <summary>Raised whenever a value that affects totals changes.</summary>
        public event EventHandler Changed;

        public IReadOnlyList<ShapeFamily> Families { get; }
        public ObservableCollection<SteelShape> Sizes { get; } = new ObservableCollection<SteelShape>();

        private readonly SageSettings _settings;

        private ShapeFamily _family;
        private SteelShape _shape;
        private double _plateWidthInches = 12.0;
        private double _linearFeet;            // blank until the user enters the takeoff length
        private double _multiplier = 1.0;
        private bool _isIntumescent;
        private double _dftMils;
        private int _coats = 1;
        private string _fireRating = "";
        private MemberKind _memberType = MemberKind.Unspecified;
        private SupportKind _support = SupportKind.Unspecified;
        private bool _isSelected;

        /// <summary>Combo choices (blank = unspecified).</summary>
        public static readonly string[] MemberTypeOptions = { "", "Column", "Beam" };
        public static readonly string[] SupportOptions = { "", "Floor", "Roof" };
        private double _wageRate;
        private double _productivity;
        private double _laborProductivityFactor = 1.0;

        public TakeoffRowViewModel(
            IReadOnlyList<ShapeFamily> families,
            ShapeFamily initialFamily = null,
            SageSettings settings = null)
        {
            Families = families;
            _settings = settings;
            _dftMils = settings?.DefaultDftMils ?? 0;
            _coats = settings?.DefaultCoats > 0 ? settings.DefaultCoats : 1;
            _fireRating = settings?.DefaultFireRating ?? "";
            _memberType = settings?.DefaultMemberType ?? MemberKind.Unspecified;
            _support = _memberType == MemberKind.Column
                ? (settings?.DefaultSupport ?? SupportKind.Unspecified)
                : SupportKind.Unspecified;

            // Labor is per member; the settings supply the starting values for a new one.
            _wageRate = settings?.WageRate ?? 0.0;
            _productivity = settings?.Productivity ?? 0.0;
            _laborProductivityFactor = settings?.LaborProductivityFactor > 0
                ? settings.LaborProductivityFactor : 1.0;

            SelectedFamily = initialFamily ?? families.FirstOrDefault();
        }

        /// <summary>
        /// Whether this member is part of the current selection. Two-way bound to
        /// DataGridRow.IsSelected, so the checkbox column, ctrl/shift-clicking and the
        /// mass-edit panel all read and write the same flag.
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set { if (Set(ref _isSelected, value)) Changed?.Invoke(this, EventArgs.Empty); }
        }

        // ---- per-member labor ---------------------------------------------

        public double WageRate
        {
            get => _wageRate;
            set { if (Set(ref _wageRate, value)) NotifyComputed(); }
        }

        public double Productivity
        {
            get => _productivity;
            set { if (Set(ref _productivity, value)) NotifyComputed(); }
        }

        public double LaborProductivityFactor
        {
            get => _laborProductivityFactor;
            set { if (Set(ref _laborProductivityFactor, value)) NotifyComputed(); }
        }

        private double Divisor => _settings?.WftLaborDivisor ?? 5.0;
        private double Solids => _settings?.VolumeSolidsPercent ?? 65.0;

        /// <summary>Effective SF/hr for this member — productivity after the DFT-derived thickness penalty.</summary>
        public double EffectiveProductivity =>
            TakeoffCalculator.EffectiveProductivity(ToLine(), Divisor, Solids);

        /// <summary>Labor price per square foot for this member.</summary>
        public double LaborPricePerSquareFoot =>
            TakeoffCalculator.LaborPricePerSquareFoot(ToLine(), Divisor, Solids);

        public ShapeFamily SelectedFamily
        {
            get => _family;
            set
            {
                if (!Set(ref _family, value)) return;
                RebuildSizes();
                Raise(nameof(IsPlate));
                NotifyComputed();
            }
        }

        public SteelShape SelectedShape
        {
            get => _shape;
            set { if (Set(ref _shape, value)) NotifyComputed(); }
        }

        public double PlateWidthInches
        {
            get => _plateWidthInches;
            set { if (Set(ref _plateWidthInches, value)) NotifyComputed(); }
        }

        public double LinearFeet
        {
            get => _linearFeet;
            set { if (Set(ref _linearFeet, value)) NotifyComputed(); }
        }

        /// <summary>
        /// How many identical members this row stands for — the same length taken off more
        /// than once. Multiplies the displayed area and labor, and drives the Sage assembly
        /// quantity. Defaults to 1.
        /// </summary>
        public double Multiplier
        {
            get => _multiplier;
            set { if (Set(ref _multiplier, value <= 0 ? 1.0 : value)) NotifyComputed(); }
        }

        /// <summary>Intumescent YES/NO — the routing switch, and what makes WFT apply.</summary>
        public bool IsIntumescent
        {
            get => _isIntumescent;
            set
            {
                if (!Set(ref _isIntumescent, value)) return;
                Raise(nameof(CoatingLabel));
                NotifyComputed();
            }
        }

        /// <summary>Specified DRY film thickness (mils), from the supplier. Only used when Intumescent is ticked.</summary>
        public double DftMils
        {
            get => _dftMils;
            set { if (Set(ref _dftMils, value)) NotifyComputed(); }
        }

        /// <summary>Number of coats for this line (any coating type). Multiplies area.</summary>
        public int Coats
        {
            get => _coats;
            set { if (Set(ref _coats, value)) NotifyComputed(); }
        }

        /// <summary>
        /// Required fire rating for this member (free text, e.g. "2 hr"). Informational:
        /// it rides onto the supplier report and the Sage assembly description, and does
        /// not affect area or labor — so no recompute, just a totals-neutral notify.
        /// </summary>
        public string FireRating
        {
            get => _fireRating;
            set { if (Set(ref _fireRating, value)) Changed?.Invoke(this, EventArgs.Empty); }
        }

        /// <summary>Column / Beam / blank, bound to the grid combo as text.</summary>
        public string MemberTypeText
        {
            get => MemberClassification.KindLabel(_memberType);
            set
            {
                var kind = string.Equals(value, "Column", StringComparison.OrdinalIgnoreCase) ? MemberKind.Column
                         : string.Equals(value, "Beam", StringComparison.OrdinalIgnoreCase) ? MemberKind.Beam
                         : MemberKind.Unspecified;
                if (_memberType == kind) return;
                _memberType = kind;
                // Support only applies to a column; drop it otherwise so it can't linger.
                if (_memberType != MemberKind.Column) _support = SupportKind.Unspecified;
                Raise(nameof(MemberTypeText));
                Raise(nameof(IsColumn));
                Raise(nameof(SupportText));
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>True when this member is a column — enables the Floor/Roof combo.</summary>
        public bool IsColumn => _memberType == MemberKind.Column;

        /// <summary>Floor / Roof / blank for a column, bound to the grid combo as text.</summary>
        public string SupportText
        {
            get => MemberClassification.SupportLabel(_memberType, _support);
            set
            {
                var support = string.Equals(value, "Floor", StringComparison.OrdinalIgnoreCase) ? SupportKind.Floor
                            : string.Equals(value, "Roof", StringComparison.OrdinalIgnoreCase) ? SupportKind.Roof
                            : SupportKind.Unspecified;
                if (_support == support) return;
                _support = support;
                Raise(nameof(SupportText));
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        public MemberKind MemberType => _memberType;
        public SupportKind Support => _support;

        public bool IsPlate => SelectedFamily != null && SelectedFamily.IsPlate;
        public string CoatingLabel => IsIntumescent ? "Intumescent" : "Standard";

        public double SfPerFoot => TakeoffCalculator.SfPerFoot(ToLine());

        /// <summary>Total wrapped coating area for this row — per-member area × member count.</summary>
        public double AreaSquareFeet => TakeoffCalculator.TotalAreaSquareFeet(ToLine());

        /// <summary>Labor dollars for this line, using this member's own wage + productivity.</summary>
        public double LaborAmount =>
            TakeoffCalculator.LaborAmount(ToLine(), Divisor, Solids);

        /// <summary>Step-by-step derivation shown under the row by "Show calculation".</summary>
        public IReadOnlyList<CalculationStep> CalculationSteps => TakeoffExplainer.Explain(ToLine(), _settings);

        public CoatingType Coating => IsIntumescent ? CoatingType.Intumescent : CoatingType.Standard;

        public TakeoffLine ToLine() => new TakeoffLine
        {
            Family = SelectedFamily,
            Shape = SelectedShape,
            PlateWidthInches = PlateWidthInches,
            LinearFeet = LinearFeet,
            Multiplier = Multiplier,
            Coating = Coating,
            DftMils = DftMils,
            Coats = Coats,
            FireRating = FireRating,
            MemberType = _memberType,
            Support = _support,
            WageRate = WageRate,
            Productivity = Productivity,
            LaborProductivityFactor = LaborProductivityFactor
        };

        private void RebuildSizes()
        {
            Sizes.Clear();
            if (SelectedFamily != null)
                foreach (var s in SelectedFamily.Shapes) Sizes.Add(s);
            SelectedShape = Sizes.FirstOrDefault();
        }

        private void NotifyComputed()
        {
            Raise(nameof(SfPerFoot));
            Raise(nameof(AreaSquareFeet));
            Raise(nameof(LaborAmount));
            Raise(nameof(EffectiveProductivity));
            Raise(nameof(LaborPricePerSquareFoot));
            Raise(nameof(CalculationSteps));
            Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Repopulate this row from a saved/loaded line. Family is set first so the size
        /// list rebuilds, then the exact shape is restored on top.
        /// </summary>
        public void LoadFrom(TakeoffLine line)
        {
            if (line.Family != null) SelectedFamily = line.Family;
            if (line.Shape != null) SelectedShape = line.Shape;
            PlateWidthInches = line.PlateWidthInches;
            LinearFeet = line.LinearFeet;
            Multiplier = line.Multiplier <= 0 ? 1.0 : line.Multiplier;
            IsIntumescent = line.Coating == CoatingType.Intumescent;
            DftMils = line.DftMils;
            Coats = line.Coats;
            _fireRating = line.FireRating ?? "";
            _memberType = line.MemberType;
            _support = line.MemberType == MemberKind.Column ? line.Support : SupportKind.Unspecified;
            _wageRate = line.WageRate;
            _productivity = line.Productivity;
            _laborProductivityFactor = line.LaborProductivityFactor <= 0 ? 1.0 : line.LaborProductivityFactor;

            Raise(nameof(FireRating));
            Raise(nameof(MemberTypeText));
            Raise(nameof(SupportText));
            Raise(nameof(IsColumn));
            Raise(nameof(WageRate));
            Raise(nameof(Productivity));
            Raise(nameof(LaborProductivityFactor));
            NotifyComputed();
        }

        /// <summary>
        /// Push new labor values in from a mass edit without going through the public
        /// setters one at a time, so the grid and totals refresh once.
        /// </summary>
        public void ApplyLabor(double? wage, double? productivity, double? factor)
        {
            if (wage.HasValue) _wageRate = wage.Value;
            if (productivity.HasValue) _productivity = productivity.Value;
            if (factor.HasValue) _laborProductivityFactor = factor.Value;

            Raise(nameof(WageRate));
            Raise(nameof(Productivity));
            Raise(nameof(LaborProductivityFactor));
            NotifyComputed();
        }

        /// <summary>
        /// Re-read after the Sage panel changes — routing, area delivery and the labor
        /// rule (divisor, rates) all live in settings.
        /// </summary>
        public void RefreshCalculation() => NotifyComputed();
    }
}
