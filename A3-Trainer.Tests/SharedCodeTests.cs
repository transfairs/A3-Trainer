using A3_Trainer.Controller;
using AnstossTrainer.Shared;
using AnstossTrainer.Shared.Tests;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Xunit;

namespace A3_Trainer.Tests
{
    /// <summary>
    /// The code shared with the other Anstoss trainers (Shared/): process memory access, the view
    /// base, the observer loop and the view scaling. The test process stands in for the game.
    /// </summary>
    public class SharedCodeTests
    {
        private const uint MemReserve = 0x2000;
        private const uint MemRelease = 0x8000;
        private const uint PageNoAccess = 0x01;

        [DllImport("kernel32.dll")]
        private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint type, uint protect);

        [DllImport("kernel32.dll")]
        private static extern bool VirtualFree(IntPtr address, UIntPtr size, uint type);

        [Fact]
        public void ProcessMemory_AttachesOnlyToALiveProcess_OnceForTheSamePid()
        {
            var memory = new ProcessMemory();
            Assert.False(memory.OpenProcess(0));
            Assert.False(memory.OpenProcess(int.MaxValue));   // no such process
            Assert.False(memory.OpenProcess(-1));             // negative IDs exist only on Windows 9x

            Process self = Process.GetCurrentProcess();
            Assert.True(memory.OpenProcess(self.Id));
            Assert.True(memory.OpenProcess(self.Id));
            Assert.Equal((uint)self.MainModule.BaseAddress.ToInt64(), memory.ModuleBase);
            Assert.Equal((uint)self.MainModule.ModuleMemorySize, memory.ModuleSize);
        }

