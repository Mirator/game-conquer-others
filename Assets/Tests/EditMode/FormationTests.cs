using NUnit.Framework;
using UnityEngine;

// Pure formation-geometry checks: slot offsets are deterministic and each shape
// has its intended footprint. FormationBalance is pinned to baked defaults so the
// assertions don't depend on a Resources asset that may or may not exist.
public sealed class FormationTests
{
    [SetUp]
    public void Pin() => FormationBalance.Override(ScriptableObject.CreateInstance<FormationBalanceData>());

    [TearDown]
    public void Reset() => FormationBalance.Override(null);

    [Test]
    public void SlotOffset_IsDeterministicPerIndex()
    {
        for (int i = 0; i < 12; i++)
        {
            Vector3 first = Formation.SlotLocalOffset(i, 12, FormationShape.Skirmish);
            Vector3 second = Formation.SlotLocalOffset(i, 12, FormationShape.Skirmish);
            Assert.That(second, Is.EqualTo(first), $"slot {i} must be stable across calls (incl. jitter)");
        }
    }

    [Test]
    public void Slots_AreUniqueAcrossAFullFormation()
    {
        const int count = 16;
        for (int a = 0; a < count; a++)
            for (int b = a + 1; b < count; b++)
            {
                Vector3 first = Formation.SlotLocalOffset(a, count, FormationShape.Line);
                Vector3 second = Formation.SlotLocalOffset(b, count, FormationShape.Line);
                Assert.That(Vector3.Distance(first, second), Is.GreaterThan(0.4f),
                    $"slots {a} and {b} must not coincide");
            }
    }

    [Test]
    public void Line_PutsTheFirstRankAheadAndCentersIt()
    {
        // The full first rank (width 6) should straddle the captain: side offsets sum
        // to ~0 and the rank sits ahead (positive depth).
        float sideSum = 0f;
        for (int i = 0; i < 6; i++)
        {
            Vector3 slot = Formation.SlotLocalOffset(i, 6, FormationShape.Line);
            sideSum += slot.x;
            Assert.That(slot.z, Is.GreaterThan(0f), $"rank-0 slot {i} should be ahead of the captain");
        }
        Assert.That(Mathf.Abs(sideSum), Is.LessThan(0.001f), "the front rank should be centered on the captain");
    }

    [Test]
    public void DeeperRanks_TrailBehindTheFront()
    {
        Vector3 front = Formation.SlotLocalOffset(0, 12, FormationShape.Line);
        Vector3 second = Formation.SlotLocalOffset(6, 12, FormationShape.Line); // first slot of rank 1
        Assert.That(second.z, Is.LessThan(front.z), "rank 1 should sit behind rank 0");
    }

    [Test]
    public void ShieldWall_IsTighterAndDeeperThanLine()
    {
        Assert.That(Span(FormationShape.ShieldWall), Is.LessThan(Span(FormationShape.Line)),
            "shield wall is narrower than the line");
        Assert.That(Depth(FormationShape.ShieldWall), Is.GreaterThan(Depth(FormationShape.Line)),
            "shield wall stacks deeper for the same count");
    }

    [Test]
    public void Skirmish_SpreadsWiderThanLine()
    {
        Assert.That(Span(FormationShape.Skirmish), Is.GreaterThan(Span(FormationShape.Line)),
            "skirmish spreads wider than the line");
    }

    // Horizontal extent of a 24-strong formation.
    private static float Span(FormationShape shape)
    {
        float min = float.MaxValue, max = float.MinValue;
        for (int i = 0; i < 24; i++)
        {
            float x = Formation.SlotLocalOffset(i, 24, shape).x;
            min = Mathf.Min(min, x);
            max = Mathf.Max(max, x);
        }
        return max - min;
    }

    // Front-to-back extent of a 24-strong formation.
    private static float Depth(FormationShape shape)
    {
        float min = float.MaxValue, max = float.MinValue;
        for (int i = 0; i < 24; i++)
        {
            float z = Formation.SlotLocalOffset(i, 24, shape).z;
            min = Mathf.Min(min, z);
            max = Mathf.Max(max, z);
        }
        return max - min;
    }
}
