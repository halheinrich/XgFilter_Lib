namespace XgFilter_Lib.Tests;

/// <summary>
/// The umbrella's <c>backgammon/TestData</c> corpus, read in place from the
/// test output directory (<c>bin/&lt;Configuration&gt;/net10.0</c>, five levels
/// below the umbrella root). Nothing copies the corpus into the build output.
/// </summary>
internal static class TestPaths
{
    private static readonly string Root =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\TestData"));

    /// <summary>Fixture-agnostic working matches; may be empty.</summary>
    public static string XgDir => Path.Combine(Root, "xg");

    /// <summary>Append-only fixtures, pinned by name; a test that reads them carries <c>RequiresFixtureFiles</c>.</summary>
    public static string FixtureFilesDir => Path.Combine(Root, "FixtureFiles");
}
