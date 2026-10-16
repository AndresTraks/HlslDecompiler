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
        // fxc parses a literal as a float and widens it, so print a double that is
        // exactly a float as that float.
        float asFloat = (float)value;
        if (BitConverter.DoubleToInt64Bits(asFloat) == BitConverter.DoubleToInt64Bits(value))
        {
            return Format(asFloat);
        }
        return value.ToString("R", _culture);
    }

    public static string Format(float value)
    {
        // decimal cannot hold NaN or infinity.
        if (float.IsNaN(value))
        {
            return "NaN";
        }
        if (float.IsInfinity(value))
        {
            return float.IsPositive(value) ? "INF" : "-INF";
        }
        // Whole numbers print exact without a decimal (which overflows past ~1e28).
        string exact = SingleConverter.ToExactString(value);
        int dot = exact.IndexOf('.');
        if (dot < 0)
        {
            // A bare literal from 2^32 is a syntax error to fxc, and from 2^31 a uint;
            // a dot keeps it a float.
            if (Math.Abs(value) >= 2147483648f)
            {
                return exact + ".0";
            }
            return exact;
        }
        decimal exactValue = decimal.Parse(exact, _culture);
        return Round(exactValue).ToString(_culture);
    }

    // As fxc: 9 significant digits below 1, 10 above.
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
