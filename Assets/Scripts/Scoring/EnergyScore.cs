using System;
using System.Globalization;
using UnityEngine;

namespace Massive.Scoring
{
    /// <summary>
    /// Engineering units used by the MASSIVE score display.
    /// The canonical runtime/storage unit is one millielectronvolt (meV).
    /// </summary>
    public enum EnergyUnit
    {
        MilliElectronVolt = 0,
        ElectronVolt = 1,
        KiloElectronVolt = 2,
        MegaElectronVolt = 3,
        GigaElectronVolt = 4,
        TeraElectronVolt = 5
    }

    [Serializable]
    public struct EnergyAmount
    {
        [SerializeField] private long milliElectronVolts;

        public long RawMilliElectronVolts => Math.Max(0L, milliElectronVolts);

        public EnergyAmount(long rawMilliElectronVolts)
        {
            milliElectronVolts = Math.Max(0L, rawMilliElectronVolts);
        }

        public static EnergyAmount From(long value, EnergyUnit unit)
        {
            return new EnergyAmount(EnergyScoreMath.ToRawMilliElectronVolts(value, unit));
        }

        public override string ToString()
        {
            return EnergyScoreFormatter.FormatWithUnit(RawMilliElectronVolts);
        }
    }

    [Serializable]
    public struct EnergyDisplayValue
    {
        public EnergyUnit unit;
        public string unitLabel;
        public long whole;
        public int fractionalThousandths;
        public bool exceedsFixedTopTierDisplay;
        public float tierProgress01;
    }

    public static class EnergyScoreMath
    {
        public const long MilliElectronVoltFactor = 1L;
        public const long ElectronVoltFactor = 1_000L;
        public const long KiloElectronVoltFactor = 1_000_000L;
        public const long MegaElectronVoltFactor = 1_000_000_000L;
        public const long GigaElectronVoltFactor = 1_000_000_000_000L;
        public const long TeraElectronVoltFactor = 1_000_000_000_000_000L;

        private static readonly long[] Factors =
        {
            MilliElectronVoltFactor,
            ElectronVoltFactor,
            KiloElectronVoltFactor,
            MegaElectronVoltFactor,
            GigaElectronVoltFactor,
            TeraElectronVoltFactor
        };

        private static readonly string[] Labels =
        {
            "meV",
            "eV",
            "keV",
            "MeV",
            "GeV",
            "TeV"
        };

        public static int UnitCount => Factors.Length;
        public static EnergyUnit HighestUnit => EnergyUnit.TeraElectronVolt;

        public static long GetFactor(EnergyUnit unit)
        {
            int index = Mathf.Clamp((int)unit, 0, Factors.Length - 1);
            return Factors[index];
        }

        public static string GetLabel(EnergyUnit unit)
        {
            int index = Mathf.Clamp((int)unit, 0, Labels.Length - 1);
            return Labels[index];
        }

        public static long ToRawMilliElectronVolts(long value, EnergyUnit unit)
        {
            if (value <= 0L) return 0L;
            return SaturatingMultiply(value, GetFactor(unit));
        }

        public static long SaturatingAdd(long a, long b)
        {
            if (a < 0L) a = 0L;
            if (b <= 0L) return a;
            if (long.MaxValue - a < b) return long.MaxValue;
            return a + b;
        }

        public static long SaturatingMultiply(long a, long b)
        {
            if (a <= 0L || b <= 0L) return 0L;
            if (a > long.MaxValue / b) return long.MaxValue;
            return a * b;
        }
    }

    public static class EnergyScoreFormatter
    {
        private const int DefaultIntegerDigits = 3;
        private const int DefaultFractionDigits = 3;

        public static EnergyUnit GetUnit(long rawMilliElectronVolts)
        {
            long raw = Math.Max(0L, rawMilliElectronVolts);

            for (int i = (int)EnergyScoreMath.HighestUnit; i > 0; i--)
            {
                EnergyUnit unit = (EnergyUnit)i;
                if (raw >= EnergyScoreMath.GetFactor(unit))
                    return unit;
            }

            return EnergyUnit.MilliElectronVolt;
        }

