using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.DirectXShaderModel;

public class ShaderTypeInfo
{
    public ParameterClass ParameterClass { get; }
    public ParameterType ParameterType { get; }
    public int Rows { get; }
    public int Columns { get; }
    public int NumElements { get; }
    public IList<ShaderStructMemberInfo> MemberInfo { get; }

    /// <summary>The SM5 type name of an interface or struct; null elsewhere.</summary>
    public string Name { get; init; }

    public ShaderTypeInfo(ParameterClass parameterClass, ParameterType parameterType, int rows, int columns, int numElements, IList<ShaderStructMemberInfo> memberInfo)
    {
        ParameterClass = parameterClass;
        ParameterType = parameterType;
        Rows = rows;
        Columns = columns;
        NumElements = numElements;
        MemberInfo = memberInfo;
    }

    /// <summary>
    /// The registers a constant table default value occupies: each array element,
    /// struct member and matrix row or column starts at a register boundary, with
    /// up to four components in each register.
    /// </summary>
    public int DefaultValueRegisterCount
    {
        get
        {
            int registers = ParameterClass switch
            {
                ParameterClass.Scalar or ParameterClass.Vector => 1,
                ParameterClass.MatrixColumns => Columns * ((Rows + 3) / 4),
                ParameterClass.MatrixRows => Rows * ((Columns + 3) / 4),
                ParameterClass.Struct => MemberInfo?.Sum(m => m.TypeInfo.DefaultValueRegisterCount) ?? 0,
                _ => 0,
            };
            return registers * Math.Max(NumElements, 1);
        }
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
