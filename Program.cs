using System;
using System.IO;

namespace HlslDecompiler
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("Expected input filename");
                return;
            }

            string inputFilename = args[0];
            string baseFilename = Path.GetFileNameWithoutExtension(inputFilename);

            using (var inputStream = File.Open(inputFilename, FileMode.Open, FileAccess.Read))
            {
                ShaderModel shader;
                AsmWriter writer;
                HlslWriter hlslWriter;

                var format = DetectFormat.Detect(inputStream);
                switch (format)
                {
                    case ShaderFileFormat.Rgxa:
                        using (var input = new RgxaReader(inputStream, true))
                        {
                            int ivs = 0, ips = 0;
                            while (true)
                            {
                                shader = input.ReadShader();
                                if (shader == null)
                                {
                                    break;
                                }

                                string outFilename;
                                if (shader.Type == ShaderType.Vertex)
                                {
                                    outFilename = string.Format("{0}_vs{1}", baseFilename, ivs);
                                    ivs++;
                                }
                                else
                                {
                                    outFilename = string.Format("{0}_ps{1}", baseFilename, ips);
                                    ips++;
                                }
                                Console.WriteLine(outFilename);

                                //shader.ToFile(outFilename.fxc", outFilename, i));

                                writer = new AsmWriter(shader);
                                writer.Write(outFilename + ".asm");

                                hlslWriter = new HlslWriter(shader);
                                hlslWriter.Write(outFilename + ".fx");

                                Console.WriteLine();
                            } 
                        }
                        break;

                    case ShaderFileFormat.Hlsl:
                        using (var input = new ShaderReader(inputStream, true))
                        {
                            Console.WriteLine();
                            Console.WriteLine("{0}", baseFilename);
                            shader = input.ReadShader();

                            writer = new AsmWriter(shader);
                            writer.Write(string.Format("{0}.asm", baseFilename));

                            hlslWriter = new HlslWriter(shader);
                            hlslWriter.Write(string.Format("{0}.fx", baseFilename));
                        }
                        break;

                    case ShaderFileFormat.Unknown:
                        Console.WriteLine("Unknown file format!");
                        break;
                }
            }

            Console.ReadKey();
        }
    }
}
