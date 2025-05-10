using Verse;

namespace KCSG
{
    public static class Debug
    {
        public static void Message(string message)
        {
            // This is a simple implementation that forwards to RimWorld's logging system
            Log.Message($"[KCSG] {message}");
        }
    }
} 