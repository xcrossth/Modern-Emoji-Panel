namespace EmojiBaseline.Generator;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            var options = ParseOptions(args);
            var summary = new BaselineGenerator().Generate(options);
            Console.WriteLine(
                $"Generated {summary.EntryCount} Emoji Entries for {summary.BaselineId} at {summary.OutputDirectory}");
            Console.WriteLine(
                $"Asset review: {summary.SharedFlagSourceCount} region flags, " +
                $"{summary.AliasCollisionCount} alias collisions, " +
                $"{summary.AsymmetricAssetCount} asymmetric keys, " +
                $"{summary.UnreferencedAssetCount} unreferenced source assets");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Emoji Baseline generation failed: {exception.Message}");
            return 1;
        }
    }

    private static GeneratorOptions ParseOptions(string[] args)
    {
        string? repositoryRoot = null;
        string? outputDirectory = null;
        string? previousEmojiDataPath = null;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--repository-root":
                    repositoryRoot = ReadValue(args, ref index);
                    break;
                case "--output":
                    outputDirectory = ReadValue(args, ref index);
                    break;
                case "--previous":
                    previousEmojiDataPath = ReadValue(args, ref index);
                    break;
                case "--help":
                case "-h":
                    Console.WriteLine(
                        "Usage: dotnet run --project tools/emoji-baseline -- " +
                        "[--repository-root <path>] [--output <path>] [--previous <emoji.json|directory>]");
                    Environment.Exit(0);
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {args[index]}");
            }
        }

        repositoryRoot = repositoryRoot is null
            ? FindRepositoryRoot(Environment.CurrentDirectory)
            : Path.GetFullPath(repositoryRoot);
        outputDirectory = outputDirectory is null
            ? Path.Combine(repositoryRoot, "data", "emoji-baseline", "17.0")
            : Path.GetFullPath(outputDirectory);

        return new GeneratorOptions
        {
            RepositoryRoot = repositoryRoot,
            OutputDirectory = outputDirectory,
            PreviousEmojiDataPath = previousEmojiDataPath,
        };
    }

    private static string ReadValue(string[] args, ref int index)
    {
        index++;
        if (index >= args.Length || string.IsNullOrWhiteSpace(args[index]))
        {
            throw new ArgumentException("An option value is missing");
        }

        return args[index];
    }

    private static string FindRepositoryRoot(string startPath)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(startPath));
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "vendor", "emoji-baseline", "sources.lock.json")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to find the repository root from the current directory");
    }
}
