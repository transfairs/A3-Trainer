using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace A3_Trainer.Controller
{
    /// <summary>
    /// Shows in the game what each interview answer does: appends its effects, e.g. " [Fans +3, Vorstand -2]",
    /// to the answer texts, so the game itself shows them with the answers.
    /// </summary>
    /// <remarks>
    /// For every interview the game reads texte\interv.saq into a heap buffer (FUN_0065F0B8, pointer at
    /// <see cref="A3Addresses.InterviewBuffer"/>, freed again at its end by FUN_0065FB29): an int with the number
    /// of questions, then one record per question {int size, int number, ..., +0x24 two effect pairs (word code,
    /// word value) per answer, +0x44 six text pointers (two variants of the question, four answers), +0x5C the
    /// texts}. The game applies the chosen answer's pairs itself (FUN_0065FF9E), so they only need to be shown.
    /// A labelled answer points to a copy with the label in a block the trainer allocates once per game process;
    /// switching off puts the original pointers back while the buffer still exists.
    /// </remarks>
    internal sealed class InterviewEffects
    {
        internal const int RecordEffects = 0x24;
        internal const int RecordTexts = 0x44;
        internal const int Answers = 4;
        internal const int FirstAnswerText = 2;
        private const int BufferSize = 0x80000;
        private const int BlockSize = 0x100000;
        // The game copies an answer into a 0x170 byte field (FUN_0065FF9E); a longer label isn't added.
        private const int MaxText = 0x16F;

        private static readonly Encoding Latin1 = Encoding.GetEncoding(1252);
        private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

        private readonly IGameMemory memory;
        private readonly IGameCode code;
        // Each labelled answer slot: the pointer it had.
        private readonly Dictionary<uint, uint> originals = new Dictionary<uint, uint>();
        // Labelled copies already written, by their text, reused for the next interview.
        private readonly Dictionary<string, uint> written = new Dictionary<string, uint>();
        private uint block, blockUsed;
        private uint labelledBuffer;

        /// <summary>Works on the attached game through <paramref name="memory"/>, allocating with <paramref name="code"/>.</summary>
        internal InterviewEffects(IGameMemory memory, IGameCode code)
        {
            this.memory = memory;
            this.code = code;
        }

        /// <summary>Whether the labels are switched on; <see cref="Tick"/> then keeps them on the answers.</summary>
        internal bool Enabled { get; private set; }

        /// <summary>The label of one effect pair, or null if it has none worth showing (flow of the interview).</summary>
        internal static string EffectText(int effect, int value)
        {
            string target;
            switch (effect)
            {
                case 1: target = "Öffentlichkeit"; break;
                case 2: target = "Medien"; break;
                case 3: target = "Fans"; break;
                case 4: target = "Reporter"; break;
                case 6: target = "Vorstand"; break;
                case 13: target = "Mannschaft"; break;
                default:
                    // 18-34: one of the players the question is about; 30 and 31 set flags instead of a value.
                    if (effect >= 18 && effect <= 34 && value != 30 && value != 31)
                    {
                        target = "Spieler";
                        break;
                    }
                    return null;
            }
            return value == 0 ? null : string.Format(German, "{0} {1:+0;-0}", target, value);
        }

        /// <summary>The label of an answer's two pairs, e.g. " [Fans +3, Vorstand -2]", or "" if it does nothing shown.</summary>
        internal static string Label(byte[] effects, int answer)
        {
            var parts = new List<string>();
            for (int pair = 0; pair < 2; pair++)
            {
                int at = answer * 8 + pair * 4;
                string text = EffectText(BitConverter.ToInt16(effects, at), BitConverter.ToInt16(effects, at + 2));
                if (text != null)
                    parts.Add(text);
            }
            return parts.Count == 0 ? "" : " [" + string.Join(", ", parts.ToArray()) + "]";
        }

        /// <summary>Switches the labels on or off; off puts the original texts back.</summary>
        internal void SetEnabled(bool on)
        {
            this.Enabled = on;
            if (on)
                this.Tick();
            else
                this.Restore();
        }

        /// <summary>Forgets everything about the game process, e.g. when the game is gone or restarted.</summary>
        internal void Reset()
        {
            this.originals.Clear();
            this.written.Clear();
            this.block = this.blockUsed = 0;
            this.labelledBuffer = 0;
        }

        /// <summary>Labels the answers of a loaded interview that aren't labelled yet. Returns how many were labelled.</summary>
        internal int Tick()
        {
            if (!this.Enabled)
                return 0;
            uint buffer = this.ReadUInt32(A3Addresses.InterviewBuffer);
            if (buffer == 0)
            {
                this.labelledBuffer = 0;
                this.originals.Clear();
                return 0;
            }
            if (buffer != this.labelledBuffer)
            {
                this.originals.Clear();
                this.labelledBuffer = buffer;
            }
            byte[] data = this.memory.Read(buffer, BufferSize);
            if (data == null)
                return 0;

            int labelled = 0;
            int count = BitConverter.ToInt32(data, 0);
            int at = 4;
            for (int question = 0; question < count && at + RecordTexts + 6 * 4 <= data.Length; question++)
            {
                int size = BitConverter.ToInt32(data, at);
                if (size <= RecordTexts + 6 * 4 || at + size > data.Length)
                    break;
                byte[] effects = new byte[Answers * 8];
                Array.Copy(data, at + RecordEffects, effects, 0, effects.Length);
                for (int answer = 0; answer < Answers; answer++)
                {
                    string label = Label(effects, answer);
                    if (label.Length == 0)
                        continue;
                    uint slot = buffer + (uint)(at + RecordTexts + (FirstAnswerText + answer) * 4);
                    uint pointer = BitConverter.ToUInt32(data, at + RecordTexts + (FirstAnswerText + answer) * 4);
                    // Pointers outside the buffer are labelled already (or not set yet by the game).
                    if (pointer < buffer || pointer >= buffer + BufferSize)
                        continue;
                    string text = ReadText(data, (int)(pointer - buffer));
                    if (text == null || text.Length + label.Length > MaxText)
                        continue;
                    uint copy = this.Copy(text + label);
                    if (copy == 0)
                        return labelled;
                    this.memory.Write(slot, BitConverter.GetBytes(copy));
                    this.originals[slot] = pointer;
                    labelled++;
                }
                at += size;
            }
            return labelled;
        }

        private void Restore()
        {
            uint buffer = this.ReadUInt32(A3Addresses.InterviewBuffer);
            if (buffer != 0 && buffer == this.labelledBuffer)
                foreach (KeyValuePair<uint, uint> slot in this.originals)
                    this.memory.Write(slot.Key, BitConverter.GetBytes(slot.Value));
            this.originals.Clear();
            this.labelledBuffer = 0;
        }

        // The text as a zero-terminated copy in the trainer's block; 0 if there is no room or no block.
        private uint Copy(string text)
        {
            if (this.written.TryGetValue(text, out uint known))
                return known;
            byte[] bytes = Latin1.GetBytes(text + "\0");
            if (this.block == 0)
            {
                this.block = this.code.AllocateExecutable(BlockSize) ?? 0;
                this.blockUsed = 0;
                if (this.block == 0)
                    return 0;
            }
            if (this.blockUsed + bytes.Length > BlockSize)
                return 0;
            uint address = this.block + this.blockUsed;
            this.memory.Write(address, bytes);
            this.blockUsed += (uint)bytes.Length;
            this.written[text] = address;
            return address;
        }

        private static string ReadText(byte[] data, int offset)
        {
            int end = Array.IndexOf(data, (byte)0, offset);
            return end < 0 ? null : Latin1.GetString(data, offset, end - offset);
        }

        private uint ReadUInt32(uint address)
        {
            byte[] data = this.memory.Read(address, 4);
            return data == null ? 0 : BitConverter.ToUInt32(data, 0);
        }
    }
}
