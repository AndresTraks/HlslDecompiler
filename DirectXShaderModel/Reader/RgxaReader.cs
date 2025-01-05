using HlslDecompiler.Util;
using System;
using System.IO;

namespace HlslDecompiler.DirectXShaderModel
{
    enum Location
    {
        Start,
        VertexShaders,
        PixelShadersHeader,
        PixelShaders,
        End
    }

    enum InputType
    {
        Bucket = 1,
        Float,
        Float2 = 4,
        Float4,
        Sampler,
        BoneMatrix = 8,
        Matrix
    }

    class RgxaReader : ShaderReader
    {
        Location location;
        int numShaders;

        public RgxaReader(Stream input, bool leaveOpen = false)
            : base(input, leaveOpen)
        {
            location = Location.Start;
        }

        public void ReadFileHeader()
        {
            int signature = ReadInt32();
            if (signature != FourCC.Make("rgxa"))
            {
                Console.WriteLine("Error: unknown file format!");
                throw new InvalidDataException();
            }

            numShaders = ReadByte();

            location = Location.VertexShaders;
        }

        void ReadShaderStruct(int numInputs)
        {
            for (int i = 0; i < numInputs; i++)
            {
                InputType inputType = (InputType)ReadByte();
                byte size = ReadByte();

                string inputName = ReadRgxaString();
                string alternateName = ReadRgxaString();

                //Console.WriteLine("{0} {1} {2}", inputType.ToString().ToLower(), inputName, alternateName);

                byte numParams = ReadByte();
                for (int p = 0; p < numParams; p++)
                {
                    string objectName = ReadRgxaString();
                    byte paramType = ReadByte();
                    if (paramType == 0)
                    {
                        uint value = ReadUInt32();
                        //Console.WriteLine("\t{0} {1}", objectName, value);
                    }
                    else if (paramType == 1)
                    {
                        float value = ReadSingle();
                        //Console.WriteLine("\t{0} {1}", objectName, value);
                    }
                    else if (paramType == 2)
                    {
                        string objectName2 = ReadRgxaString();
                        //Console.WriteLine("\t{0} {1}", objectName, objectName2);
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                }

                byte dataLength = ReadByte();
                BaseStream.Position += dataLength * 4;
            }
        }

        void ReadShaderHeader()
        {
            byte numInputs = ReadByte();
            for (int i = 0; i < numInputs; i++)
            {
                string inputName;

                InputType inputType = (InputType)ReadByte();
                byte a = ReadByte();
                if (a != 0)
                {
                    throw new NotImplementedException();
                }

                short registerindex = ReadInt16();
                inputName = ReadRgxaString();

                //Console.WriteLine("{0} {1}", inputType.ToString().ToLower(), inputName);
            }

            short bytecodeLength = ReadInt16();
            short bytecodeLength2 = ReadInt16();
            System.Diagnostics.Debug.Assert(bytecodeLength == bytecodeLength2);
            //Console.WriteLine("bytecode length: {0}", bytecodeLength);
        }

        private string ReadRgxaString()
        {
            // String length included null terminator, so take '\0' out
            string result = ReadString();
            return result.Substring(0, result.Length - 1);
        }

        void ReadPixelShadersHeader()
        {
            numShaders = ReadByte();
            BaseStream.Position += 5;
            location = Location.PixelShaders;
        }

        public override ShaderModel ReadShader()
        {
            switch (location)
            {
                case Location.Start:
                    ReadFileHeader();
                    break;
                case Location.PixelShadersHeader:
                    ReadPixelShadersHeader();
                    break;
                case Location.End:
                    return null;
            }

            if (location == Location.PixelShaders && numShaders == 1)
            {
                // AllGlobals
                byte numInputs = ReadByte();
                ReadShaderStruct(numInputs);
                //Console.WriteLine();

                numInputs = ReadByte();
                ReadShaderStruct(numInputs);
                //Console.WriteLine();

                byte numTechniques = ReadByte();
                for (int i = 0; i < numTechniques; i++)
                {
                    string technique = ReadRgxaString();
                    Console.WriteLine(technique);
                    byte numPasses = ReadByte();
                    for (int p = 0; p < numPasses; p++)
                    {
                        byte a = ReadByte();
                        byte b = ReadByte();
                        byte paramLength = ReadByte();
                        Console.WriteLine("\t{0} {1} {2}", a, b, paramLength);
                        BaseStream.Position += paramLength * 8;
                    }
                }

                numShaders = 0;
                location = Location.End;
                return null;
            }

            ReadShaderHeader();
            var shader = base.ReadShader();

            numShaders--;
            if (numShaders == 0 && location == Location.VertexShaders)
            {
                location = Location.PixelShadersHeader;
            }

            return shader;
        }
    }
}
