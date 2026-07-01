using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Guards the reduced-motion contract for the combat-juice camera feedback: a heavy
// impulse zooms the follow camera in, but reduced motion must suppress that punch.
public sealed class CameraFeedbackTests
{
    [UnityTest]
    public IEnumerator ReduceMotion_SuppressesFovPunch()
    {
        float zoomedFov = 0f;
        float reducedFov = 0f;

        SettingsService.ResetForTests();
        SettingsService.Current.cameraShake = 1f;
        SettingsService.Current.reduceMotion = false;
        SettingsService.SaveAndApply();
        yield return MeasureImpulseFov(value => zoomedFov = value);

        SettingsService.ResetForTests();
        SettingsService.Current.cameraShake = 1f;
        SettingsService.Current.reduceMotion = true;
        SettingsService.SaveAndApply();
        yield return MeasureImpulseFov(value => reducedFov = value);

        SettingsService.ResetForTests();
        // Without reduced motion the impulse zooms in (lower FOV); with it, the FOV
        // stays at the stance-driven base.
        Assert.That(reducedFov, Is.GreaterThan(zoomedFov + 1f),
            "Reduced motion should suppress the FOV punch that a normal impulse applies.");
    }

    private static IEnumerator MeasureImpulseFov(System.Action<float> report)
    {
        GameObject camObject = new GameObject("Feedback Test Camera");
        Camera camera = camObject.AddComponent<Camera>();
        camera.fieldOfView = 57f;
        ThirdPersonCamera rig = camObject.AddComponent<ThirdPersonCamera>();
        GameObject target = new GameObject("Feedback Test Target");
        rig.SetTarget(target.transform);
        rig.AddImpulse(6f, 0f, Vector3.forward);
        yield return null; // one LateUpdate applies the punch
        report(camera.fieldOfView);
        Object.Destroy(camObject);
        Object.Destroy(target);
    }
}
