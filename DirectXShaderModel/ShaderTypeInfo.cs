using System.Collections.Generic;

namespace HlslDecompiler.DirectXShaderModel
{
    public class ShaderTypeInfo
    {
        public ParameterClass ParameterClass { get; }
        public ParameterType ParameterType { get; }
        public int Rows { get; }
        public int Columns { get; }
        public int NumElements { get; }
        public IList<ShaderStructMemberInfo> MemberInfo { get; }

        public ShaderTypeInfo(ParameterClass parameterClass, ParameterType parameterType, int rows, int columns, int numElements, IList<ShaderStructMemberInfo> memberInfo)
        {
            ParameterClass = parameterClass;
            ParameterType = parameterType;
            Rows = rows;
            Columns = columns;
            NumElements = numElements;
            MemberInfo = memberInfo;
        }

        public override string ToString()
        {
            return ParameterClass.ToString();
        }

        public override bool Equals(object obj)
        {
            if (obj is not ShaderTypeInfo info)
            {
                return false;
            }
            // Exclude NumElements
            if (!ParameterClass.Equals(info.ParameterClass)
                || !ParameterType.Equals(info.ParameterType)
                || !Rows.Equals(info.Rows)
                || !Columns.Equals(info.Columns)) {
                return false;
            }
            if (MemberInfo != null)
            {
                if (MemberInfo.Count != info.MemberInfo.Count)
                {
                    return false;
                }
                for (int i = 0; i < MemberInfo.Count; i++)
                {
                    if (!MemberInfo[i].Equals(info.MemberInfo[i]))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public override int GetHashCode()
        {
            // Exclude NumElements
            int hashCode = ParameterClass.GetHashCode() 
                ^ ParameterType.GetHashCode()
                ^ Rows.GetHashCode()
                ^ Columns.GetHashCode();
            if (MemberInfo != null)
            {
                foreach (var member in MemberInfo)
                {
                    hashCode ^= member.GetHashCode();
                }
            }
            return hashCode;
        }
    }
}
