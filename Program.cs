using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl;
using HlslDecompiler.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HlslDecompiler;

class Program
{
    private const string Name = "HlslDecompiler";

    // 0 decompiled, 1 the input could not be read or decompiled or the output not
    // written, 2 the command line was wrong.
    private const int Success = 0;
    private const int Failure = 1;
    private const int UsageError = 2;

    public static int Main(string[] args)
    {
        var options = CommandLineOptions.Parse(args);
        if (options.Error != null)
        {
            Console.Error.WriteLine($"{Name}: {options.Error}");
            Console.Error.WriteLine($"Try '{Name} --help' for more information.");
            return UsageError;
        }
        if (options.ShowHelp)
        {
            Console.Out.Write(CommandLineOptions.Usage);
            return Success;
        }

        byte[] input;
        try
        {
            input = File.ReadAllBytes(options.InputFilename);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"{Name}: cannot read {options.InputFilename}: {e.Message}");
            return Failure;
        }

        using var inputStream = new MemoryStream(input, writable: false);
        ShaderFileFormat format = FormatDetector.Detect(inputStream);

        bool isArchive = format == ShaderFileFormat.Rgxa;
        if (isArchive && options.OutputFilename == null)
        {
            Console.Error.WriteLine($"{Name}: {options.InputFilename} requires a directory with -o");
            return UsageError;
        }
        if (options.OutputFilename != null
            && isArchive != Directory.Exists(options.OutputFilename))
        {
            Console.Error.WriteLine(isArchive
                ? $"{Name}: {options.OutputFilename} is not a directory"
                : $"{Name}: {options.OutputFilename} is a directory");
            return UsageError;
        }

        // Decompiled whole before anything is written, so that a shader the
        // decompiler cannot take leaves no half-written file or half a listing.
        List<(string Path, string Text)> outputs;
        try
        {
            outputs = isArchive
                ? DecompileRgxa(inputStream, options)
                : [(options.OutputFilename, Decompile(inputStream, format, options))];
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(
                $"{Name}: cannot decompile {options.InputFilename}: {Describe(e)}");
            if (options.Verbose)
            {
                Console.Error.WriteLine(e);
            }
            return Failure;
        }

