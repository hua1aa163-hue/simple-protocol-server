namespace AutoTestClient.Recipes;

/// <summary>MRTEST Elems 目录的轻量配方目录；不修改设备文件，只提供绑定和校验。</summary>
public sealed record RecipeEntry(string Name, string FilePath);

public static class RecipeCatalog
{
    private static readonly string[] Extensions = [".xml", ".json", ".recipe", ".elems"];

    public static IReadOnlyList<RecipeEntry> Discover(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return Array.Empty<RecipeEntry>();
        return Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
            .Where(p => Extensions.Contains(Path.GetExtension(p), StringComparer.OrdinalIgnoreCase))
            .OrderBy(Path.GetFileName, StringComparer.CurrentCultureIgnoreCase)
            .Select(p => new RecipeEntry(Path.GetFileNameWithoutExtension(p), p))
            .ToArray();
    }

    public static string ResolveFilePath(string directory, string recipeName)
    {
        if (string.IsNullOrWhiteSpace(recipeName)) return string.Empty;
        if (Path.IsPathRooted(recipeName) && File.Exists(recipeName)) return recipeName;
        return Discover(directory).FirstOrDefault(e => e.Name.Equals(recipeName.Trim(), StringComparison.OrdinalIgnoreCase))?.FilePath ?? string.Empty;
    }
}
