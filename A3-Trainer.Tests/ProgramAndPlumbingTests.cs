using A3_Trainer.Controller;
using A3_Trainer.View;
using AnstossTrainer.Shared;
using AnstossTrainer.Shared.Tests;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Xunit;

namespace A3_Trainer.Tests
{
    /// <summary>
    /// Entry point, the live memory adapter, the logger settings and the theme's owner-drawing.
    /// Main() runs a real message loop on its own STA thread, ended through Application.Idle once it
    /// pumps; its exception handlers are invoked directly (raising real unhandled exceptions would
    /// risk the test process).
    /// </summary>
    public class ProgramAndPlumbingTests
    {
        private static Type ProgramType => typeof(MainWindow).Assembly.GetType("A3_Trainer.Program");

        [Fact]
        public void Main_RunsAndExitsTheMessageLoop()
        {
            Exception thrown = null;
            var thread = new Thread(() =>
            {
                EventHandler exitOnIdle = null;
                exitOnIdle = (s, e) =>
                {
                    Application.Idle -= exitOnIdle;
                    Application.Exit();
                };
                Application.Idle += exitOnIdle;
                try
                {
                    ProgramType.GetMethod("Main", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
                }
                catch (Exception ex)
                {
                    thrown = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(30)));
            Assert.Null(thrown);
        }

        [Fact]
        public void Main_ExceptionHandlers_LogWithoutThrowing()
        {
            Type generated = ProgramType.GetNestedTypes(BindingFlags.NonPublic).Single(t => t.Name.StartsWith("<>c", StringComparison.Ordinal));
            object instance = generated.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Single(f => f.FieldType == generated).GetValue(null);
            MethodInfo Handler(Type args) => generated.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .Single(m => m.GetParameters().Length == 2 && m.GetParameters()[1].ParameterType == args);

            var ex = new InvalidOperationException("Test");
            Handler(typeof(ThreadExceptionEventArgs)).Invoke(instance, new object[] { null, new ThreadExceptionEventArgs(ex) });
            Handler(typeof(UnhandledExceptionEventArgs)).Invoke(instance, new object[] { null, new UnhandledExceptionEventArgs(ex, false) });
        }

        [Fact]
        public void LiveGameMemory_ReadsAndWritesTheAttachedProcess()
        {
            var memory = new ProcessMemory();
            Assert.True(memory.OpenProcess(Process.GetCurrentProcess().Id));
            IntPtr block = Marshal.AllocHGlobal(16);
            try
            {
                var live = new LiveGameMemory(memory);
                uint address = (uint)block.ToInt64();

                live.Write(address, new byte[] { 1, 2, 3, 4 });

                Assert.Equal(new byte[] { 1, 2, 3, 4 }, live.Read(address, 4));
            }
            finally
            {
                Marshal.FreeHGlobal(block);
            }
        }

        [Fact]
        public void Logger_DebugFollowsTheEnvironmentVariable()
        {
            string before = Environment.GetEnvironmentVariable("A3TRAINER_DEBUG");
            try
            {
                Environment.SetEnvironmentVariable("A3TRAINER_DEBUG", "1");
                Logger.Debug("A3-Trainer-Test");
            }
            finally
            {
                Environment.SetEnvironmentVariable("A3TRAINER_DEBUG", before);
            }
        }

        [Fact]
        public void Theme_DrawsHeadersItemsAndTabs() => StaThread.Run(() =>
        {
            using (var bitmap = new Bitmap(200, 40))
            using (Graphics g = Graphics.FromImage(bitmap))
            using (var list = new ListView { View = System.Windows.Forms.View.Details })
            using (var tabs = new TabControl())
            {
                ColumnHeader header = list.Columns.Add("Spieler", 100);
                var item = new ListViewItem(new[] { "Huhn", "TO" });
                list.Items.Add(item);
                tabs.TabPages.Add("Daten 1");
                tabs.TabPages.Add("Daten 2");
                A3Theme.Apply(list);
                A3Theme.Apply(tabs);
                var bounds = new Rectangle(0, 0, 100, 20);

                Draw("DrawGreenHeader", list, new DrawListViewColumnHeaderEventArgs(g, bounds, 0, header, ListViewItemStates.Default, Color.Black, Color.White, list.Font));
                var itemArgs = new DrawListViewItemEventArgs(g, item, bounds, 0, ListViewItemStates.Default);
                Draw("DrawDefaultItem", list, itemArgs);
                Assert.True(itemArgs.DrawDefault);
                var subItemArgs = new DrawListViewSubItemEventArgs(g, bounds, item, item.SubItems[1], 0, 1, header, ListViewItemStates.Default);
                Draw("DrawDefaultSubItem", list, subItemArgs);
                Assert.True(subItemArgs.DrawDefault);
                Draw("DrawGreenTab", tabs, new DrawItemEventArgs(g, tabs.Font, bounds, 0, DrawItemState.Selected));
                Draw("DrawGreenTab", tabs, new DrawItemEventArgs(g, tabs.Font, bounds, 1, DrawItemState.None));

                // The unselected tab was drawn last, in the panel colour.
                Assert.Equal(A3Theme.Panel.ToArgb(), bitmap.GetPixel(2, 2).ToArgb());
            }
        });

        [Fact]
        public void Theme_MenuColoursAreTheGames()
        {
            using (var menu = new MenuStrip())
            {
                A3Theme.ApplyMenu(menu);
                ProfessionalColorTable colours = ((ToolStripProfessionalRenderer)menu.Renderer).ColorTable;

                Color[] used = typeof(ProfessionalColorTable).GetProperties()
                    .Where(p => p.PropertyType == typeof(Color) && p.DeclaringType != typeof(object))
                    .Where(p => colours.GetType().GetProperty(p.Name).DeclaringType == colours.GetType())
                    .Select(p => (Color)p.GetValue(colours))
                    .ToArray();

                Assert.Equal(14, used.Length);
                Assert.All(used, c => Assert.Contains(c, new[] { A3Theme.Green, A3Theme.GreenLight, A3Theme.GreenDark, A3Theme.Panel }));
            }
        }

        private static void Draw(string handler, object sender, EventArgs args) =>
            typeof(A3Theme).GetMethod(handler, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new[] { sender, args });
    }
}