        foreach ((string path, string text) in outputs)
        {
            if (path == null)
            {
                Console.Out.Write(text);
                continue;
            }
            try
            {
                File.WriteAllText(path, text);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"{Name}: cannot write {path}: {e.Message}");
                return Failure;
            }
        }
        return Success;
    }

    private static string Decompile(Stream inputStream, ShaderFileFormat format,
        CommandLineOptions options)
    {
        switch (format)
        {
            case ShaderFileFormat.ShaderModel:
                {
                    using var reader = new ShaderReader(inputStream, true);
                    return DecompileShader(reader.ReadShader(), options);
                }
            case ShaderFileFormat.Dxbc:
                {
                    using var reader = new DxbcReader(inputStream, true);
                    return DecompileShader(reader.ReadShader(), options);
                }
            case ShaderFileFormat.Effect:
                return DecompileEffect(inputStream, options);
            default:
                throw new InvalidDataException("Unknown shader type");
        }
    }

    private static string DecompileShader(ShaderModel shader, CommandLineOptions options)
    {
        if (options.Disassemble)
        {
            return Disassemble(shader);
        }
        var hlsl = new StringWriter();
        CreateHlslWriter(shader, options).Write(hlsl);
        return hlsl.ToString();
    }

    private static string Disassemble(ShaderModel shader)
    {
        using var listing = new MemoryStream();
        new AsmWriter(shader).Write(listing);
        return Encoding.UTF8.GetString(listing.ToArray());
    }

    /// <summary>
    /// An effect is written back whole: its parameters, its shaders as functions,
    /// and its techniques. Its disassembly is a listing per shader, each under a
    /// header naming it by stage and place - vs0, ps0 and so on, numbered per stage
    /// in the order the effect stores them.
    /// </summary>
    private static string DecompileEffect(Stream inputStream, CommandLineOptions options)
    {
        long start = inputStream.Position;
        using var input = new EffectReader(inputStream, true);
        if (options.Disassemble)
        {
            return WriteEach(input.ReadShaders(), Disassemble);
        }

        bool isD3D9 = input.ReadUInt32() == EffectReader.Fx20;
        inputStream.Position = start;
        var hlsl = new StringWriter();
        if (isD3D9)
        {
            new D3D9EffectWriter(input.ReadD3D9Effect(), options.DoAstAnalysis, options.WritesFlowAttributes).Write(hlsl);
        }
        else
        {
            new EffectWriter(input.ReadEffect(), options.DoAstAnalysis, options.WritesFlowAttributes).Write(hlsl);
        }
        return hlsl.ToString();
    }

    /// <summary>
    /// An archive of separate shaders, each to a file of its own in the output
    /// directory: archive_vs0.fx, archive_ps0.fx and so on, or .asm with --asm.
    /// </summary>
    private static List<(string Path, string Text)> DecompileRgxa(
        Stream inputStream, CommandLineOptions options)
    {
        using var input = new RgxaReader(inputStream, true);
        var shaders = new List<ShaderModel>();
        for (ShaderModel shader = input.ReadShader(); shader != null; shader = input.ReadShader())
        {
            shaders.Add(shader);
        }

        string baseFilename = Path.GetFileNameWithoutExtension(options.InputFilename);
        string extension = options.Disassemble ? "asm" : "fx";
        var outputs = new List<(string Path, string Text)>();
        foreach ((ShaderModel shader, string name) in NameEach(shaders))
        {
            outputs.Add((
                Path.Combine(options.OutputFilename, $"{baseFilename}_{name}.{extension}"),
                DecompileShader(shader, options)));
        }
        return outputs;
    }

    /// <summary>
    /// Several shaders in one output: each under a comment naming it - its stage and
    /// its place among that stage's shaders, then its profile - with a blank line
    /// between one and the next.
    /// </summary>
    private static string WriteEach(IEnumerable<ShaderModel> shaders, Func<ShaderModel, string> write)
    {
        var output = new StringBuilder();
        foreach ((ShaderModel shader, string name) in NameEach(shaders))
        {
            if (output.Length != 0)
            {
                output.Append("\r\n");
            }
            output.Append($"// {name}: {shader.Profile}\r\n");
            output.Append(write(shader));
        }
        return output.ToString();
    }

    /// <summary>
    /// The name a shader of several goes by: its stage and its place among that
    /// stage's shaders, vs0, ps0, vs1, in the order they are stored.
    /// </summary>
    private static IEnumerable<(ShaderModel Shader, string Name)> NameEach(
        IEnumerable<ShaderModel> shaders)
    {
        var stageCounts = new Dictionary<string, int>();
        foreach (ShaderModel shader in shaders)
        {
            int index = stageCounts.GetValueOrDefault(shader.Stage);
            stageCounts[shader.Stage] = index + 1;
            yield return (shader, $"{shader.Stage}{index}");
        }
    }

    private static HlslWriter CreateHlslWriter(ShaderModel shader, CommandLineOptions options)
    {
        if (options.DoAstAnalysis)
        {
            return new HlslAstWriter(shader) { WritesFlowAttributes = options.WritesFlowAttributes };
        }
        return new HlslSimpleWriter(shader) { WritesFlowAttributes = options.WritesFlowAttributes };
    }

    // What went wrong, in a line. The decompiler says what it has no reading of by
    // throwing NotImplementedException with the construct's name, which on its own
    // reads as a bare opcode.
    private static string Describe(Exception e) => e switch
    {
        NotImplementedException => $"not implemented: {e.Message}",
        NotSupportedException => $"not supported: {e.Message}",
        InvalidDataException => e.Message,
        _ => $"{e.GetType().Name}: {e.Message}",
    };
}
