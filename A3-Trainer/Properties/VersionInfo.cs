using System.Reflection;

// Wird von der Release-Pipeline (.github/workflows/release.yml) aus dem Git-Tag (vX.Y.Z)
// überschrieben. Lokale/Debug-Builds ohne Tag verwenden diese Default-Werte.
[assembly: AssemblyVersion("0.0.0.0")]
[assembly: AssemblyFileVersion("0.0.0.0")]
[assembly: AssemblyInformationalVersion("0.0.0-dev")]
