using System;
using System.Globalization;

namespace HlslDecompiler.Util;

public class ConstantFormatter
{
    private static readonly CultureInfo _culture = CultureInfo.InvariantCulture;

    public static string Format(double value)
    {
        if (double.IsNaN(value))
        {
            return "NaN";
        }
        if (double.IsInfinity(value))
        {
            return double.IsPositive(value) ? "INF" : "-INF";
        }
        // fxc parses a source literal as a float and widens it afterwards, so the
        // doubles it puts in a d() immediate are float values kept in a double, and
        // read best as one: 0.1 printed as the 0.10000000000000001 a float cannot
        // hold recompiles to a different number than the bytecode held.
        float asFloat = (float)value;
        if (BitConverter.DoubleToInt64Bits(asFloat) == BitConverter.DoubleToInt64Bits(value))
        {
            return Format(asFloat);
        }
        // Not a float's: say everything, as little as round-trips it.
        return value.ToString("R", _culture);
    }

    public static string Format(float value)
    {
        // A decimal holds no NaN and no infinity, and parsing their names threw.
        // The bits of the integer -1 are a NaN as a float, so an immediate of -1
        // read as one was enough to bring the assembly writer down. fxc prints
        // these the way it prints any other float that is not a number.
        if (float.IsNaN(value))
        {
            return "NaN";
        }
        if (float.IsInfinity(value))
        {
            return float.IsPositive(value) ? "INF" : "-INF";
        }
        // A decimal holds about 29 digits, and a float reaches past the top of
        // one: parsing l(1e30) threw. Past the end the exact digits are printed
        // whole, and that asks for no rounding of its own - a float above 2^23
        // has no fractional part to round, and every float a decimal cannot hold
        // is far above it. A whole number prints the same as the whole numbers a
        // decimal can hold, which are already printed exact below.
        string exact = SingleConverter.ToExactString(value);
        int dot = exact.IndexOf('.');
        if (dot < 0)
        {
            // fxc reads a bare decimal literal as an integer, and its integer is
            // 64 bits wide - a float's whole part outgrows that well before the
            // float does. With a dot the same digits are read as the float they
            // are, the way fxc itself prints one.
            if (exact.Length - (exact[0] == '-' ? 1 : 0) >= 20)
            {
                return exact + ".0";
            }
            return exact;
        }
        decimal exactValue = decimal.Parse(exact, _culture);
        return Round(exactValue).ToString(_culture);
    }

    // To match the behavor of FXC:
    // retain 9 non-zero digits for numbers < 1
    // retain 10 non-zero digits for numbers > 1
    private static decimal Round(decimal value)
    {
        string valueString = value.ToString(_culture);
        valueString = valueString.TrimStart('-');

        int firstSignificantDigitIndex = -1;
        int dotIndex = -1;
        for (int i = 0; i < valueString.Length; i++)
        {
            if (valueString[i] == '.')
            {
                dotIndex = i;
            }
            else if (firstSignificantDigitIndex == -1 && valueString[i] != '0')
            {
                firstSignificantDigitIndex = i;
            }
        }

        if (dotIndex == -1)
        {
            return value;
        }

        int precision = firstSignificantDigitIndex != 0 ? 9 : 10;
        int decimals = precision + (firstSignificantDigitIndex - dotIndex - 1);
        return decimal.Round(value, decimals, MidpointRounding.AwayFromZero);
    }
}
