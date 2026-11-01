using A3_Trainer.Controller;
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Xunit;

namespace A3_Trainer.Tests
{
    /// <summary>Reflection access and dialog handling shared by the view and window tests.</summary>
    internal static class TestHelpers
    {
        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

        internal static object Invoke(object target, string method, params object[] args) =>
            Find(target.GetType(), t => t.GetMethod(method, Any)).Invoke(target, args);

        internal static T Get<T>(object target, string field) =>
            (T)Find(target.GetType(), t => t.GetField(field, Any)).GetValue(target);

        internal static void Set(object target, string field, object value) =>
            Find(target.GetType(), t => t.GetField(field, Any)).SetValue(target, value);

        private static TMember Find<TMember>(Type type, Func<Type, TMember> lookup) where TMember : class
        {
            for (Type t = type; t != null; t = t.BaseType)
            {
                TMember member = lookup(t);
                if (member != null)
                    return member;
            }
            throw new MissingMemberException(type.Name);
        }

        /// <summary>Raises the Click event of a control or menu item, as a mouse click would (also when it isn't on screen).</summary>
        internal static void Click(object clickable) =>
            Find(clickable.GetType(), t => t.GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(EventArgs) }, null))
                .Invoke(clickable, new object[] { EventArgs.Empty });

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumThreadWindows(uint threadId, EnumWindowsProc callback, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int maxCount);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        private const uint WM_CLOSE = 0x0010;

        /// <summary>
        /// Runs <paramref name="action"/>, which is expected to show a modal message box titled
        /// <paramref name="title"/>, and closes that box from another thread once it appears. Only
        /// windows of the calling thread count, so tests running in parallel can expect the same title.
        /// </summary>
        internal static void ExpectDialog(string title, Action action)
        {
            bool seen = false;
            uint thread = GetCurrentThreadId();
            var closer = new Thread(() =>
            {
                for (int attempt = 0; attempt < 200 && !seen; attempt++)
                {
                    Thread.Sleep(25);
                    EnumThreadWindows(thread, (window, _) =>
                    {
                        var text = new System.Text.StringBuilder(256);
                        GetWindowText(window, text, text.Capacity);
                        if (text.ToString() != title)
                            return true;
                        seen = true;
                        PostMessage(window, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                        return false;
                    }, IntPtr.Zero);
                }
            }) { IsBackground = true };
            closer.Start();
            action();
            closer.Join(TimeSpan.FromSeconds(6));
            Assert.True(seen, $"Dialog \"{title}\" wasn't shown");
        }
    }

    /// <summary>Game memory whose every access fails, as when the game process vanishes mid-read.</summary>
    internal sealed class BrokenGameMemory : IGameMemory
    {
        public byte[] Read(uint address, int length) => throw new InvalidOperationException("Speicher weg");

        public void Write(uint address, byte[] data) => throw new InvalidOperationException("Speicher weg");
    }

    /// <summary>Reads like the wrapped memory but fails every write.</summary>
    internal sealed class ReadOnlyGameMemory : IGameMemory
    {
        private readonly IGameMemory inner;

        internal ReadOnlyGameMemory(IGameMemory inner)
        {
            this.inner = inner;
        }

        public byte[] Read(uint address, int length) => this.inner.Read(address, length);

        public void Write(uint address, byte[] data) => throw new InvalidOperationException("schreibgeschützt");
    }
}
