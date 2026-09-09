using System;
using UnityEngine;

namespace DariusModelAnimationRuntime
{
    // Skill-side bridge: animations belong to the Darius skills, not to a specific skin.
    // If the current model is not the native Darius GLB, this retargeter drives compatible
    // humanoid bones and restores the original pose when the one-shot completes.
    public static class DariusRetargetApi
    {
        private static bool _initialized;
        private static bool _failed;

        private static bool EnsureInitialized()
        {
            if (_initialized) return true;
            if (_failed) return false;
            try
            {
                RuntimeLog.Initialize();
                AnimationLibrary.Initialize();
                _initialized = true;
                DariusLog.Info("RETARGET", "Universal Darius animation library initialized. clips=" + AnimationLibrary.GetNames().Length);
                return true;
            }
            catch (Exception e)
            {
                _failed = true;
                DariusLog.Exception("RETARGET", e, "Universal animation library initialization failed");
                return false;
            }
        }

        private static UniversalRetargeter Get(Hero hero)
        {
            if (hero == null || !EnsureInitialized()) return null;
            UniversalRetargeter r = hero.GetComponent<UniversalRetargeter>();
            if (r == null)
            {
                r = hero.gameObject.AddComponent<UniversalRetargeter>();
                r.Initialize(hero);
                DariusLog.Info("RETARGET", "Attached universal retargeter to " + hero.GetType().Name + " mappedBones=" + r.MappedBoneCount);
            }
            return r;
        }

        public static bool Play(Hero hero, string clip, bool loop, float speed = 1f)
        {
            UniversalRetargeter r = Get(hero);
            if (r == null) return false;
            bool ok = r.Play(clip, loop, speed);
            if (!ok) DariusLog.Warn("RETARGET", "Could not retarget clip=" + clip + " hero=" + (hero != null ? hero.GetType().Name : "<null>"));
            return ok;
        }

        public static void Stop(Hero hero)
        {
            if (hero == null) return;
            UniversalRetargeter r = hero.GetComponent<UniversalRetargeter>();
            if (r != null) r.Stop(true);
        }
    }
}
