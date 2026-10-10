namespace Utility.Notifications
{
    internal static class Toast
    {
        internal static int Count;

        public static void Info(string title, string message) =>
            System.Threading.Interlocked.Increment(ref Count);

        public static void Warning(string title, string message) =>
            System.Threading.Interlocked.Increment(ref Count);

        public static void Error(string title, string message) =>
            System.Threading.Interlocked.Increment(ref Count);
    }
}
