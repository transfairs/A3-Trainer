using System;

namespace AnstossTrainer.Shared
{
    // Anstoss 3's settings for the shared Logger (Shared/Logger.cs). Debug logging is switched on
    // with the environment variable A3TRAINER_DEBUG=1.
    internal static partial class Logger
    {
        /// <summary>Name of the log folder under %LocalAppData%.</summary>
        internal const string AppFolder = "A3-Trainer";

        static partial void GetAppFolderName(ref string name) => name = AppFolder;

        static partial void GetDebugEnabled(ref bool enabled) =>
            enabled = Environment.GetEnvironmentVariable("A3TRAINER_DEBUG") == "1";
    }
}
