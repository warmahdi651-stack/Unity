using UnityEngine;

namespace RtsWarcraft
{
    // -----------------------------------------------------------------------
    //  Plays the animation clips embedded in the rigged FBX characters
    //  (Quaternius, CC0) with fuzzy name matching.  Walk/idle loop while the
    //  unit moves or stands, attacks play on every swing and the death clip
    //  plays once when the unit dies.  When a model carries no clips the
    //  component quietly disables itself and the unit just bobs.
    // -----------------------------------------------------------------------
    public class RtsAnimator : MonoBehaviour
    {
        Animation anim;
        string idleName, walkName, attackName, deathName;
        bool dead;
        bool walking;
        float attackUntil = -1f;

        public void Init(string modelName)
        {
            anim = GetComponentInChildren<Animation>();
            if (anim == null) anim = gameObject.AddComponent<Animation>();

            AnimationClip[] clips = Resources.LoadAll<AnimationClip>("Models/" + modelName);
            if (clips == null || clips.Length == 0)
            {
                enabled = false;
                return;
            }

            anim.cullingType = AnimationCullingType.AlwaysAnimate;
            for (int i = 0; i < clips.Length; i++)
            {
                AnimationClip c = clips[i];
                c.legacy = true;
                anim.AddClip(c, c.name);
                anim[c.name].wrapMode = WrapMode.Loop;
                string n = c.name.ToLower();
                if (deathName == null && (n.Contains("death") || n.Contains("die")))
                    deathName = c.name;
                else if (attackName == null && (n.Contains("attack") || n.Contains("slash") ||
                                                n.Contains("swing") || n.Contains("shoot")))
                    attackName = c.name;
                else if (walkName == null && (n.Contains("walk") || n.Contains("run")))
                    walkName = c.name;
                else if (idleName == null && n.Contains("idle"))
                    idleName = c.name;
            }

            if (walkName == null) walkName = clips[0].name;
            if (idleName == null) idleName = walkName;
            if (deathName == null) deathName = attackName;

            if (idleName != null) anim.Play(idleName);
        }

        public void SetWalking(bool w)
        {
            if (anim == null || dead || w == walking) return;
            walking = w;
            if (Time.time > attackUntil)
                anim.CrossFade(w ? walkName : idleName, 0.15f);
        }

        public void TriggerAttack()
        {
            if (anim == null || dead || attackName == null) return;
            attackUntil = Time.time + 0.4f;
            anim[attackName].wrapMode = WrapMode.Once;
            anim.Play(attackName);
        }

        public void PlayDeath()
        {
            if (anim == null || dead) return;
            dead = true;
            if (deathName != null)
            {
                anim[deathName].wrapMode = WrapMode.Once;
                anim.Play(deathName);
            }
        }

        void LateUpdate()
        {
            if (anim == null || dead) return;
            string want = walking ? walkName : idleName;
            if (want != null && Time.time > attackUntil && !anim.IsPlaying(want))
                anim.CrossFade(want, 0.2f);
        }
    }
}
