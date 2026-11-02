using AnstossTrainer.Shared;

namespace A3_Trainer.Controller
{
    /// <summary>
    /// Absolute-address access to the game. Anstoss 3 v1.40 keeps all of its data at fixed
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

    /// <summary>Placing code in the game process, for TransferInjector.</summary>
    internal interface IGameCode
    {
        /// <summary>Executable, writable memory of <paramref name="size"/> bytes, or null if it can't be had.</summary>
        uint? AllocateExecutable(int size);

        /// <summary>Writes over the game's code; returns whether it was written.</summary>
        bool WriteCode(uint address, byte[] data);
    }

    /// <summary>The attached game process, via the shared ProcessMemory.</summary>
    internal sealed class LiveGameMemory : IGameMemory, IGameCode
    {
        private readonly ProcessMemory memory;

        /// <summary>Wraps the shared process memory.</summary>
        internal LiveGameMemory(ProcessMemory memory)
        {
            this.memory = memory;
        }

        /// <inheritdoc/>
        public byte[] Read(uint address, int length) => this.memory.ReadBytesAtAddress(address, length);

        /// <inheritdoc/>
        public void Write(uint address, byte[] data) => this.memory.WriteBytesAtAddress(address, data);

        /// <inheritdoc/>
        public uint? AllocateExecutable(int size) => this.memory.AllocateExecutable(size);

        /// <inheritdoc/>
        public bool WriteCode(uint address, byte[] data) => this.memory.WriteCode(address, data);
    }
}
