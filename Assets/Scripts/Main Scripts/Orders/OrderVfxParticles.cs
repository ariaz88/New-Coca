using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The stars and dust of the order effects, simulated as one flat array.
///
/// Why not a coroutine per particle
/// --------------------------------
/// The previous implementation started a coroutine for every sparkle. One flight
/// sheds around fifty, and packing several boxes in a row put well over a hundred
/// coroutines on the scheduler at once, each allocating its own iterator and each
/// paying a managed-to-native transition per frame. This class runs the whole
/// simulation in a single loop over a struct array, so a flight costs one Update
/// regardless of how many particles are alive.
///
/// Why not a ParticleSystem
/// ------------------------
/// Same reason as the ribbon: a ParticleSystem does not render inside a screen-space
/// Canvas, and this effect has to sort with the Orders card and land on a UI icon.
/// Pooled Images give the same look and sort correctly for free.
///
/// Nothing here allocates once the array and the Image pool have warmed up.
/// </summary>
[DisallowMultipleComponent]
public sealed class OrderVfxParticles : MonoBehaviour
{
    private struct Particle
    {
        public Image Image;
        public Vector2 Origin;
        public Vector2 Velocity;
        public Vector2 Noise;
        public Color Color;
        public float Size;
        public float Lifetime;
        public float Age;
        public float Rotation;
        public float Spin;
        public float GrowFraction;
        public float FadeStart;
    }

    private Particle[] particles = new Particle[96];
    private int liveCount;

    private OrderVfxPool pool;

    /// <summary>Number of particles currently being simulated. Exposed for tests.</summary>
    public int LiveCount => liveCount;

    public void Initialize(OrderVfxPool vfxPool)
    {
        pool = vfxPool;
    }

    /// <summary>
    /// Adds one particle. <paramref name="velocity"/> is in canvas units per second
    /// at birth; particles decelerate over their life rather than travelling at a
    /// constant speed, which is what makes them settle instead of scattering.
    /// </summary>
    public void Spawn(
        Sprite sprite,
        Vector2 position,
        Vector2 velocity,
        Color color,
        float size,
        float lifetime,
        float spin = 0f,
        float noise = 0f,
        float growFraction = 0.15f,
        float fadeStart = 0.55f)
    {
        if (pool == null || sprite == null || lifetime <= 0f)
        {
            return;
        }

        if (liveCount >= particles.Length)
        {
            // Doubles at most a couple of times in a session and then never again,
            // so the steady state is still allocation-free.
            System.Array.Resize(ref particles, particles.Length * 2);
        }

        Image image = pool.RentImage();
        image.sprite = sprite;
        image.color = color;

        RectTransform rect = image.rectTransform;
        rect.sizeDelta = new Vector2(size, size);
        rect.anchoredPosition = position;
        rect.localScale = Vector3.zero;

        float startRotation = Random.Range(0f, 360f);
        rect.localRotation = Quaternion.Euler(0f, 0f, startRotation);
        image.gameObject.SetActive(true);

        particles[liveCount] = new Particle
        {
            Image = image,
            Origin = position,
            Velocity = velocity,
            Noise = noise <= 0f
                ? Vector2.zero
                : new Vector2(Random.Range(-noise, noise), Random.Range(-noise, noise)),
            Color = color,
            Size = size,
            Lifetime = lifetime,
            Age = 0f,
            Rotation = startRotation,
            Spin = spin,
            GrowFraction = Mathf.Clamp(growFraction, 0.01f, 0.9f),
            FadeStart = Mathf.Clamp01(fadeStart)
        };

        liveCount++;
    }

    private void Update()
    {
        if (liveCount == 0)
        {
            return;
        }

        // Unscaled, matching every other part of this effect. If the last packed box
        // completes the level and something freezes timeScale, these must still
        // finish and release their pooled Images.
        float delta = Time.unscaledDeltaTime;

        for (int index = liveCount - 1; index >= 0; index--)
        {
            ref Particle particle = ref particles[index];
            particle.Age += delta;

            float t = particle.Age / particle.Lifetime;
            if (t >= 1f || particle.Image == null)
            {
                Retire(index);
                continue;
            }

            RectTransform rect = particle.Image.rectTransform;

            // Decelerating drift. EaseOutCubic on the distance travelled means the
            // particle leaves quickly and settles, rather than sliding at a
            // constant speed for its whole life.
            float travel = EaseOutCubic(t) * particle.Lifetime;
            rect.anchoredPosition =
                particle.Origin + particle.Velocity * travel + particle.Noise * t;

            particle.Rotation += particle.Spin * delta;
            rect.localRotation = Quaternion.Euler(0f, 0f, particle.Rotation);

            // Pops to full size, holds, then shrinks smoothly to nothing. A
            // constant size makes the trail switch off rather than dissolve.
            float grow = Mathf.Clamp01(t / particle.GrowFraction);
            grow = grow * grow * (3f - 2f * grow);

            float shrink = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.35f) / 0.65f));
            float scale = grow * Mathf.Max(shrink, 0f);
            rect.localScale = new Vector3(scale, scale, 1f);

            float alpha = particle.FadeStart >= 1f
                ? 1f
                : 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - particle.FadeStart) / (1f - particle.FadeStart)));

            Color color = particle.Color;
            color.a *= alpha;
            particle.Image.color = color;
        }
    }

    /// <summary>
    /// Returns every live particle to the pool immediately. Used when the director
    /// is disabled mid-flight, so pooled Images are never stranded active.
    /// </summary>
    public void ClearAll()
    {
        for (int index = liveCount - 1; index >= 0; index--)
        {
            Retire(index);
        }
    }

    private void OnDisable()
    {
        ClearAll();
    }

    /// <summary>
    /// Releases one particle by swapping the last live entry into its slot. Order
    /// is irrelevant here, so swap-remove avoids shifting the whole array.
    /// </summary>
    private void Retire(int index)
    {
        pool?.ReturnImage(particles[index].Image);
        particles[index].Image = null;

        liveCount--;
        if (index != liveCount)
        {
            particles[index] = particles[liveCount];
        }

        particles[liveCount].Image = null;
    }

    private static float EaseOutCubic(float t)
    {
        float inverse = 1f - t;
        return 1f - inverse * inverse * inverse;
    }
}
