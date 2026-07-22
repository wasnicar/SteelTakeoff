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
        private double _linearFeet = 20.0;
        private bool _isIntumescent;
        private double _wftMils;
        private int _coats = 1;
        private bool _isSelected;
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
            _wftMils = settings?.DefaultWftMils ?? 0;
            _coats = settings?.DefaultCoats > 0 ? settings.DefaultCoats : 1;

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

        /// <summary>Effective SF/hr for this member — productivity after any WFT penalty.</summary>
        public double EffectiveProductivity =>
            TakeoffCalculator.EffectiveProductivity(ToLine(), _settings?.WftLaborDivisor ?? 5.0);

        /// <summary>Labor price per square foot for this member.</summary>
        public double LaborPricePerSquareFoot =>
            TakeoffCalculator.LaborPricePerSquareFoot(ToLine(), _settings?.WftLaborDivisor ?? 5.0);

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

        /// <summary>Specified wet film thickness (mils). Only used when Intumescent is ticked.</summary>
        public double WftMils
        {
            get => _wftMils;
            set { if (Set(ref _wftMils, value)) NotifyComputed(); }
        }

        /// <summary>Number of coats for this line (any coating type). Multiplies area.</summary>
        public int Coats
        {
            get => _coats;
            set { if (Set(ref _coats, value)) NotifyComputed(); }
        }

        public bool IsPlate => SelectedFamily != null && SelectedFamily.IsPlate;
        public string CoatingLabel => IsIntumescent ? "Intumescent" : "Standard";

        public double SfPerFoot => TakeoffCalculator.SfPerFoot(ToLine());

        /// <summary>Wrapped coating area — the quantity sent to Sage (same for both coating types).</summary>
        public double AreaSquareFeet => TakeoffCalculator.AreaSquareFeet(ToLine());

        /// <summary>Labor dollars for this line, using this member's own wage + productivity.</summary>
        public double LaborAmount =>
            TakeoffCalculator.LaborAmount(ToLine(), _settings?.WftLaborDivisor ?? 5.0);

        /// <summary>Step-by-step derivation shown under the row by "Show calculation".</summary>
        public IReadOnlyList<CalculationStep> CalculationSteps => TakeoffExplainer.Explain(ToLine(), _settings);

        public CoatingType Coating => IsIntumescent ? CoatingType.Intumescent : CoatingType.Standard;

        public TakeoffLine ToLine() => new TakeoffLine
        {
            Family = SelectedFamily,
            Shape = SelectedShape,
            PlateWidthInches = PlateWidthInches,
            LinearFeet = LinearFeet,
            Coating = Coating,
            WftMils = WftMils,
            Coats = Coats,
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
