using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl;
using HlslDecompiler.Util;
using System;
using System.IO;

namespace HlslDecompiler
{
    class Program
    {
        static void Main(string[] args)
        {
            var options = CommandLineOptions.Parse(args);
            if (options.InputFilename == null)
            {
                Console.WriteLine("Expected input filename");
                return;
            }

            string baseFilename = Path.GetFileNameWithoutExtension(options.InputFilename);

            using (var inputStream = File.Open(options.InputFilename, FileMode.Open, FileAccess.Read))
            {
                var format = FormatDetector.Detect(inputStream);
                switch (format)
                {
                    case ShaderFileFormat.ShaderModel3:
                        ReadShaderModel(baseFilename, inputStream, options.DoAstAnalysis);
                        break;
                    case ShaderFileFormat.Rgxa:
                        ReadRgxa(baseFilename, inputStream, options.DoAstAnalysis);
                        break;
                    case ShaderFileFormat.Unknown:
                        Console.WriteLine("Unknown file format!");
                        break;
                }
            }

            Console.WriteLine("Finished.");
        }

        private static void ReadShaderModel(string baseFilename, FileStream inputStream, bool doAstAnalysis)
        {
            using (var input = new ShaderReader(inputStream, true))
            {
                ShaderModel shader = input.ReadShader();

                AsmWriter writer = new AsmWriter(shader);
                string asmFilename = $"{baseFilename}.asm";
                Console.WriteLine("Writing {0}", asmFilename);
                writer.Write(asmFilename);

                var hlslWriter = CreateHlslWriter(shader, doAstAnalysis);
                string hlslFilename = $"{baseFilename}.fx";
                Console.WriteLine("Writing {0}", hlslFilename);
                hlslWriter.Write(hlslFilename);
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

                    var hlslWriter = CreateHlslWriter(shader, doAstAnalysis);
                    hlslWriter.Write(outFilename + ".fx");
                }
            }
        }

        private static HlslWriter CreateHlslWriter(ShaderModel shader, bool doAstAnalysis)
        {
            if (doAstAnalysis)
            {
                return new HlslAstWriter(shader);
            }
            return new HlslSimpleWriter(shader);
        }
    }
}
