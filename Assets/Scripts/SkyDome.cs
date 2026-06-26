using UnityEngine;

// Keeps sky dressing (stars, moon, clouds) centred on the camera so it always reads
// as the far sky — it follows the camera's position but never its rotation, so the
// stars stay world-fixed overhead while the player moves around the field.
public sealed class SkyDome : MonoBehaviour
{
    private Transform follow;

    public void Configure(Transform cameraTransform) => follow = cameraTransform;

    private void LateUpdate()
    {
        if (follow != null)
            transform.position = follow.position;
    }
}
