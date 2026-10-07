using System.Reflection;
using XgFilter_Lib.Enums;
using XgFilter_Lib.Filtering;
using XgFilter_Lib.Patterns;
using XgFilter_Lib.Projection;

namespace XgFilter_Lib.Tests;

/// <summary>
/// The collection rider (Hal, 2026-09-26, on halheinrich/backgammon#273): no
/// public member hands out a live mutable collection or array behind a
/// read-only interface. Each collection this library hands out is pinned
/// immutable where it is handed out, and one sweep holds the whole exported
/// surface to the declared-type half of the rule: no public member is
/// declared as an array. The runtime half — what an interface-typed member
/// actually returns — needs an instance, so it is each site's own test here.
/// </summary>
public class PublicCollectionsTests
{
    /// <summary>
    /// Asserts <paramref name="list"/> cannot be changed by whoever holds it:
    /// not an array or a <see cref="List{T}"/> to cast back to, read-only as a
    /// collection, and every write through <see cref="IList{T}"/> refused.
    /// </summary>
    private static void ShouldBeImmutable<T>(IReadOnlyList<T> list)
    {
        list.Should().NotBeAssignableTo<T[]>();
        list.Should().NotBeAssignableTo<List<T>>();
        list.Should().BeAssignableTo<ICollection<T>>()
            .Which.IsReadOnly.Should().BeTrue();

        if (list is IList<T> writable && list.Count > 0)
        {
            var first = list[0];
            var set = () => writable[0] = first;
            var add = () => writable.Add(first);
            var clear = () => writable.Clear();

            set.Should().Throw<NotSupportedException>();
            add.Should().Throw<NotSupportedException>();
            clear.Should().Throw<NotSupportedException>();
        }
    }

    // -----------------------------------------------------------------------
    //  The sites
    // -----------------------------------------------------------------------

    [Fact]
    public void BoardPattern_Constraints_AreImmutable()
    {
        var pattern = BoardPattern.Parse("[6,,0] [5,2,] [7-12,3,]");

        ShouldBeImmutable(pattern.Constraints);
    }

    [Fact]
    public void BoardPattern_Constraints_DoNotAliasTheCallersCollection()
    {
        var constraints = new List<IPatternConstraint> { new CheckerRange(6, 2, null) };
        var pattern = new BoardPattern(constraints);

        constraints.Add(new CheckerRange(5, 2, null));

        pattern.Constraints.Should().ContainSingle();
        pattern.ToBracketList().Should().Be("[6,2,]");
    }

    [Fact]
    public void ColumnSelector_SelectedColumns_AreImmutable()
    {
        ShouldBeImmutable(new ColumnSelector([Column.Player, Column.Error]).SelectedColumns);
        ShouldBeImmutable(new ColumnSelector().SelectedColumns);
    }

    [Fact]
    public void ColumnSelector_SelectedColumns_DoNotAliasTheCallersCollection()
    {
        var columns = new List<Column> { Column.Player, Column.Error };
        var selector = new ColumnSelector(columns);

        columns.Add(Column.Roll);

        selector.SelectedColumns.Should().Equal(Column.Player, Column.Error);
        selector.Header.Should().Be("Player,Error");
    }

    [Fact]
    public void ColumnSelector_AllColumns_IsImmutable()
    {
        // Shared by every caller and every default selector, so a write
        // through it would change every other one.
        ShouldBeImmutable(ColumnSelector.AllColumns);
        ColumnSelector.AllColumns.Should().Equal(Enum.GetValues<Column>());
    }

    [Fact]
    public void MatchScoreToken_RetiredMoneyReplacements_IsImmutable()
    {
        ShouldBeImmutable(MatchScoreToken.RetiredMoneyReplacements);
    }

    [Fact]
    public void SourceReport_Rejected_IsImmutable()
    {
        // Grown by the iterator during a walk, so the list handed out must be
        // one nobody can write through — including the iterator, which
        // replaces it rather than appending.
        var report = new SourceReport();
        var iterator = new FilteredDecisionIterator(
            new DecisionFilterSet(), BgDataTypes_Lib.PlayRanking.Equity,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<FilteredDecisionIterator>.Instance);

        _ = iterator.IterateXgStreams([new XgFileStream("bad.xg", new MemoryStream([]))], report).ToList();

        ShouldBeImmutable(report.Rejected);
        ShouldBeImmutable(new SourceReport().Rejected);
    }

    // -----------------------------------------------------------------------
    //  The sweep's declared-type half
    // -----------------------------------------------------------------------

    [Fact]
    public void NoPublicMember_IsDeclaredAsAnArray()
    {
        // A raw array handed out is writable by whoever receives it. Every
        // public property, field and method result of every exported type is
        // checked; an array a member takes as an input is the caller's own
        // and is not in scope.
        var assembly = typeof(FilterConfig).Assembly;
        const BindingFlags Public = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static
                                    | BindingFlags.DeclaredOnly;

        var exported = assembly.GetExportedTypes();
        var offenders = exported.SelectMany(type =>
                type.GetProperties(Public).Select(p => (Type: type, Member: p.Name, Declared: p.PropertyType))
                    .Concat(type.GetFields(Public).Select(f => (Type: type, Member: f.Name, Declared: f.FieldType)))
                    .Concat(type.GetMethods(Public).Where(m => !m.IsSpecialName)
                        .Select(m => (Type: type, Member: m.Name, Declared: m.ReturnType))))
            .Where(m => m.Declared.IsArray)
            .Select(m => $"{m.Type.Name}.{m.Member}")
            .ToList();

        exported.Should().Contain(typeof(BoardPattern), "the sweep must reach the library's public types");
        offenders.Should().BeEmpty();
    }
}
