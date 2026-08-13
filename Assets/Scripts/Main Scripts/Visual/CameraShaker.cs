using Cinemachine;
using UnityEngine;

/// <summary>
/// Screen shake for the one camera this game has.
///
/// It has to go through Cinemachine. The Main Camera carries a CinemachineBrain,
/// which overwrites the camera transform every LateUpdate, so the obvious
/// approach - DOShakePosition on the camera - is silently a no-op: the tween runs,
/// the transform moves, and the brain puts it back before anything is drawn.
///
/// Both halves of the impulse system are wired up here rather than in the scene.
/// The 25 level scenes were baked before this existed, and a listener added to a
/// prefab or a scene by hand would have to be added twenty-five more times; doing
/// it in code means the first shake of the first level sets itself up.
/// </summary>
public static class CameraShaker
{
    private const string SourceObjectName = "~CameraImpulseSource";

    private static CinemachineImpulseSource source;

    /// <summary>
    /// Fires one shake. Amplitude is a multiplier on the configured default, so
    /// callers can say "half a shake" without knowing the units.
    /// </summary>
    public static void Shake(float amplitude = 1f)
    {
        if (amplitude <= 0f)
        {
            return;
        }

        CinemachineImpulseSource impulseSource = ResolveSource();
        if (impulseSource == null)
        {
            return;
        }

        EnsureListener();
        impulseSource.GenerateImpulseWithVelocity(Random.insideUnitSphere.normalized * amplitude);
    }

    /// <summary>
    /// Dropped between scenes. The source lives in the level scene, so the static
    /// reference is stale the moment a new level loads and has to be rebuilt.
    /// </summary>
    private static CinemachineImpulseSource ResolveSource()
    {
        if (source != null)
        {
            return source;
        }

        GameObject host = GameObject.Find(SourceObjectName);
        if (host == null)
        {
            host = new GameObject(SourceObjectName);
        }

        source = host.GetComponent<CinemachineImpulseSource>();
        if (source == null)
        {
            source = host.AddComponent<CinemachineImpulseSource>();
        }

        // A short, sharp, decaying bump. The default profile is a long rumble
        // tuned for explosions in a 3D world; at this camera distance it reads as
        // the whole board sliding rather than as an impact.
        source.m_ImpulseDefinition.m_ImpulseDuration = 0.35f;
        source.m_ImpulseDefinition.m_ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Explosion;
        source.m_ImpulseDefinition.m_AmplitudeGain = 0.18f;
        source.m_ImpulseDefinition.m_FrequencyGain = 0.6f;
        source.m_DefaultVelocity = Vector3.one;

        return source;
    }

    /// <summary>
    /// An impulse with nothing listening moves nothing, and the listener is an
    /// extension on the virtual camera rather than on the brain.
    /// </summary>
    private static void EnsureListener()
    {
        CinemachineBrain brain = Camera.main != null
            ? Camera.main.GetComponent<CinemachineBrain>()
            : null;
        if (brain == null)
        {
            return;
        }

        ICinemachineCamera active = brain.ActiveVirtualCamera;
        CinemachineVirtualCameraBase vcam = active?.VirtualCameraGameObject != null
            ? active.VirtualCameraGameObject.GetComponent<CinemachineVirtualCameraBase>()
            : null;
        if (vcam == null)
        {
            return;
        }

        if (vcam.GetComponent<CinemachineImpulseListener>() == null)
        {
            vcam.gameObject.AddComponent<CinemachineImpulseListener>();
        }
    }
}
