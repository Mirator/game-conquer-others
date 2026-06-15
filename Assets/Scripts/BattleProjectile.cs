using UnityEngine;

public sealed class BattleProjectile : MonoBehaviour
{
    private BattleManager battle;
    private BattleFighter attacker;
    private Vector3 velocity;
    private float damage;
    private float lifetime = 5f;
    private Vector3 previousPosition;

    public void Configure(BattleManager manager, BattleFighter source, Vector3 direction, float hitDamage)
    {
        battle = manager;
        attacker = source;
        damage = hitDamage;
        velocity = direction.normalized * 34f;
        previousPosition = transform.position;
        BuildArrow();
    }

    private void Update()
    {
        if (battle == null || attacker == null || !battle.IsBattleRunning)
        {
            Destroy(gameObject);
            return;
        }

        lifetime -= Time.deltaTime;
        if (lifetime <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        velocity += Physics.gravity * 0.2f * Time.deltaTime;
        Vector3 next = transform.position + velocity * Time.deltaTime;
        BattleFighter target = battle.FindProjectileTarget(attacker, previousPosition, next, 0.45f);
        if (target != null)
        {
            battle.ReportProjectileHit();
            battle.ReportArrowImpact(next, true);
            target.ReceiveProjectileHit(damage, attacker);
            Embed(next, target.transform, 2.4f);
            return;
        }

        Vector3 travel = next - previousPosition;
        int ignoreRaycast = LayerMask.NameToLayer("Ignore Raycast");
        int mask = ignoreRaycast >= 0 ? ~(1 << ignoreRaycast) : ~0;
        if (travel.sqrMagnitude > 0.0001f
            && Physics.Raycast(previousPosition, travel.normalized, out RaycastHit hit, travel.magnitude, mask, QueryTriggerInteraction.Ignore))
        {
            battle.ReportArrowImpact(hit.point, false);
            Embed(hit.point + velocity.normalized * 0.08f, hit.collider.transform, 4f);
            return;
        }

        transform.position = next;
        transform.rotation = Quaternion.LookRotation(velocity.normalized);
        previousPosition = next;
    }

    private void Embed(Vector3 position, Transform parent, float duration)
    {
        transform.position = position;
        transform.rotation = Quaternion.LookRotation(velocity.normalized);
        transform.SetParent(parent, true);
        enabled = false;
        Destroy(gameObject, duration);
    }

    private void BuildArrow()
    {
        GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shaft.name = "Arrow Shaft";
        shaft.transform.SetParent(transform, false);
        shaft.transform.localPosition = Vector3.forward * 0.38f;
        shaft.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        shaft.transform.localScale = new Vector3(0.025f, 0.42f, 0.025f);
        Destroy(shaft.GetComponent<Collider>());
        shaft.GetComponent<Renderer>().sharedMaterial = RuntimeAssets.Material(new Color(0.32f, 0.16f, 0.05f));

        GameObject tip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tip.name = "Arrow Tip";
        tip.transform.SetParent(transform, false);
        tip.transform.localPosition = Vector3.forward * 0.84f;
        tip.transform.localScale = new Vector3(0.08f, 0.08f, 0.15f);
        Destroy(tip.GetComponent<Collider>());
        tip.GetComponent<Renderer>().sharedMaterial = RuntimeAssets.Material(new Color(0.58f, 0.62f, 0.66f));

        CreateFletching("Arrow Fletching H", new Vector3(0f, 0f, -0.05f), new Vector3(0.18f, 0.025f, 0.22f));
        CreateFletching("Arrow Fletching V", new Vector3(0f, 0f, -0.05f), new Vector3(0.025f, 0.18f, 0.22f));
    }

    private void CreateFletching(string partName, Vector3 position, Vector3 scale)
    {
        GameObject feather = GameObject.CreatePrimitive(PrimitiveType.Cube);
        feather.name = partName;
        feather.transform.SetParent(transform, false);
        feather.transform.localPosition = position;
        feather.transform.localScale = scale;
        Destroy(feather.GetComponent<Collider>());
        Color color = attacker.Team == Team.Allies ? new Color(0.16f, 0.42f, 0.9f) : new Color(0.84f, 0.14f, 0.08f);
        feather.GetComponent<Renderer>().sharedMaterial = RuntimeAssets.Material(color);
    }
}
