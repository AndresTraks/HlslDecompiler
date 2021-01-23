using HlslDecompiler.DirectXShaderModel;
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
            
            // Try to simplify HLSL expressions by doing AST analysis
            bool doAstAnalysis = false;

            using (var inputStream = File.Open(inputFilename, FileMode.Open, FileAccess.Read))
            {
                var format = FormatDetector.Detect(inputStream);
                switch (format)
                {
                    case ShaderFileFormat.ShaderModel:
                        ReadShaderModel(baseFilename, inputStream, doAstAnalysis);
                        break;
                    case ShaderFileFormat.Rgxa:
                        ReadRgxa(baseFilename, inputStream, doAstAnalysis);
                        break;
                    case ShaderFileFormat.Unknown:
                        Console.WriteLine("Unknown file format!");
                        break;
                }
            }

            Console.ReadKey();
        }

        private static void ReadShaderModel(string baseFilename, FileStream inputStream, bool doAstAnalysis)
        {
            using (var input = new ShaderReader(inputStream, true))
            {
                Console.WriteLine();
                Console.WriteLine("{0}", baseFilename);
                var shader = input.ReadShader();

                AsmWriter writer = new AsmWriter(shader);
                writer.Write($"{baseFilename}.asm");

                var hlslWriter = new HlslWriter(shader, doAstAnalysis);
                hlslWriter.Write($"{baseFilename}.fx");
            }
        }

        private static void ReadRgxa(string baseFilename, FileStream inputStream, bool doAstAnalysis)
        {
            using (var input = new RgxaReader(inputStream, true))
            {
                int ivs = 0, ips = 0;
                while (true)
                {
                    ShaderModel shader = input.ReadShader();
                    if (shader == null)
                    {
                        break;
                    }

                    string outFilename;
                    if (shader.Type == ShaderType.Vertex)
                    {
                        outFilename = $"{baseFilename}_vs{ivs}";
                        ivs++;
                    }
                    else
                    {
                        outFilename = $"{baseFilename}_ps{ips}";
                        ips++;
                    }
                    Console.WriteLine(outFilename);

                    //shader.ToFile("outFilename.fxc");

                    var writer = new AsmWriter(shader);
                    writer.Write(outFilename + ".asm");

                    var hlslWriter = new HlslWriter(shader, doAstAnalysis);
                    hlslWriter.Write(outFilename + ".fx");

                    Console.WriteLine();
                }
            }
        }
    }
}
