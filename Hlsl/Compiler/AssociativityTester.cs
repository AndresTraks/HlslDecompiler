namespace HlslDecompiler.Hlsl
{
    public static class AssociativityTester
    {
        public static bool TestForMultiplication(HlslTreeNode node)
        {
            switch (node)
            {
                case AddOperation _:
                case SubtractOperation _:
                    return false;
                default:
                    return true;
            }
        }
    }
}