        /// <summary>
        /// Returns 0 at entry into the active tier and reaches 1 at the final
        /// representable raw value before promotion. meV begins at literal zero.
        /// TeV uses 1..1000 TeV as its presentation range and clamps above it.
        /// </summary>
        public static float GetTierProgress01(long rawMilliElectronVolts)
        {
            long raw = Math.Max(0L, rawMilliElectronVolts);
            EnergyUnit unit = GetUnit(raw);
            long factor = EnergyScoreMath.GetFactor(unit);

            long start = unit == EnergyUnit.MilliElectronVolt ? 0L : factor;
            long nextTierStart = EnergyScoreMath.SaturatingMultiply(factor, 1_000L);
            long endInclusive = nextTierStart == long.MaxValue
                ? long.MaxValue
                : Math.Max(start, nextTierStart - 1L);

            if (endInclusive <= start) return raw > start ? 1f : 0f;
            if (raw <= start) return 0f;
            if (raw >= endInclusive) return 1f;

            double t = (double)(raw - start) / (double)(endInclusive - start);
            return Mathf.Clamp01((float)t);
        }

        public static EnergyDisplayValue GetDisplayValue(long rawMilliElectronVolts)
        {
            long raw = Math.Max(0L, rawMilliElectronVolts);
            EnergyUnit unit = GetUnit(raw);
            long factor = EnergyScoreMath.GetFactor(unit);

            long whole = factor > 0L ? raw / factor : 0L;
            long remainder = factor > 0L ? raw % factor : 0L;

            // Exact thousandths, deliberately truncated so a display never rounds
            // up into the next tier before the authoritative integer total does.
            int thousandths = factor > 0L
                ? (int)((remainder * 1_000L) / factor)
                : 0;

            bool topOverflow = unit == EnergyUnit.TeraElectronVolt && whole > 999L;
            if (topOverflow)
            {
                whole = 999L;
                thousandths = 999;
            }

            return new EnergyDisplayValue
            {
                unit = unit,
                unitLabel = EnergyScoreMath.GetLabel(unit),
                whole = whole,
                fractionalThousandths = thousandths,
                exceedsFixedTopTierDisplay = topOverflow,
                tierProgress01 = GetTierProgress01(raw)
            };
        }

        public static string FormatValue(
            long rawMilliElectronVolts,
            int integerDigits = DefaultIntegerDigits,
            int fractionDigits = DefaultFractionDigits,
            bool spaceCharacters = false)
        {
            EnergyDisplayValue display = GetDisplayValue(rawMilliElectronVolts);
            integerDigits = Mathf.Max(1, integerDigits);
            fractionDigits = Mathf.Clamp(fractionDigits, 0, 3);

            string whole = display.whole.ToString(new string('0', integerDigits), CultureInfo.InvariantCulture);
            string value;

            if (fractionDigits <= 0)
            {
                value = whole;
            }
            else
            {
                string fractional3 = display.fractionalThousandths.ToString("000", CultureInfo.InvariantCulture);
                string fractional = fractional3.Substring(0, fractionDigits);
                value = whole + "." + fractional;
            }

            if (display.exceedsFixedTopTierDisplay)
                value += "+";

            return spaceCharacters ? InsertCharacterSpacing(value) : value;
        }

        public static string FormatWithUnit(
            long rawMilliElectronVolts,
            int integerDigits = DefaultIntegerDigits,
            int fractionDigits = DefaultFractionDigits,
            bool spaceCharacters = false)
        {
            EnergyDisplayValue display = GetDisplayValue(rawMilliElectronVolts);
            return $"{FormatValue(rawMilliElectronVolts, integerDigits, fractionDigits, spaceCharacters)} {display.unitLabel}";
        }

        public static string InsertCharacterSpacing(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            var chars = value.ToCharArray();
            return string.Join(" ", chars);
        }
    }
}
