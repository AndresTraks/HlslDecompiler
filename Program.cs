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
    /// An effect is written back whole, as name.fx: its parameters, its shaders as
    /// functions, and its techniques. The disassembly is a listing per shader,
    /// name_vs0.asm, name_ps0.asm and so on, numbered per stage in the order the
    /// effect stores them.
    /// </summary>
    private static void ReadEffect(string baseFilename, FileStream inputStream, CommandLineOptions options)
    {
        long start = inputStream.Position;
        using var input = new EffectReader(inputStream, true);
        bool isD3D9 = input.ReadUInt32() == EffectReader.Fx20;
        inputStream.Position = start;

        Action<TextWriter> write;
        if (isD3D9)
        {
            var writer = new D3D9EffectWriter(input.ReadD3D9Effect(), options.DoAstAnalysis);
            write = writer.Write;
        }
        else
        {
            var writer = new EffectWriter(input.ReadEffect(), options.DoAstAnalysis);
            write = writer.Write;
        }
        inputStream.Position = start;
        ReadShadersOfEffect(baseFilename, input, options, writeHlsl: false);

        if (options.PrintToConsole)
        {
            write(Console.Out);
            return;
        }
        string hlslFilename = $"{baseFilename}.fx";
        Console.WriteLine("Writing {0}", hlslFilename);
        using var file = new StreamWriter(hlslFilename);
        write(file);
    }

    private static void ReadShadersOfEffect(string baseFilename, EffectReader input, CommandLineOptions options, bool writeHlsl)
    {
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
            if (!writeHlsl)
            {
                continue;
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
