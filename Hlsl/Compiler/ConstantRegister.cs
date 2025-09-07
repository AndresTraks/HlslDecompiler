namespace HlslDecompiler.Hlsl
{
    public class ConstantRegister
    {
        public int RegisterIndex { get; private set; }
        public float[] Value { get; private set; }

        public ConstantRegister(int registerIndex, float value0, float value1, float value2, float value3)
        {
            RegisterIndex = registerIndex;
            Value = [value0, value1, value2, value3];
        }

        public float this[int index]
        {
            get
            {
                return Value[index];
            }
            set
            {
                Value[index] = value;
            }
        }
    }
}
