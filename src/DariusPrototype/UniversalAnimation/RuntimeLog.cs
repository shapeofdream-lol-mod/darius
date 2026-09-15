using System;

namespace DariusModelAnimationRuntime
{
    // Adapter kept for the reusable retargeting code. It resolves assets from the main
    // DariusPrototype mod root and writes into the normal Darius runtime log.
    internal static class RuntimeLog
    {
        public static string Root { get; private set; }

        public static void Initialize()
        {
            Root = global::DariusMedia.Root;
            if (string.IsNullOrEmpty(Root))
            {
                global::DariusMedia.PreloadAll();
                Root = global::DariusMedia.Root;
            }
            if (string.IsNullOrEmpty(Root)) throw new InvalidOperationException("Darius media root is unavailable.");
        }

        public static void Info(string tag, string message) { global::DariusLog.Info("RETARGET-" + tag, message); }
        public static void DebugInfo(string tag, string message) { global::DariusLog.DebugInfo("RETARGET-" + tag, message); }
        public static void Warn(string tag, string message) { global::DariusLog.Warn("RETARGET-" + tag, message); }
        public static void Error(string tag, string message) { global::DariusLog.Error("RETARGET-" + tag, message); }
        public static void Exception(string tag, Exception e, string message) { global::DariusLog.Exception("RETARGET-" + tag, e, message); }
    }
}
