namespace XgFilter_Lib.Patterns;

/// <summary>
/// One condition of a <see cref="BoardPattern"/>, and the element type of its
/// <see cref="BoardPattern.Constraints"/>. The set is closed: its members are
/// internal, so no type outside this library can implement it, and the only
/// constraints are
/// <list type="bullet">
/// <item><description>
/// <see cref="CheckerRange"/> — a bound on the signed checker count at one
/// <see cref="CheckerLocation"/> (bracket token <c>[location,min,max]</c>);
/// </description></item>
/// <item><description>
/// <see cref="CheckerSpanRange"/> — a bound on one side's total checkers across
/// a <see cref="CheckerSpan"/> (bracket token <c>[a-b,min,max]</c>).
/// </description></item>
/// </list>
/// A consumer that needs the detail of a constraint pattern-matches on those
/// two types. Each is a validated, immutable value with structural equality,
/// which is what <see cref="BoardPattern"/>'s own equality delegates to, and
/// each renders its own bracket token through <see cref="object.ToString"/>.
/// </summary>
public interface IPatternConstraint
{
    /// <summary>
    /// The place this constraint addresses — its <see cref="CheckerLocation"/>
    /// or its <see cref="CheckerSpan"/>. <see cref="BoardPattern"/> refuses two
    /// constraints on the same place; places of different kinds never compare
    /// equal, so a location and a span never collide.
    /// </summary>
    internal object Place { get; }

    /// <summary>
    /// Tests whether <paramref name="board"/>, the on-roll-relative board
    /// array, satisfies this constraint.
    /// </summary>
    internal bool IsSatisfiedBy(IReadOnlyList<int> board);
}
