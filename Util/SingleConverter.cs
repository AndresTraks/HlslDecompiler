using System;
using System.Globalization;

// Based on https://stackoverflow.com/a/1584328/1778371

namespace HlslDecompiler.Util;

/// <summary>Converts floats to their exact decimal values.</summary>
public class SingleConverter
{
    public static string ToExactString(float s)
    {
        if (double.IsPositiveInfinity(s))
            return "+Infinity";
        if (double.IsNegativeInfinity(s))
            return "-Infinity";
        if (double.IsNaN(s))
            return "NaN";

        int bits = BitConverter.SingleToInt32Bits(s);
        bool negative = (bits < 0);
        int exponent = (int)((bits >> 23) & 0xffL);
        long mantissa = bits & 0x7fffffL;

        // Subnormals: exponent one higher, no implicit leading bit.
        if (exponent == 0)
        {
            exponent++;
        }
        else
        {
            mantissa |= (1L << 23);
        }

        // Bias 127, plus 23 for treating the mantissa as an integer.
        exponent -= 150;

        if (mantissa == 0)
        {
            return "0";
        }

        while ((mantissa & 1) == 0)
        {
            mantissa >>= 1;
            exponent++;
        }

        ArbitraryDecimal ad = new ArbitraryDecimal(mantissa);

        // Dividing by 2 is multiplying by 5 and shifting one decimal place.
        if (exponent < 0)
        {
            for (int i = 0; i < -exponent; i++)
                ad.MultiplyBy(5);
            ad.Shift(-exponent);
        }
        else
        {
            for (int i = 0; i < exponent; i++)
                ad.MultiplyBy(2);
        }

        if (negative)
        {
            return "-" + ad.ToString();
        }
        return ad.ToString();
    }

    class ArbitraryDecimal
    {
        // One byte per digit.
        byte[] digits;
        // How many digits are after the decimal point.
        int decimalPoint = 0;

        // x must not be negative.
        internal ArbitraryDecimal(long x)
        {
            string tmp = x.ToString(CultureInfo.InvariantCulture);
            digits = new byte[tmp.Length];
            for (int i = 0; i < tmp.Length; i++)
                digits[i] = (byte)(tmp[i] - '0');
            Normalize();
        }

        // amount must be 2 or 5.
        internal void MultiplyBy(int amount)
        {
            byte[] result = new byte[digits.Length + 1];
            for (int i = digits.Length - 1; i >= 0; i--)
            {
                int resultDigit = digits[i] * amount + result[i + 1];
                result[i] = (byte)(resultDigit / 10);
                result[i + 1] = (byte)(resultDigit % 10);
            }
            if (result[0] != 0)
            {
                digits = result;
            }
            else
            {
                Array.Copy(result, 1, digits, 0, digits.Length);
            }
            Normalize();
        }

        // A positive amount makes the value smaller.
        internal void Shift(int amount)
        {
            decimalPoint += amount;
        }

        internal void Normalize()
        {
            int first;
            for (first = 0; first < digits.Length; first++)
                if (digits[first] != 0)
                    break;
            int last;
            for (last = digits.Length - 1; last >= 0; last--)
                if (digits[last] != 0)
                    break;

            if (first == 0 && last == digits.Length - 1)
                return;

            byte[] tmp = new byte[last - first + 1];
            for (int i = 0; i < tmp.Length; i++)
                tmp[i] = digits[i + first];

            decimalPoint -= digits.Length - (last + 1);
            digits = tmp;
        }

        public override string ToString()
        {
            char[] digitString = new char[digits.Length];
            for (int i = 0; i < digits.Length; i++)
                digitString[i] = (char)(digits[i] + '0');

            // 35
            if (decimalPoint == 0)
            {
                return new string(digitString);
            }

            // 350
            if (decimalPoint < 0)
            {
                return new string(digitString) +
                       new string('0', -decimalPoint);
            }

            // 0.035
            if (decimalPoint >= digitString.Length)
            {
                return "0." +
                    new string('0', decimalPoint - digitString.Length) +
                    new string(digitString);
            }

            // 3.5
            return new string(digitString, 0,
                               digitString.Length - decimalPoint) +
                "." +
                new string(digitString,
                            digitString.Length - decimalPoint,
                            decimalPoint);
        }
    }
}
