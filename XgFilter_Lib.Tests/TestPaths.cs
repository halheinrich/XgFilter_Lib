namespace BackgammonDiagram_Lib.Tests;

internal static class TestPaths
{
    private static readonly string _root =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\TestData"));

    public static string BothWaysDir => Path.Combine(_root, "BothWays");
    public static string ThisWayXg   => Path.Combine(BothWaysDir, "ThisWay.xg");
    public static string ThatWayXg   => Path.Combine(BothWaysDir, "ThatWay.xg");

    public static string OutputDir   => Path.Combine(_root, "Output");

    /// <summary>
    /// Writes a diagram SVG to TestData\Output\{filename} for manual browser inspection.
    /// Creates the Output directory if it doesn't exist.
    /// </summary>
    public static string SvgOutputPath(string filename)
    {
        Directory.CreateDirectory(OutputDir);
        return Path.Combine(OutputDir, filename);
    }
}
