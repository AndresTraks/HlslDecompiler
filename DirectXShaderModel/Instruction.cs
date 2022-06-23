using System;

namespace HlslDecompiler.DirectXShaderModel
{
    enum IfComparison
    {
        None,
        GT,
        EQ,
        GE,
        LT,
        NE,
        LE
    }

    public abstract class Instruction
    {
        public int Modifier { get; set; }
        public bool Predicated { get; set; }

        public abstract bool HasDestination { get; }

        public abstract bool IsTextureOperation { get; }

        public abstract float GetParamSingle(int index);
        
        public abstract float GetParamInt(int index);

        public abstract RegisterKey GetParamRegisterKey(int index);

        public abstract int GetParamRegisterNumber(int index);

        public abstract string GetParamRegisterName(int index);

        public abstract int GetDestinationParamIndex();

        public abstract int GetDestinationWriteMask();

        public string GetDestinationWriteMaskName(int destinationLength, bool hlsl)
        {
            int writeMask = GetDestinationWriteMask();
            int writeMaskLength = GetDestinationMaskLength();

            if (!hlsl)
            {
                destinationLength = 4; // explicit mask in assembly
            }

            // Check if mask is the same length and of the form .xyzw
            if (writeMaskLength == destinationLength && writeMask == ((1 << writeMaskLength) - 1))
            {
                return "";
            }

            string writeMaskName =
                string.Format(".{0}{1}{2}{3}",
                ((writeMask & 1) != 0) ? "x" : "",
                ((writeMask & 2) != 0) ? "y" : "",
                ((writeMask & 4) != 0) ? "z" : "",
                ((writeMask & 8) != 0) ? "w" : "");
            return writeMaskName;
        }

        // Length of ".xy" = 2
        // Length of ".yw" = 4 (xyzw)
        public virtual int GetDestinationMaskedLength()
        {
            int writeMask = GetDestinationWriteMask();
            for (int i = 3; i >= 0; i--)
            {
                if ((writeMask & (1 << i)) != 0)
                {
                    return i + 1;
                }
            }
            return 0;
        }

        // Length of ".yw" = 2
        public int GetDestinationMaskLength()
        {
            int writeMask = GetDestinationWriteMask();
            int length = 0;
            for (int i = 0; i < 4; i++)
            {
                if ((writeMask & (1 << i)) != 0)
                {
                    length++;
                }
            }
            return length;
        }

        public abstract int GetSourceSwizzle(int srcIndex);

        public byte[] GetSourceSwizzleComponents(int srcIndex)
        {
            int swizzle = GetSourceSwizzle(srcIndex);
            byte[] swizzleArray = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                swizzleArray[i] = (byte)((swizzle >> (i * 2)) & 0x3);
            }
            return swizzleArray;
        }

        public abstract string GetSourceSwizzleName(int srcIndex);

        public abstract string GetDeclSemantic();
    }
}
