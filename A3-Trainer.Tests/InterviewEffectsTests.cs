using A3_Trainer.Controller;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace A3_Trainer.Tests
{
    /// <summary>Showing the effects of interview answers in the game's answer texts.</summary>
    public class InterviewEffectsTests
    {
        private const uint Buffer = 0x08000000;
        private static readonly Encoding Latin1 = Encoding.GetEncoding(1252);

        // An interview buffer as the game loads texte\interv.saq: two questions. The first one's answers:
        // 1 Medien -5 and Mannschaft +15, 2 Öffentlichkeit +5 and a flow code, 3 nothing, 4 Fans -5 and Vorstand -2.
        // The second one's answer 1 changes a player's view (+4), answer 2 sets a player flag (30).
        internal static FakeGameMemory WithInterview(out uint[] answerSlots)
        {
            var m = new FakeGameMemory();
            m.Map(A3Addresses.InterviewBuffer, 4);
            m.Map(Buffer, 0x80000);
            m.PutUInt32(A3Addresses.InterviewBuffer, Buffer);
            var slots = new List<uint>();
            var file = new List<byte>(BitConverter.GetBytes(2));
            Record(file, slots, 1, new short[] { 2, -5, 13, 15, 1, 5, 9, 1, 0, 0, 0, 0, 3, -5, 6, -2 },
                "Soll das so weitergehen?", "", "Nein.", "Weiter so!", "Keine Ahnung.", "Was verstehen Sie schon davon?");
            Record(file, slots, 2, new short[] { 20, 4, 0, 0, 21, 30, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                "Wie ist N1 drauf?", "", "Gut.", "Schlecht.", "Weiß nicht.", "Kein Kommentar.");
            m.Put(Buffer, file.ToArray());
            answerSlots = slots.ToArray();
            return m;
        }

        private static void Record(List<byte> file, List<uint> slots, int number, short[] effects, params string[] texts)
        {
            int start = file.Count;
            var record = new byte[0x5C];
            BitConverter.GetBytes(number).CopyTo(record, 4);
            for (int i = 0; i < effects.Length; i++)
                BitConverter.GetBytes(effects[i]).CopyTo(record, InterviewEffects.RecordEffects + i * 2);
            var text = new List<byte>();
            for (int i = 0; i < texts.Length; i++)
            {
                BitConverter.GetBytes(Buffer + (uint)(start + 0x5C + text.Count)).CopyTo(record, InterviewEffects.RecordTexts + i * 4);
                if (i >= InterviewEffects.FirstAnswerText)
                    slots.Add(Buffer + (uint)(start + InterviewEffects.RecordTexts + i * 4));
                text.AddRange(Latin1.GetBytes(texts[i] + "\0"));
            }
            BitConverter.GetBytes(record.Length + text.Count).CopyTo(record, 0);
            file.AddRange(record);
            file.AddRange(text);
        }

        private static string TextAt(FakeGameMemory m, uint slot)
        {
            uint pointer = m.ReadUInt32(slot);
            byte[] data = m.Read(pointer, 400);
            return Latin1.GetString(data, 0, Array.IndexOf(data, (byte)0));
        }

        [Theory]
        [InlineData(1, 5, "Öffentlichkeit +5")]
        [InlineData(2, -5, "Medien -5")]
        [InlineData(3, 2, "Fans +2")]
        [InlineData(4, -1, "Reporter -1")]
        [InlineData(6, -2, "Vorstand -2")]
        [InlineData(13, 15, "Mannschaft +15")]
        [InlineData(18, 3, "Spieler +3")]
        [InlineData(34, -3, "Spieler -3")]
        [InlineData(20, 30, null)]
        [InlineData(20, 31, null)]
        [InlineData(35, 3, null)]
        [InlineData(9, 1, null)]
        [InlineData(2, 0, null)]
        public void EffectText_NamesWhatTheAnswerChanges(int effect, int value, string expected) =>
            Assert.Equal(expected, InterviewEffects.EffectText(effect, value));

        [Fact]
        public void Tick_LabelsTheAnswers_AndSwitchingOffRestoresThem()
        {
            FakeGameMemory m = WithInterview(out uint[] slots);
            var code = new TransferInjectorTests.FakeCode(m);
            var effects = new InterviewEffects(m, code);
            uint[] originals = slots.Select(s => m.ReadUInt32(s)).ToArray();

            Assert.Equal(0, effects.Tick());              // off: nothing
            effects.SetEnabled(true);
            Assert.Equal("Nein. [Medien -5, Mannschaft +15]", TextAt(m, slots[0]));
            Assert.Equal("Weiter so! [Öffentlichkeit +5]", TextAt(m, slots[1]));
            Assert.Equal("Keine Ahnung.", TextAt(m, slots[2]));
            Assert.Equal("Was verstehen Sie schon davon? [Fans -5, Vorstand -2]", TextAt(m, slots[3]));
            Assert.Equal("Gut. [Spieler +4]", TextAt(m, slots[4]));
            Assert.Equal("Schlecht.", TextAt(m, slots[5]));
            Assert.Equal(0, effects.Tick());               // labelled already

            effects.SetEnabled(false);
            Assert.Equal(originals, slots.Select(s => m.ReadUInt32(s)).ToArray());
        }

        [Fact]
        public void Tick_LabelsEachNewInterview_ReusingTheCopies()
        {
            FakeGameMemory m = WithInterview(out uint[] slots);
            var effects = new InterviewEffects(m, new TransferInjectorTests.FakeCode(m));
            effects.SetEnabled(true);
            uint labelled = m.ReadUInt32(slots[0]);

            // The interview ends (buffer freed) and the next one loads the file again.
            m.PutUInt32(A3Addresses.InterviewBuffer, 0);
            Assert.Equal(0, effects.Tick());
            FakeGameMemory again = WithInterview(out _);
            m.Put(Buffer, again.Read(Buffer, 0x400));
            m.PutUInt32(A3Addresses.InterviewBuffer, Buffer);
            Assert.Equal(4, effects.Tick());
            Assert.Equal(labelled, m.ReadUInt32(slots[0]));
        }

        [Fact]
        public void Tick_SkipsWhatItCantLabel()
        {
            // No interview running, or a game process that refuses the allocation.
            FakeGameMemory idle = WithInterview(out _);
            idle.PutUInt32(A3Addresses.InterviewBuffer, 0);
            var effects = new InterviewEffects(idle, new TransferInjectorTests.FakeCode(idle));
            effects.SetEnabled(true);
            Assert.Equal(0, effects.Tick());

            FakeGameMemory m = WithInterview(out uint[] slots);
            uint original = m.ReadUInt32(slots[0]);
            var refusing = new InterviewEffects(m, new TransferInjectorTests.FakeCode(m) { Refuse = true });
            refusing.SetEnabled(true);
            Assert.Equal(original, m.ReadUInt32(slots[0]));

            // After Reset (another game process) a new block is allocated.
            var effectsOk = new InterviewEffects(m, new TransferInjectorTests.FakeCode(m));
            effectsOk.SetEnabled(true);
            effectsOk.Reset();
            Assert.Equal(0, effectsOk.Tick());   // the slots still point to the old copies

            // An unreadable buffer or a broken record stops quietly.
            FakeGameMemory broken = new FakeGameMemory();
            broken.Map(A3Addresses.InterviewBuffer, 4);
            broken.PutUInt32(A3Addresses.InterviewBuffer, 0x09000000);
            var none = new InterviewEffects(broken, new TransferInjectorTests.FakeCode(broken));
            none.SetEnabled(true);
            Assert.Equal(0, none.Tick());
            broken.Map(0x09000000, 0x80000);
            broken.PutUInt32(0x09000000, 3);
            broken.PutUInt32(0x09000004, 8);   // a record too small to hold its texts
            Assert.Equal(0, none.Tick());
        }

        [Fact]
        public void Label_JoinsBothPairs()
        {
            var effects = new byte[32];
            BitConverter.GetBytes((short)3).CopyTo(effects, 8);
            BitConverter.GetBytes((short)7).CopyTo(effects, 10);
            Assert.Equal("", InterviewEffects.Label(effects, 0));
            Assert.Equal(" [Fans +7]", InterviewEffects.Label(effects, 1));
        }
    }
}
