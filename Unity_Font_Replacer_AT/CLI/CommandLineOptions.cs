using System.CommandLine;

namespace UnityFontReplacer.CLI;

internal static class CommandLineOptions
{
    public static Option<string> CreateGamePathOption()
    {
        return RequiredOption<string>("--gamepath", "Unity game path", "-g");
    }

    public static Option<T> RequiredOption<T>(string name, string description, params string[] aliases)
    {
        return new Option<T>(name, aliases)
        {
            Description = description,
            Required = true,
        };
    }

    public static Option<T> OptionalOption<T>(string name, string description, params string[] aliases)
    {
        return new Option<T>(name, aliases)
        {
            Description = description,
        };
    }

    public static Option<T> OptionalOption<T>(string name, T defaultValue, string description, params string[] aliases)
    {
        return new Option<T>(name, aliases)
        {
            Description = description,
            DefaultValueFactory = _ => defaultValue,
        };
    }
}
