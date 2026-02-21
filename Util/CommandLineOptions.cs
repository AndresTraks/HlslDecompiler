using System;

namespace HlslDecompiler.Util;

public class CommandLineOptions
{
    public string InputFilename { get; }
    public bool DoAstAnalysis { get; }
    public bool PrintToConsole { get; }

    public static CommandLineOptions Parse(string[] args)
    {
        return new CommandLineOptions(args);
    }

    private CommandLineOptions(string[] args)
    {
        foreach (string arg in args)
        {
            if (arg.StartsWith("--"))
            {
                string option = arg.Substring(2);
                if (option == "ast")
                {
                    DoAstAnalysis = true;
                }
                else if (option == "print")
                {
                    PrintToConsole = true;
                }
                else
                {
                    Console.WriteLine("Unknown option: --" + option);
                }
            }
            else
            {
                InputFilename = arg;
            }
        }
    }
}