        [Fact]
        public void ProcessMemory_LeavesAProcessAlone_WhoseWindowHangs()
        {
            // A transparent window off screen whose thread sleeps instead of pumping messages:
            // Windows reports the process as not responding.
            const string script = "Add-Type -AssemblyName System.Windows.Forms; $f = New-Object Windows.Forms.Form; "
                + "$f.StartPosition = 'Manual'; $f.Location = New-Object Drawing.Point(-3000, -3000); "
                + "$f.FormBorderStyle = 'FixedToolWindow'; $f.Opacity = 0; $f.Show(); Start-Sleep 60";
            var start = new ProcessStartInfo("powershell.exe", "-MTA -NoProfile -NonInteractive -Command \"" + script + "\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using (Process hung = Process.Start(start))
            {
                try
                {
                    var waited = Stopwatch.StartNew();
                    while (hung.MainWindowHandle == IntPtr.Zero && !hung.HasExited && waited.Elapsed < TimeSpan.FromSeconds(30))
                    {
                        System.Threading.Thread.Sleep(100);
                        hung.Refresh();
                    }
                    Assert.NotEqual(IntPtr.Zero, hung.MainWindowHandle);

                    Assert.False(new ProcessMemory().OpenProcess(hung.Id));
                }
                finally
                {
                    if (!hung.HasExited)
                        hung.Kill();
                }
            }
        }

        [Fact]
        public void ProcessMemory_ReadsAndWritesThroughPointerChains_AndYieldsDefaultsWhereNothingIsReadable()
        {
            var memory = new ProcessMemory();
            Assert.True(memory.OpenProcess(Process.GetCurrentProcess().Id));
            IntPtr block = Marshal.AllocHGlobal(64);
            IntPtr reserved = VirtualAlloc(IntPtr.Zero, (UIntPtr)4096u, MemReserve, PageNoAccess);
            try
            {
                // block: +0 points to +16, +8 holds a null pointer, +12 points into memory that can't be read.
                uint cell = (uint)block.ToInt64();
                uint data = cell + 16;
                Marshal.WriteInt32(block, 0, unchecked((int)data));
                Marshal.WriteInt32(block, 8, 0);
                Marshal.WriteInt32(block, 12, unchecked((int)(uint)reserved.ToInt64()));
                string At(uint pointerField, uint offset) => $"testhost+{unchecked(pointerField - memory.ModuleBase):X},{offset:X}";

                memory.WriteBytes(At(cell, 0), new byte[] { 0x34, 0x12, 0, 0 });
                Assert.Equal(0x34, memory.ReadByte(At(cell, 0)));
                Assert.Equal(0x1234, memory.ReadInt32(At(cell, 0)));
                Assert.Equal(0x1234, memory.ReadUInt16(At(cell, 0)));
                Assert.Equal(0x34, memory.ReadByte($"testhost+{unchecked(cell - memory.ModuleBase):X}, "));   // a blank offset is 0
                memory.WriteMemory(At(cell, 4), "string", "Huhn\0", Encoding.ASCII);
                Assert.Equal("Huhn", memory.ReadString(At(cell, 4), 8, Encoding.ASCII));
                Assert.Equal("Huhn", memory.ReadStringAtAddress(data + 4, 8, Encoding.ASCII));
                Assert.Throws<ArgumentException>(() => memory.WriteMemory(At(cell, 0), "int", "1", Encoding.ASCII));
                Assert.Throws<FormatException>(() => memory.ReadByte("testhost+10"));

                // A null pointer, an unreadable pointer field and an unreadable target.
                foreach (string unreadable in new[] { At(cell + 8, 0), At(0x1000, 0), At(cell + 12, 0) })
                {
                    Assert.Equal(0, memory.ReadByte(unreadable));
                    Assert.Equal(0, memory.ReadInt32(unreadable));
                    Assert.Equal(0, memory.ReadUInt16(unreadable));
                    Assert.Equal("", memory.ReadString(unreadable, 4, Encoding.ASCII));
                    memory.WriteBytes(unreadable, new byte[] { 1 });
                }

                Assert.Null(memory.ReadBytesAtAddress(0x100, 4));
                Assert.Null(memory.ReadBytesAtAddress((uint)reserved.ToInt64(), 4));
                Assert.Equal("", memory.ReadStringAtAddress(0x100, 4, Encoding.ASCII));
                memory.WriteBytesAtAddress(0x100, new byte[] { 1 });
            }
            finally
            {
                Marshal.FreeHGlobal(block);
                VirtualFree(reserved, UIntPtr.Zero, MemRelease);
            }
        }

        [Fact]
        public void ProcessMemory_WritesCode_OnlyWithAProcessAndAValidAddress()
        {
            var memory = new ProcessMemory();
            Assert.True(memory.OpenProcess(Process.GetCurrentProcess().Id));
            var live = new LiveGameMemory(memory);
            uint code = live.AllocateExecutable(16).Value;

            Assert.True(live.WriteCode(code, new byte[] { 0xC3 }));
            Assert.Equal(new byte[] { 0xC3 }, live.Read(code, 1));
            Assert.False(memory.WriteCode(0x100, new byte[] { 0x90 }));
            Assert.False(new ProcessMemory().WriteCode(code, new byte[] { 0x90 }));   // no process handle
            Assert.Equal(new byte[] { 0xC3 }, live.Read(code, 1));
        }

        [Fact]
        public void ProcessMemory_SharedArena_AllocatesThroughAMappingHandedToTheGame()
        {
            // Windows 9x path; the test process stands in for the game, where the view is ordinary memory.
            bool before = ProcessMemory.UseSharedArena;
            ProcessMemory.UseSharedArena = true;
            try
            {
                var memory = new ProcessMemory();
                Assert.True(memory.OpenProcess(Process.GetCurrentProcess().Id));
                uint block = memory.AllocateExecutable(0x100).Value;
                memory.WriteBytesAtAddress(block, new byte[] { 0xC3, 0x90 });
                Assert.Equal(new byte[] { 0xC3, 0x90 }, memory.ReadBytesAtAddress(block, 2));

                Assert.Null(memory.AllocateExecutable(0));                       // empty mapping refused
                Assert.Null(new ProcessMemory().AllocateExecutable(0x100));      // no game to hand the handle to
            }
            finally
            {
                ProcessMemory.UseSharedArena = before;
            }
        }

        private sealed class TestView : TrainerView
        {
            internal TestView()
            {
            }

            internal TestView(ProcessMemory memory) : base(memory)
            {
            }

            internal string Started { get; private set; }

            internal string DisplayName => this.GameDisplayName;

            internal override void StartProcess(string url) => this.Started = url;

            internal void Digits(KeyPressEventArgs e) => this.NumericOnly_KeyPress(this, e);

            internal string Capped(Action<object, EventArgs> handler, string text)
            {
                var box = new TextBox { Text = text };
                handler(box, EventArgs.Empty);
                return box.Text;
            }

            internal string Ushort(string text) => this.Capped(this.UshortMaxNumber_TextChanged, text);
            internal string Int(string text) => this.Capped(this.IntMaxNumber_TextChanged, text);
            internal string Byte(string text) => this.Capped(this.ByteMax255_TextChanged, text);

            internal void ClickLink(object data) =>
                this.LinkLabel_LinkClicked(this, new LinkLabelLinkClickedEventArgs(new LinkLabel.Link { LinkData = data }));
        }

        [Fact]
        public void TrainerView_FiltersAndCapsNumbers_AndOpensLinks() => StaThread.Run(() =>
        {
            using (var designer = new TestView())
                Assert.Null(designer.Memory);
            var memory = new ProcessMemory();
            using (var view = new TestView(memory))
            {
                Assert.Same(memory, view.Memory);
                Assert.Equal("Anstoss", view.DisplayName);

                var letter = new KeyPressEventArgs('a');
                var digit = new KeyPressEventArgs('7');
                var backspace = new KeyPressEventArgs('\b');
                view.Digits(letter);
                view.Digits(digit);
                view.Digits(backspace);
                Assert.True(letter.Handled);
                Assert.False(digit.Handled);
                Assert.False(backspace.Handled);

                Assert.Equal("65535", view.Ushort("70000"));
                Assert.Equal("123", view.Ushort("123"));
                Assert.Equal("", view.Ushort(""));
                Assert.Equal("2147483647", view.Int("9999999999"));
                Assert.Equal("123", view.Int("123"));
                Assert.Equal("255", view.Byte("300"));
                Assert.Equal("12", view.Byte("12"));

                view.ClickLink(null);
                Assert.Null(view.Started);
                view.ClickLink("https://example.org/");
                Assert.Equal("https://example.org/", view.Started);
            }
        });

        private sealed class PlainView : TrainerView
        {
        }

        [Fact]
        public void TrainerView_HandsTheLinkToWindows() => StaThread.Run(() =>
        {
            // A file that doesn't exist: Windows refuses it, so nothing opens, and the error reaches the caller.
            string missing = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".html");
            using (var view = new PlainView())
                Assert.Throws<System.ComponentModel.Win32Exception>(() => view.StartProcess(missing));
        });

