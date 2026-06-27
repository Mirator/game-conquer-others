using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// Pure-logic guards on the terrain/scatter helpers extracted into ArenaBuilder: the
// height field must stay flat across the playable footprint (so combat, spawns, and
// formations are unaffected) and stay bounded at the rim, and scattered points must
// never escape the field clamp. These run without any GameObject lifecycle.
public sealed class ArenaBuilderTests
{
    // Inside the playable footprint (max(|x|/HalfWidth, |z|/HalfDepth) <= 1.1) the
    // height must be exactly 0 — anything else would tilt the combat plane.
    [TestCase(0f, 0f)]
    [TestCase(20f, 20f)]
    [TestCase(ArenaMetrics.HalfWidth, ArenaMetrics.HalfDepth)] // corner of the footprint
    [TestCase(-ArenaMetrics.HalfWidth, 0f)]
    [TestCase(0f, -ArenaMetrics.HalfDepth)]
    public void TerrainHeight_IsFlat_AcrossPlayableCore(float x, float z)
    {
        Assert.AreEqual(0f, ArenaBuilder.TerrainHeight(x, z), 1e-4f);
    }

    [Test]
    public void TerrainHeight_RemainsFlat_UpToRimThreshold()
    {
        // r = 1.1 is the last flat ring; just inside it the smoothstep is still 0.
        float x = ArenaMetrics.HalfWidth * 1.05f;
        float z = ArenaMetrics.HalfDepth * 1.05f;
        Assert.AreEqual(0f, ArenaBuilder.TerrainHeight(x, z), 1e-4f);
    }

    [Test]
    public void TerrainHeight_IsBounded_AcrossTheWholeField()
    {
        // Sweep well past the rim into the rolling hills: amplitude is capped at 7.
        for (float x = -120f; x <= 120f; x += 3f)
            for (float z = -120f; z <= 120f; z += 3f)
                Assert.LessOrEqual(Mathf.Abs(ArenaBuilder.TerrainHeight(x, z)), 7f,
                    $"height out of bounds at ({x},{z})");
    }

    [Test]
    public void TerrainHeight_RisesBeyondTheRim()
    {
        // Far outside the footprint the field should actually undulate (not stay flat),
        // otherwise the "rolling hills toward the horizon" contract is broken.
        bool anyRelief = false;
        for (float x = 50f; x <= 110f && !anyRelief; x += 2f)
            for (float z = 50f; z <= 110f; z += 2f)
                if (Mathf.Abs(ArenaBuilder.TerrainHeight(x, z)) > 0.25f)
                {
                    anyRelief = true;
                    break;
                }
        Assert.IsTrue(anyRelief, "terrain is flat even far beyond the rim");
    }

    [Test]
    public void GroundHeightAt_MatchesTerrainHeight()
    {
        Assert.AreEqual(ArenaBuilder.TerrainHeight(33f, -41f), ArenaBuilder.GroundHeightAt(33f, -41f), 1e-6f);
    }

    [Test]
    public void ClusterCenters_StayWithinBounds()
    {
        const float hw = 26f;
        const float hd = 30f;
        List<Vector2> centers = ArenaBuilder.ClusterCenters(200, hw, hd);
        Assert.AreEqual(200, centers.Count);
        foreach (Vector2 c in centers)
        {
            Assert.LessOrEqual(Mathf.Abs(c.x), hw);
            Assert.LessOrEqual(Mathf.Abs(c.y), hd);
        }
    }

    [Test]
    public void ClusteredPoint_IsClampedToTheField()
    {
        const float hw = 26f;
        const float hd = 30f;
        // Centres on the edge + a large radius would overshoot without the clamp.
        List<Vector2> centers = new() { new Vector2(hw, hd), new Vector2(-hw, -hd), Vector2.zero };
        for (int i = 0; i < 2000; i++)
        {
            Vector3 p = ArenaBuilder.ClusteredPoint(centers, 12f, hw, hd);
            Assert.LessOrEqual(Mathf.Abs(p.x), hw + 1e-4f, $"x escaped at iteration {i}");
            Assert.LessOrEqual(Mathf.Abs(p.z), hd + 1e-4f, $"z escaped at iteration {i}");
            Assert.AreEqual(0f, p.y, 1e-6f); // y is left for the caller to sample onto terrain
        }
    }
}
