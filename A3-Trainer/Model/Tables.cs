namespace A3_Trainer.Model
{
    /// <summary>
    /// Value and bit tables of Anstoss 3, taken from the game's own tables (text IDs resolved via
    /// the developer headers shipped with patch 1.10, deb\ListeDerTexte.rh).
    /// </summary>
    internal static class Tables
    {
        /// <summary>Position values 1..10 (index 0 = none), in the game's own button order.</summary>
        internal static readonly string[] Positions = { "–", "TO", "LI", "IV", "LV", "RV", "RM", "LM", "DM", "OM", "ST" };

        /// <summary>Position value of the goalkeeper; goalkeepers use their own skill table.</summary>
        internal const int Goalkeeper = 1;

        /// <summary>Talent values 0..4.</summary>
        internal static readonly string[] Talents = { "riesig", "groß", "mittel", "klein", "keins" };

        /// <summary>Strong foot values 1..3 (index 0 = unknown).</summary>
        internal static readonly string[] Feet = { "–", "rechts", "links", "beidfüßig" };

        /// <summary>Strength is a byte; the trainer doesn't restrict it further (as the A2G trainer).</summary>
        internal const int MaxStrength = 255;

        /// <summary>Position values in the order of the game's editor (Positionen tab).</summary>
        internal static readonly int[] EditorPositions = { 1, 2, 3, 4, 5, 8, 7, 6, 9, 10 };

        /// <summary>Full position names, parallel to <see cref="EditorPositions"/>.</summary>
        internal static readonly string[] EditorPositionNames =
        {
            "Torwart", "Libero", "Innenverteidiger", "linker Verteidiger", "rechter Verteidiger", "defensiver Mittelfeldspieler",
            "linker Mittelfeldspieler", "rechter Mittelfeldspieler", "offensiver Mittelfeldspieler", "Stürmer",
        };

        /// <summary>Skill bits of field players (table 0x008887E0).</summary>
        internal static readonly string[] FieldSkills =
        {
            "Kopfball", "Zweikampf", "Schnelligkeit", "Schußkraft", "Elfmeter", "Freistöße", "Flanken", "Torinstinkt",
            "Laufstärke", "Technik", "Ballzauberer", "Spielmacher", "Viererkette", "Spielübersicht", "Ballhalten", "Dribbling",
        };

        /// <summary>Skill bits of goalkeepers (table 0x00888820; the game uses only these nine).</summary>
        internal static readonly string[] GoalkeeperSkills =
        {
            "Elfmetertöter", "starke Reflexe", "Herauslaufen", "Fangsicherheit", "Fausten", "Ballsicherheit", "Schnelligkeit", "weite Abschläge", "weite Abwürfe",
        };

        /// <summary>Trait bits (table 0x008A8550).</summary>
        internal static readonly string[] Traits =
        {
            "Kämpfernatur", "Trainingsweltmeister", "trainingsfauler Spieler", "Treter", "fairer Spieler", "Mimose", "Schlitzohr", "Spezialist",
            "Allrounder", "flexibler Spieler", "Schiriliebling", "Heimspieler", "Schönwetterfußballer", "Joker", "Egoist", "Bruder Leichtfuß",
        };

        /// <summary>Character bits (table 0x008A8590).</summary>
        internal static readonly string[] Character =
        {
            "Führungsperson", "Hitzkopf", "Frohnatur", "Mann ohne Nerven", "Nervenbündel", "Phlegmatiker", "Geldgeier", "Vereinsanhänger",
            "Musterprofi", "Skandalnudel", "Sensibelchen", "Starallüren", "Teenie-Star", "Unruhestifter", "Leberwurst", "Integrationsfigur",
        };

        /// <summary>The skill names that apply to a player at the given position.</summary>
        internal static string[] SkillsFor(int position) => position == Goalkeeper ? GoalkeeperSkills : FieldSkills;

        /// <summary>Position name for a stored value, "?" for values outside the table.</summary>
        internal static string PositionName(int value) => value >= 0 && value < Positions.Length ? Positions[value] : "?";
    }
}
