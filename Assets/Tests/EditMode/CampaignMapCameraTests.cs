using NUnit.Framework;
using UnityEngine;

// The pure framing math behind the campaign-map camera, extracted from
// CampaignMapController so it can be verified without a live camera: focusing puts
// the camera at the requested height over a point, and the table clamp keeps the
// point under the screen centre inside the map bounds.
public sealed class CampaignMapCameraTests
{
    // The map camera looks down at a fixed 62-degree pitch.
    private static readonly Vector3 Forward = Quaternion.Euler(62f, 0f, 0f) * Vector3.forward;

    // Where the downward camera's view axis meets the y=0 plane (screen-centre point).
    private static Vector3 Center(Vector3 position)
        => position + Forward * (position.y / Mathf.Max(-Forward.y, 0.01f));

    [Test]
    public void Focus_PlacesCameraAtRequestedHeight()
    {
        Vector3 ground = new Vector3(12f, 0.2f, -7f);
        Vector3 pos = CampaignMapCamera.FocusPosition(ground, Forward, 22f);
        Assert.AreEqual(22f, pos.y, 1e-3f, "camera should sit at the requested height");
    }

    [Test]
    public void Focus_FramesTheRequestedPoint()
    {
        // After focusing, sliding back down the view axis to the ground height returns
        // the original point — i.e. it is centred in view.
        Vector3 ground = new Vector3(12f, 0.2f, -7f);
        Vector3 pos = CampaignMapCamera.FocusPosition(ground, Forward, 22f);
        float back = (22f - ground.y) / -Forward.y;
        Vector3 framed = pos + Forward * back;
        Assert.AreEqual(ground.x, framed.x, 1e-3f);
        Assert.AreEqual(ground.z, framed.z, 1e-3f);
        Assert.AreEqual(ground.y, framed.y, 1e-3f);
    }

    [Test]
    public void ClampToTable_LeavesAnInBoundsViewUntouched()
    {
        // A camera whose screen-centre is already well inside the table is unchanged.
        Vector3 inBounds = CampaignMapCamera.FocusPosition(new Vector3(10f, 0f, 5f), Forward, 30f);
        Vector3 result = CampaignMapCamera.ClampToTable(inBounds, Forward);
        Assert.AreEqual(inBounds.x, result.x, 1e-3f);
        Assert.AreEqual(inBounds.y, result.y, 1e-3f);
        Assert.AreEqual(inBounds.z, result.z, 1e-3f);
    }

    [Test]
    public void ClampToTable_PullsAnOutOfBoundsViewBackToTheLimit()
    {
        // Screen-centre far past the +x edge: the clamp should pull it back to the
        // limit while preserving the camera height.
        Vector3 pos = new Vector3(100f, 30f, 0f);
        Assert.Greater(Center(pos).x, 40f, "test precondition: starts out of bounds");

        Vector3 result = CampaignMapCamera.ClampToTable(pos, Forward);
        Vector3 center = Center(result);
        Assert.AreEqual(40f, center.x, 1e-2f, "centre should be clamped to the +x table limit");
        Assert.GreaterOrEqual(center.z, -31f - 1e-2f);
        Assert.LessOrEqual(center.z, 35f + 1e-2f);
        Assert.AreEqual(30f, result.y, 1e-3f, "clamp should not change the camera height");
    }

    [Test]
    public void ClampToTable_ClampsTheZAxisToo()
    {
        Vector3 pos = new Vector3(0f, 30f, -200f);
        Assert.Less(Center(pos).z, -31f, "test precondition: starts past the -z edge");
        Vector3 center = Center(CampaignMapCamera.ClampToTable(pos, Forward));
        Assert.AreEqual(-31f, center.z, 1e-2f, "centre should be clamped to the -z table limit");
    }
}