        private sealed class TestObserver : GameObserver
        {
            private readonly int ticks;
            private readonly bool failUpdate;
            private int attempts;

            internal TestObserver(int ticks, bool failUpdate)
            {
                this.ticks = ticks;
                this.failUpdate = failUpdate;
            }

            internal List<string> Calls { get; } = new List<string>();

            protected override bool ShutDownRequested => this.Calls.Count >= this.ticks;

            internal override IEnumerable<Process> GetGameCandidates() => new[] { Process.GetCurrentProcess() };

            // The first attempt finds no game yet, unless an update is to fail right away.
            protected override bool TryAttach(Process candidate) => this.failUpdate || this.attempts++ > 0;

            protected override void UpdateGameProcess()
            {
                this.Calls.Add("update");
                if (this.failUpdate)
                    throw new InvalidOperationException("Speicher weg");
            }

            protected override void ClearGameProcess() => this.Calls.Add("clear");

            internal void Refresh(Action action) => this.RefreshView("Test", action);
        }

        [Fact]
        public void GameObserver_ClearsUntilTheGameIsFound_ThenUpdates_AndStopsOnAnError()
        {
            var observer = new TestObserver(3, failUpdate: false);
            observer.Observe();     // the third tick keeps the live game without looking again
            Assert.Equal(new[] { "clear", "update", "update" }, observer.Calls);

            var failing = new TestObserver(5, failUpdate: true);
            failing.Observe();     // logged, the loop ends
            Assert.Equal(new[] { "update" }, failing.Calls);

            bool ran = false;
            failing.Refresh(() => ran = true);
            failing.Refresh(() => throw new InvalidOperationException("kaputt"));   // logged only
            Assert.True(ran);
        }

