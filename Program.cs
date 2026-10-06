using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl;
using HlslDecompiler.Util;
using System;
using System.Collections.Generic;
using System.IO;

namespace HlslDecompiler;

class Program
{
    public static void Main(string[] args)
    {
        var options = CommandLineOptions.Parse(args);
        if (options.InputFilename == null)
        {
            Console.WriteLine("Expected input filename");
            return;
        }

        string baseFilename = Path.GetFileNameWithoutExtension(options.InputFilename);

        using var inputStream = File.Open(options.InputFilename, FileMode.Open, FileAccess.Read);
        var format = FormatDetector.Detect(inputStream);
        switch (format)
        {
            case ShaderFileFormat.ShaderModel:
                ReadShaderModel(baseFilename, inputStream, options);
                break;
            case ShaderFileFormat.Dxbc:
                ReadDxbc(baseFilename, inputStream, options);
                break;
            case ShaderFileFormat.Rgxa:
                ReadRgxa(baseFilename, inputStream, options.DoAstAnalysis);
                break;
            case ShaderFileFormat.Effect:
                ReadEffect(baseFilename, inputStream, options);
                break;
            case ShaderFileFormat.Unknown:
                Console.WriteLine("Unknown file format!");
                break;
        }

        if (!options.PrintToConsole)
        {
            Console.WriteLine("Finished.");
        }
    }

    private static void ReadShaderModel(string baseFilename, FileStream inputStream, CommandLineOptions options)
    {
        using (var input = new ShaderReader(inputStream, true))
        {
            ShaderModel shader = input.ReadShader();

            if (!options.PrintToConsole)
            {
                var writer = new AsmWriter(shader);
                string asmFilename = $"{baseFilename}.asm";
                Console.WriteLine("Writing {0}", asmFilename);
                writer.Write(asmFilename);
            }

            var hlslWriter = CreateHlslWriter(shader, options.DoAstAnalysis);
            if (options.PrintToConsole)
            {
                hlslWriter.Write(Console.Out);
            }
            else
            {
                string hlslFilename = $"{baseFilename}.fx";
                Console.WriteLine("Writing {0}", hlslFilename);
                hlslWriter.Write(hlslFilename);
            }
        }
    }

    private static void ReadDxbc(string baseFilename, FileStream inputStream, CommandLineOptions options)
    {
        using (var input = new DxbcReader(inputStream, true))
        {
            ShaderModel shader = input.ReadShader();

            if (!options.PrintToConsole)
            {
                var writer = new AsmWriter(shader);
                string asmFilename = $"{baseFilename}.asm";
                Console.WriteLine("Writing {0}", asmFilename);
                writer.Write(asmFilename);
            }

            var hlslWriter = CreateHlslWriter(shader, options.DoAstAnalysis);
            if (options.PrintToConsole)
            {
                hlslWriter.Write(Console.Out);
            }
            else
            {
                string hlslFilename = $"{baseFilename}.fx";
                Console.WriteLine("Writing {0}", hlslFilename);
                hlslWriter.Write(hlslFilename);
            }
        }
    }

    /// <summary>
    /// Each shader an effect holds, as if it had been compiled on its own: name_vs0,
    /// name_ps0 and so on, numbered per stage in the order the effect stores them.
    /// The techniques and passes that set them are not written yet.
    /// </summary>
    private static void ReadEffect(string baseFilename, FileStream inputStream, CommandLineOptions options)
    {
        using var input = new EffectReader(inputStream, true);
        var stageCounts = new Dictionary<string, int>();
        foreach (ShaderModel shader in input.ReadShaders())
        {
            int index = stageCounts.GetValueOrDefault(shader.Stage);
            stageCounts[shader.Stage] = index + 1;
            string outFilename = $"{baseFilename}_{shader.Stage}{index}";

            if (!options.PrintToConsole)
            {
                string asmFilename = $"{outFilename}.asm";
                Console.WriteLine("Writing {0}", asmFilename);
                new AsmWriter(shader).Write(asmFilename);
            }

            // Written whole before it goes anywhere, so that one shader the writer
            // cannot take - a preshader instruction it has no reading of - leaves
            // no half-written file and does not stop the others.
            var hlsl = new StringWriter();
            try
            {
                CreateHlslWriter(shader, options.DoAstAnalysis).Write(hlsl);
            }
            catch (NotSupportedException e)
            {
                Console.WriteLine($"Not decompiling {outFilename}: {e.Message}");
                continue;
            }

            if (options.PrintToConsole)
            {
                Console.WriteLine($"// {outFilename}: {shader.Profile}");
                Console.WriteLine(hlsl);
                continue;
            }

            string hlslFilename = $"{outFilename}.fx";
            Console.WriteLine("Writing {0}", hlslFilename);
            File.WriteAllText(hlslFilename, hlsl.ToString());
        }
    }

    private static void ReadRgxa(string baseFilename, FileStream inputStream, bool doAstAnalysis)
    {
        using var input = new RgxaReader(inputStream, true);
        int ivs = 0, ips = 0;
        while (true)
        {
            ShaderModel shader = input.ReadShader();
            if (shader is null)
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

    private static HlslWriter CreateHlslWriter(ShaderModel shader, bool doAstAnalysis)
    {
        if (doAstAnalysis)
        {
            return new HlslAstWriter(shader);
        }
        return new HlslSimpleWriter(shader);
    }
}
