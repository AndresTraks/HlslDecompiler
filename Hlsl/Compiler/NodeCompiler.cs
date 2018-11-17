using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl
{
    public sealed class NodeCompiler
    {
        private readonly RegisterState _registers;
        private readonly NodeGrouper _nodeGrouper;
        private readonly ConstantCompiler _constantCompiler;
        private readonly MatrixMultiplicationCompiler _matrixMultiplicationCompiler;

        public const int PromoteToAnyVectorSize = -1;

        public NodeCompiler(RegisterState registers)
        {
            _registers = registers;
            _nodeGrouper = new NodeGrouper(registers);
            _constantCompiler = new ConstantCompiler(_nodeGrouper);
            _matrixMultiplicationCompiler = new MatrixMultiplicationCompiler(this);
        }

        public string Compile(IEnumerable<HlslTreeNode> group, int promoteToVectorSize = PromoteToAnyVectorSize)
        {
            return Compile(group.ToList(), promoteToVectorSize);
        }

        public string Compile(List<HlslTreeNode> components, int promoteToVectorSize = PromoteToAnyVectorSize)
        {
            if (components.Count == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(components));
            }

            if (components.Count == 1)
            {
                HlslTreeNode singleComponent = components[0];
                HlslTreeNode[] vector = _nodeGrouper.LengthGrouper.TryGetLengthContext(singleComponent);
                if (vector != null)
                {
                    string value = Compile(vector);
                    return $"length({value})";
                }

                DotProductContext dotProduct = _nodeGrouper.DotProductGrouper.TryGetDotProductGroup(singleComponent);
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
                    var constructorParts = componentGroups.Select(g => Compile(g));
                    return $"float{components.Count}({string.Join(", ", constructorParts)})";
                }

                var multiplication = _nodeGrouper.MatrixMultiplicationGrouper.TryGetMultiplicationGroup(components);
                if (multiplication != null)
                {
                    return _matrixMultiplicationCompiler.Compile(multiplication);
                }

                var normalize = _nodeGrouper.NormalizeGrouper.TryGetContext(components);
                if (normalize != null)
                {
                    var vector = Compile(normalize);
                    return $"normalize({vector})";
                }
            }

            var first = components[0];

            if (first is ConstantNode constant)
            {
                return CompileConstant(components);
            }

            if (first is Operation operation)
            {
                return CompileOperation(operation, components, promoteToVectorSize);
            }

            if (first is IHasComponentIndex component)
            {
                return CompileNodesWithComponents(components, first, promoteToVectorSize);
            }

            throw new NotImplementedException();
        }

        private string CompileConstant(List<HlslTreeNode> components)
        {
            var constantComponents = components.Cast<ConstantNode>().ToArray();
            return _constantCompiler.Compile(constantComponents);
        }

        private string CompileOperation(Operation operation, List<HlslTreeNode> components, int promoteToVectorSize)
        {
            switch (operation)
            {
                case NegateOperation _:
                    {
                        string name = operation.Mnemonic;
                        IEnumerable<HlslTreeNode> input = components.Select(g => g.Inputs[0]);
                        bool isAssociative = AssociativityTester.TestForMultiplication(input.First());
                        string value = Compile(input);
                        return isAssociative
                            ? $"-{value}"
                            : $"-({value})";
                    }

                case UnaryOperation _:
                case SignGreaterOrEqualOperation _:
                case SignLessOperation _:
                    {
                        string name = operation.Mnemonic;
                        string value = Compile(components.Select(g => g.Inputs[0]));
                        return $"{name}({value})";
                    }

                case AddOperation _:
                    {
                        return string.Format("{0} + {1}",
                            Compile(components.Select(g => g.Inputs[0])),
                            Compile(components.Select(g => g.Inputs[1])));
                    }

                case SubtractOperation _:
                    {
                        return string.Format("{0} - {1}",
                            Compile(components.Select(g => g.Inputs[0])),
                            Compile(components.Select(g => g.Inputs[1])));
                    }

                case MultiplyOperation _:
                    {
                        var multiplicand1 = components.Select(g => g.Inputs[0]);
                        var multiplicand2 = components.Select(g => g.Inputs[1]);

                        if (!(multiplicand1.First() is ConstantNode) && multiplicand2.First() is ConstantNode)
                        {
                            var temp = multiplicand1;
                            multiplicand1 = multiplicand2;
                            multiplicand2 = temp;
                        }

                        bool firstIsAssociative = AssociativityTester.TestForMultiplication(multiplicand1.First());
                        bool secondIsAssociative = AssociativityTester.TestForMultiplication(multiplicand2.First());
                        string format =
                            (firstIsAssociative ? "{0}" : "({0})") +
                            " * " +
                            (secondIsAssociative ? "{1}" : "({1})");

                        return string.Format(format,
                            Compile(multiplicand1, promoteToVectorSize),
                            Compile(multiplicand2, promoteToVectorSize));
                    }

                case DivisionOperation _:
                    {
                        var dividend = components.Select(g => g.Inputs[0]);
                        var divisor = components.Select(g => g.Inputs[1]);

                        bool divisorIsAssociative = AssociativityTester.TestForMultiplication(divisor.First());
                        string format = divisorIsAssociative
                            ? "{0} / {1}"
                            : "{0} / ({1})";

                        return string.Format("{0} / {1}",
                            Compile(dividend),
                            Compile(divisor));
                    }

                case MaximumOperation _:
                case MinimumOperation _:
                case PowerOperation _:
                    {
                        var value1 = Compile(components.Select(g => g.Inputs[0]));
                        var value2 = Compile(components.Select(g => g.Inputs[1]));

                        var name = operation.Mnemonic;

                        return $"{name}({value1}, {value2})";
                    }

                case LinearInterpolateOperation _:
                    {
                        var value1 = Compile(components.Select(g => g.Inputs[0]));
                        var value2 = Compile(components.Select(g => g.Inputs[1]));
                        var value3 = Compile(components.Select(g => g.Inputs[2]));

                        var name = "lerp";

                        return $"{name}({value1}, {value2}, {value3})";
                    }

                case CompareOperation _:
                    {
                        var value1 = Compile(components.Select(g => g.Inputs[0]));
                        var value2 = Compile(components.Select(g => g.Inputs[1]), components.Count);
                        var value3 = Compile(components.Select(g => g.Inputs[2]), components.Count);

                        return $"{value1} >= 0 ? {value2} : {value3}";
                    }
                default:
                    throw new NotImplementedException();
            }
        }

        private string CompileNodesWithComponents(List<HlslTreeNode> components, HlslTreeNode first, int promoteToVectorSize)
        {
            var componentsWithIndices = components.Cast<IHasComponentIndex>();

            if (first is RegisterInputNode shaderInput)
            {
                var registerKey = shaderInput.RegisterComponentKey.RegisterKey;

                string swizzle = "";
                if (registerKey.Type != RegisterType.Sampler)
                {
                    swizzle = GetAstSourceSwizzleName(componentsWithIndices,
                        _registers.GetRegisterFullLength(registerKey),
                        promoteToVectorSize);
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
                string input = Compile(first.Inputs);
                string swizzle = GetAstSourceSwizzleName(componentsWithIndices, 4);
                return $"normalize({input}){swizzle}";
            }

            throw new NotImplementedException();
        }

        private static string GetAstSourceSwizzleName(IEnumerable<IHasComponentIndex> inputs,
            int registerSize, 
            int promoteToVectorSize = PromoteToAnyVectorSize)
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

            if (promoteToVectorSize == PromoteToAnyVectorSize && swizzleName.Distinct().Count() == 1)
            {
                return "." + swizzleName.First();
            }

            return "." + swizzleName;
        }
    }
}
