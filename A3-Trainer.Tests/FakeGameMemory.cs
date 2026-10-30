using A3_Trainer.Controller;
using System;
using System.Collections.Generic;
using System.Linq;

namespace A3_Trainer.Tests
{
    /// <summary>
    /// Stand-in for the game's address space made of separate readable regions; reading across
    /// a gap fails like an unmapped page does in the real process.
    /// </summary>
    internal sealed class FakeGameMemory : IGameMemory
    {
        private readonly List<KeyValuePair<uint, byte[]>> regions = new List<KeyValuePair<uint, byte[]>>();

        internal List<MemoryWrite> Writes { get; } = new List<MemoryWrite>();

        /// <summary>Makes a zero-filled range readable.</summary>
        internal void Map(uint address, int length) => this.regions.Add(new KeyValuePair<uint, byte[]>(address, new byte[length]));

        /// <summary>Puts bytes into an already mapped range, or maps a new range just for them.</summary>
        internal void Put(uint address, params byte[] data)
        {
            if (!this.TryLocate(address, data.Length, out byte[] region, out int offset))
            {
                this.Map(address, data.Length);
                this.TryLocate(address, data.Length, out region, out offset);
            }
            Array.Copy(data, 0, region, offset, data.Length);
        }

        internal void PutUInt16(uint address, int value) => this.Put(address, BitConverter.GetBytes((ushort)value));
        internal void PutUInt32(uint address, uint value) => this.Put(address, BitConverter.GetBytes(value));

        internal byte Get(uint address) => this.Read(address, 1)[0];
        internal int GetUInt16(uint address) => BitConverter.ToUInt16(this.Read(address, 2), 0);

        public byte[] Read(uint address, int length)
        {
            if (!this.TryLocate(address, length, out byte[] region, out int offset))
                return null;
            var data = new byte[length];
            Array.Copy(region, offset, data, 0, length);
            return data;
        }

        public void Write(uint address, byte[] data)
        {
            this.Writes.Add(new MemoryWrite(address, data));
            if (this.TryLocate(address, data.Length, out byte[] region, out int offset))
                Array.Copy(data, 0, region, offset, data.Length);
        }

        /// <summary>The bytes last written at exactly this address, or null.</summary>
        internal byte[] WrittenAt(uint address) =>
            this.Writes.Where(w => w.Address == address).Select(w => w.Data).LastOrDefault();

        // Later mappings win, so a small Put over a big zero region lands in the big region.
        private bool TryLocate(uint address, int length, out byte[] region, out int offset)
        {
            for (int i = this.regions.Count - 1; i >= 0; i--)
            {
                uint start = this.regions[i].Key;
                byte[] data = this.regions[i].Value;
                if (address >= start && (long)address + length <= (long)start + data.Length)
                {
                    region = data;
                    offset = (int)(address - start);
                    return true;
                }
            }
            region = null;
            offset = 0;
            return false;
        }
    }
}
