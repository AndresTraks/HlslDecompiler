using HlslDecompiler.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HlslDecompiler.DirectXShaderModel
{
    public class ShaderReader : BinaryReader
    {
        public ShaderReader(Stream input, bool leaveOpen = false)
            : base(input, new UTF8Encoding(false, true), leaveOpen)
        {
        }

        virtual public ShaderModel ReadShader()
        {
            // Version token
            byte minorVersion = ReadByte();
            byte majorVersion = ReadByte();
            ShaderType shaderType = (ShaderType)ReadUInt16();

            var instructions = new List<Instruction>();
            while (true)
            {
                D3D9Instruction instruction = ReadInstruction();
                InstructionVerifier.Verify(instruction);
                instructions.Add(instruction);
                if (instruction.Opcode == Opcode.End) break;
            }

            return new ShaderModel(majorVersion, minorVersion, shaderType, ParseConstantTable(instructions), instructions);
        }

        private static IList<ConstantDeclaration> ParseConstantTable(IList<Instruction> instructions)
        {
            var constantDeclarations = new List<ConstantDeclaration>();

            byte[] constantTable = GetConstantTableData(instructions);
            if (constantTable == null)
            {
                return constantDeclarations;
            }

            var ctabStream = new MemoryStream(constantTable);
            using (var ctabReader = new BinaryReader(ctabStream))
            {
                int ctabSize = ctabReader.ReadInt32();
                System.Diagnostics.Debug.Assert(ctabSize == 0x1C);
                long creatorPosition = ctabReader.ReadInt32();

                int minorVersion = ctabReader.ReadByte();
                int majorVersion = ctabReader.ReadByte();

                var shaderType = (ShaderType)ctabReader.ReadUInt16();

                int numConstants = ctabReader.ReadInt32();
                long constantInfoPosition = ctabReader.ReadInt32();
                ShaderFlags shaderFlags = (ShaderFlags)ctabReader.ReadInt32();
                Console.WriteLine("Flags: {0}", shaderFlags);

                long shaderModelPosition = ctabReader.ReadInt32();
                //Console.WriteLine("ctabStart = {0}, shaderModelPosition = {1}", ctabStart, shaderModelPosition);


                ctabStream.Position = creatorPosition;
                string compilerInfo = ReadStringNullTerminated(ctabStream);
                Console.WriteLine("Compiler: " + compilerInfo);

                ctabStream.Position = shaderModelPosition;
                string shaderModel = ReadStringNullTerminated(ctabStream);
                Console.WriteLine("Shader model: " + shaderModel);


                for (int i = 0; i < numConstants; i++)
                {
                    ctabStream.Position = constantInfoPosition + i * 20;
                    ConstantDeclaration declaration = ReadConstantDeclaration(ctabReader);
                    constantDeclarations.Add(declaration);
                }
            }

            return constantDeclarations;
        }

        private static byte[] GetConstantTableData(IList<Instruction> instructions)
        {
            int ctabToken = FourCC.Make("CTAB");
            var ctabComment = instructions
                .OfType<D3D9Instruction>()
                .FirstOrDefault(x => x.Opcode == Opcode.Comment && x.Params[0] == ctabToken);
            if (ctabComment == null)
            {
                return null;
            }

            byte[] constantTable = new byte[ctabComment.Params.Count * 4];
            for (int i = 1; i < ctabComment.Params.Count; i++)
            {
                constantTable[i * 4 - 4] = (byte)(ctabComment.Params[i] & 0xFF);
                constantTable[i * 4 - 3] = (byte)((ctabComment.Params[i] >> 8) & 0xFF);
                constantTable[i * 4 - 2] = (byte)((ctabComment.Params[i] >> 16) & 0xFF);
                constantTable[i * 4 - 1] = (byte)((ctabComment.Params[i] >> 24) & 0xFF);
            }

            return constantTable;
        }

        private static ConstantDeclaration ReadConstantDeclaration(BinaryReader ctabReader)
        {
            var ctabStream = ctabReader.BaseStream;

            // D3DXSHADER_CONSTANTINFO
            int nameOffset = ctabReader.ReadInt32();
            RegisterSet registerSet = (RegisterSet)ctabReader.ReadInt16();
            short registerIndex = ctabReader.ReadInt16();
            short registerCount = ctabReader.ReadInt16();
            ctabStream.Position += sizeof(short); // Reserved
            int typeInfoOffset = ctabReader.ReadInt32();
            int defaultValueOffset = ctabReader.ReadInt32();
            System.Diagnostics.Debug.Assert(defaultValueOffset == 0);

            ctabStream.Position = nameOffset;
            string name = ReadStringNullTerminated(ctabStream);

            // D3DXSHADER_TYPEINFO
            ctabStream.Position = typeInfoOffset;
            ParameterClass cl = (ParameterClass)ctabReader.ReadInt16();
            ParameterType type = (ParameterType)ctabReader.ReadInt16();
            short rows = ctabReader.ReadInt16();
            short columns = ctabReader.ReadInt16();
            short numElements = ctabReader.ReadInt16();
            short numStructMembers = ctabReader.ReadInt16();
            int structMemberInfoOffset = ctabReader.ReadInt32();
            //System.Diagnostics.Debug.Assert(numElements == 1);
            System.Diagnostics.Debug.Assert(structMemberInfoOffset == 0);

            return new ConstantDeclaration(name, registerSet, registerIndex, registerCount, cl, type, rows, columns);
        }

        private D3D9Instruction ReadInstruction()
        {
            uint instructionToken = ReadUInt32();
            Opcode opcode = (Opcode)(instructionToken & 0xffff);

            int size;
            if (opcode == Opcode.Comment)
            {
                size = (int)((instructionToken >> 16) & 0x7FFF);
            }
            else
            {
                size = (int)((instructionToken >> 24) & 0x0f);
            }

            uint[] paramTokens = new uint[size];
            for (int i = 0; i < size; i++)
            {
                paramTokens[i] = ReadUInt32();
            }
            var instruction = new D3D9Instruction(opcode, paramTokens);

            if (opcode != Opcode.Comment)
            {
                instruction.Modifier = (int)((instructionToken >> 16) & 0xff);
                instruction.Predicated = (instructionToken & 0x10000000) != 0;
                System.Diagnostics.Debug.Assert((instructionToken & 0xE0000000) == 0);
            }

            return instruction;
        }

        private static string ReadStringNullTerminated(Stream stream)
        {
            var builder = new StringBuilder();
            char b;
            while ((b = (char)stream.ReadByte()) != 0)
            {
                builder.Append(b);
            }
            return builder.ToString();
        }
    }
}
