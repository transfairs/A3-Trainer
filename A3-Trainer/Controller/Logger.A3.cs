using System;

namespace AnstossTrainer.Shared
{
    // Anstoss 3's settings for the shared Logger (Shared/Logger.cs). Debug logging is switched on
    // with the environment variable A3TRAINER_DEBUG=1 until the trainer gets a settings page.
    internal static partial class Logger
    {
        static partial void GetAppFolderName(ref string name) => name = "A3-Trainer";

        static partial void GetDebugEnabled(ref bool enabled) =>
            enabled = Environment.GetEnvironmentVariable("A3TRAINER_DEBUG") == "1";
    }
}
