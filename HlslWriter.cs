﻿using HlslDecompiler.Hlsl;
using HlslDecompiler.Hlsl.Compiler;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HlslDecompiler
{
    public class HlslWriter
    {
        private readonly ShaderModel _shader;
        private readonly bool _doAstAnalysis;

        FileStream hlslFile;
        StreamWriter hlslWriter;
        string indent = "";

        public RegisterState _registers;

        public HlslWriter(ShaderModel shader, bool doAstAnalysis = false)
        {
            _shader = shader;
            _doAstAnalysis = doAstAnalysis;
        }

        void WriteLine()
        {
            hlslWriter.WriteLine();
        }

        void WriteLine(string value)
        {
            hlslWriter.Write(indent);
            hlslWriter.WriteLine(value);
        }

        void WriteLine(string format, params object[] args)
        {
            hlslWriter.Write(indent);
            hlslWriter.WriteLine(format, args);
        }

        private string GetDestinationName(Instruction instruction)
        {
            return _registers.GetDestinationName(instruction);
        }

        private string GetSourceName(Instruction instruction, int srcIndex)
        {
            return _registers.GetSourceName(instruction, srcIndex);
        }

        private static string GetConstantTypeName(ConstantDeclaration declaration)
        {
            switch (declaration.ParameterClass)
            {
                case ParameterClass.Scalar:
                    return declaration.ParameterType.ToString().ToLower();
                case ParameterClass.Vector:
                    if (declaration.ParameterType == ParameterType.Float)
                    {
                        return "float" + declaration.Columns;
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                case ParameterClass.MatrixColumns:
                case ParameterClass.MatrixRows:
                    if (declaration.ParameterType == ParameterType.Float)
                    {
                        return $"float{declaration.Rows}x{declaration.Columns}";
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                case ParameterClass.Object:
                    switch (declaration.ParameterType)
                    {
                        case ParameterType.Sampler2D:
                        case ParameterType.Sampler3D:
                            return "sampler";
                        default:
                            throw new NotImplementedException();
                    }
            }
            throw new NotImplementedException();
        }

        private void WriteInstruction(Instruction instruction)
        {
            switch (instruction.Opcode)
            {
                case Opcode.Abs:
                    WriteLine("{0} = abs({1});", GetDestinationName(instruction),
                        GetSourceName(instruction, 1));
                    break;
                case Opcode.Add:
                    WriteLine("{0} = {1} + {2};", GetDestinationName(instruction),
                        GetSourceName(instruction, 1), GetSourceName(instruction, 2));
                    break;
                case Opcode.Cmp:
                    // TODO: should be per-component
                    WriteLine("{0} = ({1} >= 0) ? {2} : {3};", GetDestinationName(instruction),
                        GetSourceName(instruction, 1), GetSourceName(instruction, 2), GetSourceName(instruction, 3));
                    break;
                case Opcode.DP2Add:
                    WriteLine("{0} = dot({1}, {2}) + {3};", GetDestinationName(instruction),
                        GetSourceName(instruction, 1), GetSourceName(instruction, 2), GetSourceName(instruction, 3));
                    break;
                case Opcode.Dp3:
                    WriteLine("{0} = dot({1}, {2});", GetDestinationName(instruction),
                        GetSourceName(instruction, 1), GetSourceName(instruction, 2));
                    break;
                case Opcode.Dp4:
                    WriteLine("{0} = dot({1}, {2});", GetDestinationName(instruction),
                        GetSourceName(instruction, 1), GetSourceName(instruction, 2));
                    break;
                case Opcode.Else:
                    indent = indent.Substring(0, indent.Length - 1);
                    WriteLine("} else {");
                    indent += "\t";
                    break;
                case Opcode.Endif:
                    indent = indent.Substring(0, indent.Length - 1);
                    WriteLine("}");
                    break;
                case Opcode.Exp:
                    WriteLine("{0} = exp2({1});", GetDestinationName(instruction), GetSourceName(instruction, 1));
                    break;
                case Opcode.Frc:
                    WriteLine("{0} = frac({1});", GetDestinationName(instruction), GetSourceName(instruction, 1));
                    break;
                case Opcode.If:
                    WriteLine("if ({0}) {{", GetSourceName(instruction, 0));
                    indent += "\t";
                    break;
                case Opcode.IfC:
                    if ((IfComparison)instruction.Modifier == IfComparison.GE &&
                        instruction.GetSourceModifier(0) == SourceModifier.AbsAndNegate &&
                        instruction.GetSourceModifier(1) == SourceModifier.Abs &&
                        instruction.GetParamRegisterName(0) + instruction.GetSourceSwizzleName(0) ==
                        instruction.GetParamRegisterName(1) + instruction.GetSourceSwizzleName(1))
                    {
                        WriteLine("if ({0} == 0) {{", instruction.GetParamRegisterName(0) + instruction.GetSourceSwizzleName(0));
                    }
                    else if ((IfComparison)instruction.Modifier == IfComparison.LT &&
                        instruction.GetSourceModifier(0) == SourceModifier.AbsAndNegate &&
                        instruction.GetSourceModifier(1) == SourceModifier.Abs &&
                        instruction.GetParamRegisterName(0) + instruction.GetSourceSwizzleName(0) ==
                        instruction.GetParamRegisterName(1) + instruction.GetSourceSwizzleName(1))
                    {
                        WriteLine("if ({0} != 0) {{", instruction.GetParamRegisterName(0) + instruction.GetSourceSwizzleName(0));
                    }
                    else
                    {
                        string ifComparison;
                        switch ((IfComparison)instruction.Modifier)
                        {
                            case IfComparison.GT:
                                ifComparison = ">";
                                break;
                            case IfComparison.EQ:
                                ifComparison = "==";
                                break;
                            case IfComparison.GE:
                                ifComparison = ">=";
                                break;
                            case IfComparison.LE:
                                ifComparison = "<=";
                                break;
                            case IfComparison.NE:
                                ifComparison = "!=";
                                break;
                            case IfComparison.LT:
                                ifComparison = "<";
                                break;
                            default:
                                throw new InvalidOperationException();
                        }
                        WriteLine("if ({0} {2} {1}) {{", GetSourceName(instruction, 0), GetSourceName(instruction, 1), ifComparison);
                    }
                    indent += "\t";
                    break;
                case Opcode.Log:
                    WriteLine("{0} = log2({1});", GetDestinationName(instruction), GetSourceName(instruction, 1));
                    break;
                case Opcode.Lrp:
                    WriteLine("{0} = lerp({2}, {3}, {1});", GetDestinationName(instruction),
                        GetSourceName(instruction, 1), GetSourceName(instruction, 2), GetSourceName(instruction, 3));
                    break;
                case Opcode.Mad:
                    WriteLine("{0} = {1} * {2} + {3};", GetDestinationName(instruction),
                        GetSourceName(instruction, 1), GetSourceName(instruction, 2), GetSourceName(instruction, 3));
                    break;
                case Opcode.Max:
                    WriteLine("{0} = max({1}, {2});", GetDestinationName(instruction),
                        GetSourceName(instruction, 1), GetSourceName(instruction, 2));
                    break;
                case Opcode.Min:
                    WriteLine("{0} = min({1}, {2});", GetDestinationName(instruction),
                        GetSourceName(instruction, 1), GetSourceName(instruction, 2));
                    break;
                case Opcode.Mov:
                    WriteLine("{0} = {1};", GetDestinationName(instruction), GetSourceName(instruction, 1));
                    break;
                case Opcode.MovA:
                    WriteLine("{0} = {1};", GetDestinationName(instruction), GetSourceName(instruction, 1));
                    break;
                case Opcode.Mul:
                    WriteLine("{0} = {1} * {2};", GetDestinationName(instruction),
                        GetSourceName(instruction, 1), GetSourceName(instruction, 2));
                    break;
                case Opcode.Nrm:
                    WriteLine("{0} = normalize({1});", GetDestinationName(instruction), GetSourceName(instruction, 1));
                    break;
                case Opcode.Pow:
                    WriteLine("{0} = pow({1}, {2});", GetDestinationName(instruction),
                        GetSourceName(instruction, 1), GetSourceName(instruction, 2));
                    break;
                case Opcode.Rcp:
                    WriteLine("{0} = 1 / {1};", GetDestinationName(instruction), GetSourceName(instruction, 1));
                    break;
                case Opcode.Rsq:
                    WriteLine("{0} = 1 / sqrt({1});", GetDestinationName(instruction), GetSourceName(instruction, 1));
                    break;
                case Opcode.Sge:
                    if (instruction.GetSourceModifier(1) == SourceModifier.AbsAndNegate &&
                        instruction.GetSourceModifier(2) == SourceModifier.Abs &&
                        instruction.GetParamRegisterName(1) + instruction.GetSourceSwizzleName(1) ==
                        instruction.GetParamRegisterName(2) + instruction.GetSourceSwizzleName(2))
                    {
                        WriteLine("{0} = ({1} == 0) ? 1 : 0;", GetDestinationName(instruction),
                            instruction.GetParamRegisterName(1) + instruction.GetSourceSwizzleName(1));
                    }
                    else
                    {
                        WriteLine("{0} = ({1} >= {2}) ? 1 : 0;", GetDestinationName(instruction), GetSourceName(instruction, 1),
                            GetSourceName(instruction, 2));
                    }
                    break;
                case Opcode.Slt:
                    WriteLine("{0} = ({1} < {2}) ? 1 : 0;", GetDestinationName(instruction), GetSourceName(instruction, 1),
                        GetSourceName(instruction, 2));
                    break;
                case Opcode.SinCos:
                    WriteLine("sincos({1}, {0}, {0});", GetDestinationName(instruction), GetSourceName(instruction, 1));
                    break;
                case Opcode.Sub:
                    WriteLine("{0} = {1} - {2};", GetDestinationName(instruction),
                        GetSourceName(instruction, 1), GetSourceName(instruction, 2));
                    break;
                case Opcode.Tex:
                    if ((_shader.MajorVersion == 1 && _shader.MinorVersion >= 4) || (_shader.MajorVersion > 1))
                    {
                        WriteLine("{0} = tex2D({2}, {1});", GetDestinationName(instruction),
                            GetSourceName(instruction, 1), GetSourceName(instruction, 2));
                    }
                    else
                    {
                        WriteLine("{0} = tex2D();", GetDestinationName(instruction));
                    }
                    break;
                case Opcode.TexLDL:
                    WriteLine("{0} = tex2Dlod({2}, {1});", GetDestinationName(instruction),
                        GetSourceName(instruction, 1), GetSourceName(instruction, 2));
                    break;
                case Opcode.Comment:
                case Opcode.End:
                    break;
            }
        }

        public void Write(string hlslFilename)
        {
            hlslFile = new FileStream(hlslFilename, FileMode.Create, FileAccess.Write);
            hlslWriter = new StreamWriter(hlslFile);

            ParseRegisterDeclarations();

            WriteConstantDeclarations();

            if (_registers.MethodInputRegisters.Count > 1)
            {
                WriteInputStructureDeclaration();
            }

            if (_registers.MethodOutputRegisters.Count > 1)
            {
                WriteOutputStructureDeclaration();
            }

            string methodReturnType = GetMethodReturnType();
            string methodParameters = GetMethodParameters();
            string methodSemantic = GetMethodSemantic();
            WriteLine("{0} main({1}){2}", methodReturnType, methodParameters, methodSemantic);
            WriteLine("{");
            indent = "\t";

            if (_registers.MethodOutputRegisters.Count > 1)
            {
                var outputStructType = _shader.Type == ShaderType.Pixel ? "PS_OUT" : "VS_OUT";
                WriteLine($"{outputStructType} o;");
                WriteLine();
            }

            HlslAst ast = null;
            if (_doAstAnalysis)
            {
                var parser = new BytecodeParser();
                ast = parser.Parse(_shader);
                ast.ReduceTree();
            }
            if (ast != null)
            {
                WriteAst(ast);
            }
            else
            {
                WriteLine("{0} o;", methodReturnType);
                WriteLine();

                // Find all assignments to temporary variables
                // and declare the variables.
                var tempRegisters = new Dictionary<string, int>();
                foreach (Instruction instruction in _shader.Instructions)
                {
                    if (!instruction.HasDestination)
                    {
                        continue;
                    }

                    int destIndex = instruction.GetDestinationParamIndex();
                    if (instruction.GetParamRegisterType(destIndex) == RegisterType.Temp)
                    {
                        string registerName = instruction.GetParamRegisterName(destIndex);
                        if (!tempRegisters.ContainsKey(registerName))
                        {
                            tempRegisters.Add(registerName, 0);
                        }
                        tempRegisters[registerName] |= instruction.GetDestinationWriteMask();
                    }
                }

                foreach (var registerName in tempRegisters.Keys)
                {
                    int writeMask = tempRegisters[registerName];
                    string writeMaskName;
                    switch (writeMask)
                    {
                        case 0x1:
                            writeMaskName = "float";
                            break;
                        case 0x3:
                            writeMaskName = "float2";
                            break;
                        case 0x7:
                            writeMaskName = "float3";
                            break;
                        case 0xF:
                            writeMaskName = "float4";
                            break;
                        default:
                            // TODO
                            writeMaskName = "float4";
                            break;
                            //throw new NotImplementedException();
                    }
                    WriteLine("{0} {1};", writeMaskName, registerName);
                }

                foreach (Instruction instruction in _shader.Instructions)
                {
                    WriteInstruction(instruction);
                }

                WriteLine();
                WriteLine("return o;");
            }
            indent = "";
            WriteLine("}");

            hlslWriter.Dispose();
            hlslFile.Dispose();
        }

        private void ParseRegisterDeclarations()
        {
            _registers = new RegisterState(_shader);
        }

        private void WriteConstantDeclarations()
        {
            if (_registers.ConstantDeclarations.Count != 0)
            {
                foreach (ConstantDeclaration declaration in _registers.ConstantDeclarations)
                {
                    string typeName = GetConstantTypeName(declaration);
                    WriteLine("{0} {1};", typeName, declaration.Name);
                }

                WriteLine();
            }
        }

        private void WriteInputStructureDeclaration()
        {
            var inputStructType = _shader.Type == ShaderType.Pixel ? "PS_IN" : "VS_IN";
            WriteLine($"struct {inputStructType}");
            WriteLine("{");
            indent = "\t";
            foreach (var input in _registers.MethodInputRegisters.Values)
            {
                WriteLine($"{input.TypeName} {input.Name} : {input.Semantic};");
            }
            indent = "";
            WriteLine("};");
            WriteLine();
        }

        private void WriteOutputStructureDeclaration()
        {
            var outputStructType = _shader.Type == ShaderType.Pixel ? "PS_OUT" : "VS_OUT";
            WriteLine($"struct {outputStructType}");
            WriteLine("{");
            indent = "\t";
            foreach (var output in _registers.MethodOutputRegisters.Values)
            {
                WriteLine($"{output.TypeName} {output.Name} : {output.Semantic};");
            }
            indent = "";
            WriteLine("};");
            WriteLine();
        }

        private string GetMethodReturnType()
        {
            switch (_registers.MethodOutputRegisters.Count)
            {
                case 0:
                    throw new InvalidOperationException();
                case 1:
                    return _registers.MethodOutputRegisters.Values.First().TypeName;
                default:
                    return _shader.Type == ShaderType.Pixel ? "PS_OUT" : "VS_OUT";
            }
        }

        private string GetMethodSemantic()
        {
            switch (_registers.MethodOutputRegisters.Count)
            {
                case 0:
                    throw new InvalidOperationException();
                case 1:
                    string semantic = _registers.MethodOutputRegisters.Values.First().Semantic;
                    return $" : {semantic}";
                default:
                    return string.Empty;
            }
        }

        private string GetMethodParameters()
        {
            if (_registers.MethodInputRegisters.Count == 0)
            {
                return string.Empty;
            }
            else if (_registers.MethodInputRegisters.Count == 1)
            {
                var input = _registers.MethodInputRegisters.Values.First();
                return $"{input.TypeName} {input.Name} : {input.Semantic}";
            }

            return _shader.Type == ShaderType.Pixel
                    ? "VS_OUT i"
                    : "VS_IN i";
        }

        private string GetAstSourceSwizzleName(IEnumerable<IHasComponentIndex> inputs, int registerSize)
        {
            string swizzleName = "";
            foreach (int swizzle in inputs.Select(i => i.ComponentIndex))
            {
                swizzleName += "xyzw"[swizzle];
            }

            if (swizzleName.Equals("xyzw".Substring(0, registerSize)))
            {
                return "";
            }

            if (swizzleName.Distinct().Count() == 1)
            {
                return "." + swizzleName.First();
            }

            return "." + swizzleName;
        }

        private void WriteAst(HlslAst ast)
        {
            var rootGroups = ast.Roots.GroupBy(r => r.Key.RegisterKey);

            foreach (var rootGroup in rootGroups)
            {
                var registerKey = rootGroup.Key;
                var roots = rootGroup.OrderBy(r => r.Key.ComponentIndex).Select(r => r.Value).ToList();
                string statement = Compile(roots);

                if (_registers.MethodOutputRegisters.Count == 1)
                {
                    WriteLine($"return {statement};");
                }
                else
                {
                    var name = _registers.MethodOutputRegisters[registerKey].Name;
                    WriteLine($"o.{name} = {statement};");
                }
            }

            if (_registers.MethodOutputRegisters.Count > 1)
            {
                WriteLine();
                WriteLine($"return o;");
            }
            
        }

        private string Compile(IEnumerable<HlslTreeNode> group)
        {
            List<HlslTreeNode> groupList = group.ToList();
            int componentCount = groupList.Count;

            var subGroups = NodeGrouper.GroupComponents(groupList);
            if (subGroups.Count == 0)
            {
                throw new InvalidOperationException();
            }

            if (subGroups.Count > 1)
            {

                // In float4(x, float), x cannot be promoted from float to float3
                // In float4(x, y), x cannot be promoted to float2 and y to float2
                // float4(float2, float2) is allowed
                var constructorParts = subGroups.Select(Compile);
                return $"float{componentCount}({string.Join(", ", constructorParts)})";
            }
            
            if (groupList.Count == 2 && NodeGrouper.IsMatrixMultiplication(groupList[0], groupList[1]))
            {
                return "mul(matrix_2x2, position.xy)";
            }
            
            var first = group.First();

            if (first is ConstantNode constant)
            {
                var components = group.Cast<ConstantNode>().ToArray();
                return ConstantCompiler.Compile(components);
            }

            if (first is Operation operation)
            {
                switch (operation)
                {
                    case AbsoluteOperation _:
                    case CosineOperation _:
                    case FractionalOperation _:
                    case NegateOperation _:
                    case ReciprocalOperation _:
                    case ReciprocalSquareRootOperation _:
                    case SignGreaterOrEqualOperation _:
                    case SignLessOperation _:
                        {
                            string name = operation.Mnemonic;
                            string value = Compile(group.Select(g => g.Children[0]));
                            return $"{name}({value})";
                        }

                    case AddOperation _:
                        {
                            return string.Format("{0} + {1}",
                                Compile(group.Select(g => g.Children[0])),
                                Compile(group.Select(g => g.Children[1])));
                        }

                    case SubtractOperation _:
                        {
                            return string.Format("{0} - {1}",
                                Compile(group.Select(g => g.Children[0])),
                                Compile(group.Select(g => g.Children[1])));
                        }

                    case MultiplyOperation _:
                        {
                            var multiplicand1 = group.Select(g => g.Children[0]);
                            var multiplicand2 = group.Select(g => g.Children[1]);

                            if (multiplicand2.First() is ConstantNode)
                            {
                                return string.Format("{0} * {1}",
                                    Compile(multiplicand2),
                                    Compile(multiplicand1));
                            }

                            return string.Format("{0} * {1}",
                                Compile(multiplicand1),
                                Compile(multiplicand2));
                        }

                    case MaximumOperation _:
                    case MinimumOperation _:
                    case PowerOperation _:
                        {
                            var value1 = Compile(group.Select(g => g.Children[0]));
                            var value2 = Compile(group.Select(g => g.Children[1]));

                            var name = operation.Mnemonic;

                            return $"{name}({value1}, {value2})";
                        }

                    case LinearInterpolateOperation _:
                        {
                            var value1 = Compile(group.Select(g => g.Children[0]));
                            var value2 = Compile(group.Select(g => g.Children[1]));
                            var value3 = Compile(group.Select(g => g.Children[2]));

                            var name = "lerp";

                            return $"{name}({value1}, {value2}, {value3})";
                        }

                    case CompareOperation _:
                        {
                            var value1 = Compile(group.Select(g => g.Children[0]));
                            var value2 = Compile(group.Select(g => g.Children[1]));
                            var value3 = Compile(group.Select(g => g.Children[2]));

                            return $"{value1} >= 0 ? {value2} : {value3}";
                        }
                }
            }


            if (first is IHasComponentIndex component)
            {
                var components = group.Cast<IHasComponentIndex>();

                if (first is RegisterInputNode shaderInput)
                {
                    var registerKey = shaderInput.RegisterComponentKey.RegisterKey;

                    string swizzle = "";
                    if (registerKey.Type != RegisterType.Sampler)
                    {
                        swizzle = GetAstSourceSwizzleName(components, _registers.GetRegisterFullLength(registerKey));
                    }

                    string name = _registers.GetRegisterName(registerKey);
                    return $"{name}{swizzle}";
                }

                if (first is TextureLoadOutputNode textureLoad)
                {
                    string swizzle = GetAstSourceSwizzleName(components, 4);

                    string sampler = Compile(new[] { textureLoad.SamplerInput });
                    string texcoords = Compile(textureLoad.TextureCoordinateInputs);
                    return $"tex2D({sampler}, {texcoords}){swizzle}";
                }

                if (first is DotProductOutputNode dotProductOutputNode)
                {
                    string input1 = Compile(dotProductOutputNode.Inputs1);
                    string input2 = Compile(dotProductOutputNode.Inputs2);
                    string swizzle = GetAstSourceSwizzleName(components, 4);
                    return $"dot({input1}, {input2}){swizzle}";
                }

                if (first is NormalizeOutputNode)
                {
                    string input = Compile(first.Children);
                    string swizzle = GetAstSourceSwizzleName(components, 4);
                    return $"normalize({input}){swizzle}";
                }

                throw new NotImplementedException();
            }

            throw new NotImplementedException();
        }
    }
}
