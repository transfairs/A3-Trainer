using AnstossTrainer.Shared;

namespace A3_Trainer.Controller
{
    /// <summary>
    /// Absolute-address access to the game. Anstoss 3 v1.10 keeps all of its data at fixed
    /// addresses inside its own image, so there are no pointer chains to resolve. The interface
    /// keeps decoding and write routing testable without a running game.
    /// </summary>
    internal interface IGameMemory
    {
        /// <summary>Reads <paramref name="length"/> bytes, or null if the range isn't readable.</summary>
        byte[] Read(uint address, int length);

        /// <summary>Writes <paramref name="data"/>; does nothing if the address is invalid.</summary>
        void Write(uint address, byte[] data);
    }

    /// <summary>The attached game process, via the shared ProcessMemory.</summary>
    internal sealed class LiveGameMemory : IGameMemory
    {
        private readonly ProcessMemory memory;

        internal LiveGameMemory(ProcessMemory memory)
        {
            this.memory = memory;
        }

        public byte[] Read(uint address, int length) => this.memory.ReadBytesAtAddress(address, length);

        public void Write(uint address, byte[] data) => this.memory.WriteBytesAtAddress(address, data);
    }
}
