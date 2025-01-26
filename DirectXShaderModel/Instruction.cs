namespace HlslDecompiler.DirectXShaderModel
{
    public abstract class Instruction
    {

        public abstract bool HasDestination { get; }

        public abstract bool IsTextureOperation { get; }

        public abstract float GetParamSingle(int index);
        
        public abstract float GetParamInt(int index);

        public abstract RegisterKey GetParamRegisterKey(int index);

        public abstract int GetParamRegisterNumber(int index);

        public abstract string GetParamRegisterName(int index);

        public abstract int GetDestinationParamIndex();

        public abstract int GetDestinationWriteMask();

        public string GetDestinationWriteMaskName(int destinationLength)
        {
            int destinationMask = (1 << destinationLength) - 1;
            int writeMask = GetDestinationWriteMask() & destinationMask;

            if (writeMask == destinationMask)
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

        public abstract string GetSourceSwizzleName(int srcIndex, int? destinationLength);

        public abstract string GetDeclSemantic();

        public abstract int GetDestinationSemanticSize();
    }
}
