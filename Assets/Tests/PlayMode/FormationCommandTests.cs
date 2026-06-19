using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Order/formation behaviour that needs real fighters: stable slot assignment, the
// Advance anchor sitting ahead of the captain, Shield Wall tightening the line, and
// Charge releasing the formation. FormationBalance is pinned to baked defaults so
// the geometry assertions are independent of any Resources asset. (Hold-Fire is
// covered end-to-end by the -smokecommands runtime smoke, which can wait real time
// for an archer to draw and loose.)
public sealed class FormationCommandTests
{
    [SetUp]
    public void Pin() => FormationBalance.Override(ScriptableObject.CreateInstance<FormationBalanceData>());

    [TearDown]
    public void Reset() => FormationBalance.Override(null);

    [UnityTest]
    public IEnumerator Formation_AssignsStableUniqueSlotToEachAlly()
    {
        GameObject root = new("Formation Slots");
        BattleManager manager = CreateManager(root);
        CreateFighter<PlayerFighter>(root, manager, Team.Allies, true, Vector3.zero);
        AIFighter[] allies = CreateAllies(root, manager, 5);

        Vector3[] first = SlotsOf(manager, allies);
        Vector3[] second = SlotsOf(manager, allies);
        for (int i = 0; i < allies.Length; i++)
        {
            Assert.That(second[i], Is.EqualTo(first[i]), $"ally {i} slot is stable across calls");
            for (int j = i + 1; j < allies.Length; j++)
                Assert.That(Vector3.Distance(first[i], first[j]), Is.GreaterThan(0.5f),
                    $"allies {i} and {j} occupy distinct slots");
        }
        yield return DestroyRoot(root);
    }

    [UnityTest]
    public IEnumerator Advance_PlacesTheLineAheadOfTheCaptain()
    {
        GameObject root = new("Formation Advance");
        BattleManager manager = CreateManager(root);
        PlayerFighter player = CreateFighter<PlayerFighter>(root, manager, Team.Allies, true, Vector3.zero);
        AIFighter[] allies = CreateAllies(root, manager, 5);

        manager.SetAllyCommand(BattleManager.AllyCommand.Advance);
        foreach (AIFighter ally in allies)
            Assert.That(manager.DebugFormationPosition(ally).z, Is.GreaterThan(player.transform.position.z + 3f),
                "advance forms the line well ahead of the captain");
        yield return DestroyRoot(root);
    }

    [UnityTest]
    public IEnumerator ShieldWall_TightensTheLineVersusLine()
    {
        GameObject root = new("Formation Shapes");
        BattleManager manager = CreateManager(root);
        CreateFighter<PlayerFighter>(root, manager, Team.Allies, true, Vector3.zero);
        AIFighter[] allies = CreateAllies(root, manager, 6);

        float lineSpan = HorizontalSpan(manager, allies); // defaults: Follow / Line
        manager.CycleFormation();
        Assert.That(manager.CurrentFormation, Is.EqualTo(FormationShape.ShieldWall));
        float wallSpan = HorizontalSpan(manager, allies);
        Assert.That(wallSpan, Is.LessThan(lineSpan), "shield wall is tighter than the line");
        yield return DestroyRoot(root);
    }

    [UnityTest]
    public IEnumerator Charge_ReleasesFormation()
    {
        GameObject root = new("Formation Charge");
        BattleManager manager = CreateManager(root);
        CreateFighter<PlayerFighter>(root, manager, Team.Allies, true, Vector3.zero);
        AIFighter ally = CreateAllies(root, manager, 1)[0];

        manager.SetAllyCommand(BattleManager.AllyCommand.Follow);
        Assert.That(manager.TryGetCommandPosition(ally, null, out _), Is.True, "follow holds formation");
        manager.SetAllyCommand(BattleManager.AllyCommand.Charge);
        Assert.That(manager.TryGetCommandPosition(ally, null, out _), Is.False, "charge releases formation");
        yield return DestroyRoot(root);
    }

    private static Vector3[] SlotsOf(BattleManager manager, AIFighter[] allies)
    {
        Vector3[] slots = new Vector3[allies.Length];
        for (int i = 0; i < allies.Length; i++)
            slots[i] = manager.DebugFormationPosition(allies[i]);
        return slots;
    }

    private static float HorizontalSpan(BattleManager manager, AIFighter[] allies)
    {
        float min = float.MaxValue, max = float.MinValue;
        foreach (AIFighter ally in allies)
        {
            float x = manager.DebugFormationPosition(ally).x;
            min = Mathf.Min(min, x);
            max = Mathf.Max(max, x);
        }
        return max - min;
    }

    private static AIFighter[] CreateAllies(GameObject root, BattleManager manager, int count)
    {
        AIFighter[] allies = new AIFighter[count];
        for (int i = 0; i < count; i++)
            allies[i] = CreateFighter<AIFighter>(root, manager, Team.Allies, false, new Vector3(i * 0.6f, 0f, -3f));
        return allies;
    }

    private static BattleManager CreateManager(GameObject root)
    {
        BattleManager manager = root.AddComponent<BattleManager>();
        manager.Configure(null, null);
        return manager;
    }

    private static T CreateFighter<T>(GameObject root, BattleManager manager, Team team, bool player, Vector3 position)
        where T : BattleFighter
    {
        GameObject go = new(typeof(T).Name);
        go.transform.SetParent(root.transform);
        go.transform.position = position;
        T fighter = go.AddComponent<T>();
        fighter.Configure(manager, team, player);
        manager.Register(fighter);
        return fighter;
    }

    private static IEnumerator DestroyRoot(GameObject root)
    {
        Object.Destroy(root);
        yield return null;
    }
}
