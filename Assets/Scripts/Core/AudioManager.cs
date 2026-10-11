using UnityEngine;

namespace RtsWarcraft
{
    // -----------------------------------------------------------------------
    //  Runtime-synthesised sound effects.  Every cue is generated into an
    //  AudioClip at startup so the project ships without any audio assets,
    //  yet still has feedback for clicks, harvesting, combat, training and
    //  errors - a large part of what makes an RTS feel alive.
    // -----------------------------------------------------------------------
    public static class Sfx
    {
        static AudioSource src;
        static AudioClip click, chop, coin, hit, death, chime, error, swing, build;
        static float lastAt = -1f;

        public static void Init()
        {
            if (src != null) return;

            GameObject go = new GameObject("[Sfx]");
            Object.DontDestroyOnLoad(go);
            src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.volume = 0.55f;

            // Real recorded CC0 clips (Kenney / artisticdude) with the
            // synthesised versions as an offline fallback.
            click = Load("click") ?? Tone("click", 0.07f, t => Mathf.Sin(6280f * t) * 0.40f * Mathf.Exp(-t * 60f));
            chop  = Load("chop") ?? Noise("chop", 0.12f, 900f, 45f);
            coin  = Load("coin") ?? Tone("coin", 0.14f, t => (Mathf.Sin(7400f * t) * 0.5f + Mathf.Sin(9800f * t) * 0.25f) * Mathf.Exp(-t * 22f));
            hit   = Load("hit") ?? Noise("hit", 0.16f, 500f, 30f);
            death = Load("death") ?? Tone("death", 0.35f, t => Mathf.Sin((500f - 900f * t) * t * 6.28f) * 0.5f * Mathf.Exp(-t * 7f));
            chime = Load("chime") ?? Tone("chime", 0.50f, t => (Mathf.Sin(4140f * t) * 0.3f + Mathf.Sin(5540f * Mathf.Max(0f, t - 0.12f)) * 0.3f) * Mathf.Exp(-t * 5f));
            error = Load("error") ?? Tone("error", 0.20f, t => Mathf.Sign(Mathf.Sin(1000f * t)) * 0.18f * Mathf.Exp(-t * 9f));
            swing = Load("swing");
            build = Load("build");
        }

        static AudioClip Load(string name)
        {
            return Resources.Load<AudioClip>("Audio/" + name);
        }

        public static AudioClip Click { get { return click; } }
        public static AudioClip Chop  { get { return chop; } }
        public static AudioClip Coin  { get { return coin; } }
        public static AudioClip Hit   { get { return hit; } }
        public static AudioClip Death { get { return death; } }
        public static AudioClip Chime { get { return chime; } }
        public static AudioClip Error { get { return error; } }
        public static AudioClip Swing { get { return swing; } }
        public static AudioClip Build { get { return build; } }

        /// <summary>One-shots a cue with a tiny global throttle so battles do
        /// not stack into noise.</summary>
        public static void Play(AudioClip c)
        {
            if (src == null || c == null) return;
            if (Time.time - lastAt < 0.03f) return;
            lastAt = Time.time;
            src.PlayOneShot(c);
        }

        // ===================================================================

        static AudioClip Tone(string name, float len, System.Func<float, float> gen)
        {
            const int hz = 44100;
            int n = Mathf.Max(1, (int)(hz * len));
            float[] data = new float[n];
            for (int i = 0; i < n; i++) data[i] = gen(i / (float)hz);
            AudioClip clip = AudioClip.Create(name, n, 1, hz, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Filtered noise burst with a low thud - impacts.</summary>
        static AudioClip Noise(string name, float len, float cutoff, float decay)
        {
            const int hz = 44100;
            int n = Mathf.Max(1, (int)(hz * len));
            float[] data = new float[n];
            float prev = 0f;
            System.Random rnd = new System.Random(7);
            float smooth = Mathf.Clamp01(cutoff / 22050f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)hz;
                float white = (float)(rnd.NextDouble() * 2.0 - 1.0);
                prev = Mathf.Lerp(white, prev, 1f - smooth);
                data[i] = (white * 0.35f + prev * 0.65f) * Mathf.Exp(-t * decay) +
                          Mathf.Sin(300f * t) * 0.3f * Mathf.Exp(-t * 40f);
            }
            AudioClip clip = AudioClip.Create(name, n, 1, hz, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
