using NUnit.Framework;

// The seeded RNG and the pure AI offense decisions extracted from AIFighter. Driving
// them with a DeterministicRng (instead of UnityEngine.Random) makes AI personality —
// feinting a guard, punishing recovery, mis-guarding — reproducible and testable.
public sealed class AIDecisionsTests
{
    // --- DeterministicRng ---------------------------------------------------

    [Test]
    public void Rng_SameSeed_ProducesSameSequence()
    {
        DeterministicRng a = new DeterministicRng(1234);
        DeterministicRng b = new DeterministicRng(1234);
        for (int i = 0; i < 200; i++)
            Assert.AreEqual(a.Value, b.Value, 1e-7f, $"divergence at draw {i}");
    }

    [Test]
    public void Rng_DifferentSeeds_Diverge()
    {
        DeterministicRng a = new DeterministicRng(1);
        DeterministicRng b = new DeterministicRng(2);
        bool diverged = false;
        for (int i = 0; i < 50 && !diverged; i++)
            if (a.Value != b.Value)
                diverged = true;
        Assert.IsTrue(diverged, "two different seeds produced identical sequences");
    }

    [Test]
    public void Rng_ZeroSeed_IsNotStuck()
    {
        // 0 is a fixed point of xorshift; the ctor must fold it so it still advances.
        DeterministicRng rng = new DeterministicRng(0);
        bool nonZero = false;
        for (int i = 0; i < 10 && !nonZero; i++)
            if (rng.Value > 0f)
                nonZero = true;
        Assert.IsTrue(nonZero, "zero-seeded RNG stayed at 0");
    }

    [Test]
    public void Rng_Value_StaysInUnitInterval()
    {
        DeterministicRng rng = new DeterministicRng(99);
        for (int i = 0; i < 5000; i++)
        {
            float v = rng.Value;
            Assert.GreaterOrEqual(v, 0f);
            Assert.Less(v, 1f);
        }
    }

    [Test]
    public void Rng_FloatRange_StaysWithinBounds()
    {
        DeterministicRng rng = new DeterministicRng(7);
        for (int i = 0; i < 5000; i++)
        {
            float v = rng.Range(2.15f, 2.55f);
            Assert.GreaterOrEqual(v, 2.15f);
            Assert.Less(v, 2.55f);
        }
    }

    [Test]
    public void Rng_IntRange_CoversAndStaysInRange()
    {
        DeterministicRng rng = new DeterministicRng(42);
        bool[] seen = new bool[4];
        for (int i = 0; i < 2000; i++)
        {
            int v = rng.Range(0, 4);
            Assert.GreaterOrEqual(v, 0);
            Assert.Less(v, 4);
            seen[v] = true;
        }
        Assert.IsTrue(seen[0] && seen[1] && seen[2] && seen[3], "int range did not cover every value");
    }

    [Test]
    public void Rng_IntRange_EmptyReturnsMin()
    {
        DeterministicRng rng = new DeterministicRng(3);
        Assert.AreEqual(5, rng.Range(5, 5));
        Assert.AreEqual(5, rng.Range(5, 4));
    }

    // --- AIDecisions -------------------------------------------------------

    [Test]
    public void RandomWrongDirection_NeverReturnsIncoming()
    {
        DeterministicRng rng = new DeterministicRng(11);
        for (int i = 0; i < 4000; i++)
        {
            CombatDirection incoming = (CombatDirection)(i % 4);
            Assert.AreNotEqual(incoming, AIDecisions.RandomWrongDirection(incoming, rng));
        }
    }

    [Test]
    public void RandomDirection_OnlyYieldsTheFourLines()
    {
        DeterministicRng rng = new DeterministicRng(5);
        for (int i = 0; i < 2000; i++)
        {
            CombatDirection d = AIDecisions.RandomDirection(rng);
            Assert.IsTrue(d == CombatDirection.Left || d == CombatDirection.Right
                || d == CombatDirection.Up || d == CombatDirection.Thrust);
        }
    }

    [Test]
    public void RandomPunishDirection_FavoursOverheadButMixesLines()
    {
        DeterministicRng rng = new DeterministicRng(8);
        int up = 0, other = 0;
        for (int i = 0; i < 4000; i++)
        {
            if (AIDecisions.RandomPunishDirection(rng) == CombatDirection.Up)
                up++;
            else
                other++;
        }
        Assert.Greater(up, other, "overhead should be the most common punish");
        Assert.Greater(other, 0, "punish should still mix in other lines");
    }

    [Test]
    public void ChooseAttackDirection_AlwaysFeintsBlockingTarget_WhenFeintCertain()
    {
        AIProfile profile = new AIProfile { feintChance = 1f };
        DeterministicRng rng = new DeterministicRng(21);
        for (int i = 0; i < 1000; i++)
        {
            CombatDirection guard = (CombatDirection)(i % 4);
            // Target blocking + certain feint => must strike a line other than the guard.
            CombatDirection chosen = AIDecisions.ChooseAttackDirection(profile, true, guard, false, rng);
            Assert.AreNotEqual(guard, chosen);
        }
    }

    [Test]
    public void ChooseAttackDirection_NeverFeints_WhenFeintImpossible()
    {
        // feint=0 and not recovering: just a plain random line; over many tries it must
        // eventually equal the guard line (i.e. it is NOT avoiding it).
        AIProfile profile = new AIProfile { feintChance = 0f, recoveryPunishChance = 0f };
        DeterministicRng rng = new DeterministicRng(33);
        bool matchedGuard = false;
        for (int i = 0; i < 1000 && !matchedGuard; i++)
            if (AIDecisions.ChooseAttackDirection(profile, true, CombatDirection.Up, false, rng) == CombatDirection.Up)
                matchedGuard = true;
        Assert.IsTrue(matchedGuard, "with feint disabled the attacker should not avoid the guard line");
    }

    [Test]
    public void ChooseAttackDirection_PunishesRecovery_WhenCertain()
    {
        // Not blocking, recovering, certain punish => only punish lines (Up/Thrust/Left/Right,
        // i.e. never excludes any but is drawn from the punish distribution). Assert it is
        // overwhelmingly overhead-led, which only the punish path produces.
        AIProfile profile = new AIProfile { feintChance = 0f, recoveryPunishChance = 1f };
        DeterministicRng rng = new DeterministicRng(44);
        int up = 0;
        for (int i = 0; i < 2000; i++)
            if (AIDecisions.ChooseAttackDirection(profile, false, CombatDirection.Right, true, rng) == CombatDirection.Up)
                up++;
        // Punish path yields Up ~50% of the time; a plain random line would be ~25%.
        Assert.Greater(up, 800, "recovery punish should be overhead-led");
    }
}
