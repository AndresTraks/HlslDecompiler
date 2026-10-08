using System.Collections.Generic;

namespace HlslDecompiler.Util;

/// <summary>
/// The command line, parsed or refused. A parse that fails carries the reason in
/// <see cref="Error"/> and nothing else is to be trusted.
/// </summary>
public class CommandLineOptions
{
    public const string Usage =
        "Usage: HlslDecompiler [options] <shader>\r\n"
        + "\r\n"
        + "Decompiles Direct3D shader bytecode, an effect, or an .rgxa archive, and\r\n"
        + "writes the HLSL to standard output.\r\n"
        + "\r\n"
        + "Options:\r\n"
        + "  -o, --output <file>  Write to <file> instead of standard output\r\n"
        + "  --asm                Write the disassembly instead of HLSL\r\n"
        + "  --instructions       Write HLSL one statement per instruction instead\r\n"
        + "                       of the simplified expression tree\r\n"
        + "  --flow-attributes    Mark every if [branch] and every loop [loop], so that\r\n"
        + "                       the shader branches and loops where the original did\r\n"
        + "  --verbose            Print the stack trace when decompiling fails\r\n"
        + "  -h, --help           Show this help\r\n";

    public string InputFilename { get; private set; }
    public string OutputFilename { get; private set; }
    public bool Disassemble { get; private set; }
    public bool DoAstAnalysis { get; private set; } = true;
    public bool WritesFlowAttributes { get; private set; }
    public bool Verbose { get; private set; }
    public bool ShowHelp { get; private set; }
    public string Error { get; private set; }

    public static CommandLineOptions Parse(string[] args)
    {
        var options = new CommandLineOptions();
        options.ParseArguments(args);
        return options;
    }

    private void ParseArguments(string[] args)
    {
        var inputs = new List<string>();
        bool optionsEnded = false;
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (optionsEnded || arg == "-" || !arg.StartsWith('-'))
            {
                inputs.Add(arg);
                continue;
            }

            // --output=file is the same as --output file.
            string value = null;
            int equals = arg.IndexOf('=');
            if (arg.StartsWith("--") && equals != -1)
            {
                value = arg[(equals + 1)..];
                arg = arg[..equals];
            }

            switch (arg)
            {
                case "--":
                    optionsEnded = true;
                    break;
                case "-o":
                case "--output":
                    if (value == null)
                    {
                        if (i + 1 == args.Length)
                        {
                            Error = $"option {arg} needs a file name";
                            return;
                        }
                        value = args[++i];
                    }
                    if (OutputFilename != null)
                    {
                        Error = "more than one output file given";
                        return;
                    }
                    OutputFilename = value;
                    break;
                case "--asm":
                    Disassemble = true;
                    break;
                case "--instructions":
                    DoAstAnalysis = false;
                    break;
                case "--flow-attributes":
                    WritesFlowAttributes = true;
                    break;
                case "--verbose":
                    Verbose = true;
                    break;
                case "-h":
                case "--help":
                    ShowHelp = true;
                    break;
                default:
                    Error = $"unknown option {arg}";
                    return;
            }

            if (value != null && arg is not ("-o" or "--output"))
            {
                Error = $"option {arg} takes no value";
                return;
            }
        }

        if (ShowHelp)
        {
            return;
        }
        if (inputs.Count == 0)
        {
            Error = "no input file given";
        }
        else if (inputs.Count > 1)
        {
            Error = $"one input file expected, {inputs.Count} given";
        }
        else if (inputs[0] == "-")
        {
            Error = "reading from standard input is not supported";
        }
        else
        {
            InputFilename = inputs[0];
        }
    }
}
