using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl.Compiler
{
    public sealed class NodeCompiler
    {
        private RegisterState _registers;

        public NodeCompiler(RegisterState registers)
        {
            _registers = registers;
        }

        public string Compile(IEnumerable<HlslTreeNode> group)
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

        private static string GetAstSourceSwizzleName(IEnumerable<IHasComponentIndex> inputs, int registerSize)
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
    }
}