        [Fact]
        public void ViewScaler_ScalesFontsBoundsColumnsAndRichText_AndFollowsAddedAndRemovedControls() => StaThread.Run(() =>
        {
            using (var root = new UserControl { Size = new Size(200, 100), Font = new Font("Tahoma", 8f) })
            {
                var label = new Label { Bounds = new Rectangle(10, 10, 50, 20), Font = new Font("Tahoma", 10f, FontStyle.Bold) };
                var panel = new Panel { Bounds = new Rectangle(0, 40, 100, 50), AutoScroll = true };
                panel.Controls.Add(new TextBox { Bounds = new Rectangle(0, 200, 50, 20) });
                var rich = new RichTextBox { Bounds = new Rectangle(100, 0, 100, 50), Rtf = @"{\rtf1 Hallo}" };
                var list = new ListView { Bounds = new Rectangle(100, 50, 100, 50), View = System.Windows.Forms.View.Details };
                list.Columns.Add("Spieler", 40);
                var docked = new Panel { Dock = DockStyle.Bottom, Height = 5 };
                root.Controls.AddRange(new Control[] { label, panel, rich, list, docked });
                root.CreateControl();
                panel.AutoScrollPosition = new Point(0, 100);

                using (var scaler = new ViewScaler(root))
                {
                    scaler.ScaleTo(Size.Empty);
                    Assert.Equal(1f, scaler.ScaleX);

                    scaler.ScaleTo(new Size(400, 200));
                    Assert.Equal(2f, scaler.FontScale);
                    Assert.Equal(new Rectangle(20, 20, 100, 40), label.Bounds);
                    Assert.Equal(20f, label.Font.Size);
                    Assert.Equal(16f, root.Font.Size);
                    Assert.Equal(80, list.Columns[0].Width);
                    Assert.Equal(2f, rich.ZoomFactor);
                    Assert.Contains("Hallo", rich.Text);
                    Assert.Equal(5, docked.Height);       // docked controls are left to their parent

                    Font scaled = label.Font;
                    scaler.ScaleTo(new Size(400, 200));
                    Assert.Same(scaled, label.Font);

                    // Controls added later come in at the current scale, inside the scrolled panel as well.
                    var added = new Label { Bounds = new Rectangle(5, 5, 20, 10), Font = new Font("Tahoma", 9f) };
                    root.Controls.Add(added);
                    Assert.Equal(new Rectangle(10, 10, 40, 20), added.Bounds);
                    Assert.Equal(18f, added.Font.Size);
                    var box = new Panel { Bounds = new Rectangle(0, 0, 10, 10) };
                    var inner = new RichTextBox { Bounds = new Rectangle(0, 0, 10, 10) };
                    box.Controls.Add(inner);
                    panel.Controls.Add(box);
                    Assert.Equal(20, box.Width);
                    Assert.Equal(2f, inner.ZoomFactor);
                    TestHelpers.Invoke(scaler, "Track", label);   // already followed: nothing changes

                    root.Controls.Remove(list);
                    panel.Controls.Remove(box);
                    scaler.ScaleTo(new Size(200, 100));
                    Assert.Equal(10f, label.Font.Size);
                    Assert.Equal(new Rectangle(10, 10, 50, 20), label.Bounds);
                    Assert.Equal(80, list.Columns[0].Width);   // no longer followed
                    Assert.Equal(20, box.Width);
                }
            }
        });
    }
}
