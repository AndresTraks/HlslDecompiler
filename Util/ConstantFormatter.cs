using System;
using System.Globalization;

namespace HlslDecompiler.Util;

public class ConstantFormatter
{
    private static readonly CultureInfo _culture = CultureInfo.InvariantCulture;

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
        decimal exactValue = decimal.Parse(SingleConverter.ToExactString(value), _culture);
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
