using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl.Compiler
{
    public sealed class NodeCompiler
    {
        private readonly RegisterState _registers;
        private readonly NodeGrouper _nodeGrouper;
        private readonly ConstantCompiler _constantCompiler;

        public NodeCompiler(RegisterState registers)
        {
            _registers = registers;
            _nodeGrouper = new NodeGrouper(registers);
            _constantCompiler = new ConstantCompiler(_nodeGrouper);
        }

        public string Compile(IEnumerable<HlslTreeNode> group)
        {
            return Compile(group.ToList());
        }

        public string Compile(List<HlslTreeNode> components)
        {
            if (components.Count == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(components));
            }

            if (components.Count == 1)
            {
                DotProductContext dotProduct = _nodeGrouper.DotProductGrouper.TryGetDotProductGroup(components[0]);
                if (dotProduct != null)
                {
                    string value1 = Compile(dotProduct.Value1);
                    string value2 = Compile(dotProduct.Value2);
                    return $"dot({value1}, {value2})";
                }
            }
            else
            {
                IList<IList<HlslTreeNode>> componentGroups = _nodeGrouper.GroupComponents(components);
                if (componentGroups.Count > 1)
                {
                    // In float4(x, float), x cannot be promoted from float to float3
                    // In float4(x, y), x cannot be promoted to float2 and y to float2
                    // float4(float2, float2) is allowed
                    var constructorParts = componentGroups.Select(Compile);
                    return $"float{components.Count}({string.Join(", ", constructorParts)})";
                }

                var multiplication = _nodeGrouper.MatrixMultiplicationGrouper.TryGetMultiplicationGroup(components);
                if (multiplication != null)
                {
                    string matrixName = multiplication.MatrixDeclaration.Name;
                    string vector = Compile(multiplication.Vector);
                    return multiplication.IsMatrixByVector
                        ? $"mul({matrixName}, {vector})"
                        : $"mul({vector}, {matrixName})";
                }
            }

            var first = components[0];

            if (first is ConstantNode constant)
            {
                var constantComponents = components.Cast<ConstantNode>().ToArray();
                return _constantCompiler.Compile(constantComponents);
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
                            string value = Compile(components.Select(g => g.Children[0]));
                            return $"{name}({value})";
                        }

                    case AddOperation _:
                        {
                            return string.Format("{0} + {1}",
                                Compile(components.Select(g => g.Children[0])),
                                Compile(components.Select(g => g.Children[1])));
                        }

                    case SubtractOperation _:
                        {
                            return string.Format("{0} - {1}",
                                Compile(components.Select(g => g.Children[0])),
                                Compile(components.Select(g => g.Children[1])));
                        }

                    case MultiplyOperation _:
                        {
                            var multiplicand1 = components.Select(g => g.Children[0]);
                            var multiplicand2 = components.Select(g => g.Children[1]);

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
                            var value1 = Compile(components.Select(g => g.Children[0]));
                            var value2 = Compile(components.Select(g => g.Children[1]));

                            var name = operation.Mnemonic;

                            return $"{name}({value1}, {value2})";
                        }

                    case LinearInterpolateOperation _:
                        {
                            var value1 = Compile(components.Select(g => g.Children[0]));
                            var value2 = Compile(components.Select(g => g.Children[1]));
                            var value3 = Compile(components.Select(g => g.Children[2]));

                            var name = "lerp";

                            return $"{name}({value1}, {value2}, {value3})";
                        }

                    case CompareOperation _:
                        {
                            var value1 = Compile(components.Select(g => g.Children[0]));
                            var value2 = Compile(components.Select(g => g.Children[1]));
                            var value3 = Compile(components.Select(g => g.Children[2]));

                            return $"{value1} >= 0 ? {value2} : {value3}";
                        }
                }
            }


            if (first is IHasComponentIndex component)
            {
                var componentsWithIndices = components.Cast<IHasComponentIndex>();

                if (first is RegisterInputNode shaderInput)
                {
                    var registerKey = shaderInput.RegisterComponentKey.RegisterKey;

                    string swizzle = "";
                    if (registerKey.Type != RegisterType.Sampler)
                    {
                        swizzle = GetAstSourceSwizzleName(componentsWithIndices, _registers.GetRegisterFullLength(registerKey));
                    }

                    string name = _registers.GetRegisterName(registerKey);
                    return $"{name}{swizzle}";
                }

                if (first is TextureLoadOutputNode textureLoad)
                {
                    string swizzle = GetAstSourceSwizzleName(componentsWithIndices, 4);

                    string sampler = Compile(new[] { textureLoad.SamplerInput });
                    string texcoords = Compile(textureLoad.TextureCoordinateInputs);
                    return $"tex2D({sampler}, {texcoords}){swizzle}";
                }

                if (first is NormalizeOutputNode)
                {
                    string input = Compile(first.Children);
                    string swizzle = GetAstSourceSwizzleName(componentsWithIndices, 4);
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
