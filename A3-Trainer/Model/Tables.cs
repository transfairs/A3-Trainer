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

        /// <summary>Health types, one bit each in bits 0-6 of the health word (1 &lt;&lt; (editor value - 1), FUN_0068C903).</summary>
        internal static readonly string[] HealthTypes =
        {
            "normal", "robust", "anfällig", "Knieprobleme", "schnell wieder fit", "dauert bis wieder fit", "wehleidig",
        };

        /// <summary>Bits of the health word that hold the health type.</summary>
        internal const int HealthTypeMask = 0x7F;

        /// <summary>Leagues a contract is valid in, the contract flags' bits 0-1 (texts 4086-4089).</summary>
        internal static readonly string[] ContractValidity = { "alle Ligen", "nur erste Liga", "mind. 2. Liga", "mind. 3. Liga" };

        /// <summary>
        /// Season goals a club's board sets (texts 1069-1080); a lower number is the harder goal, the game rates the
        /// final position as one of these levels (FUN_0077600E).
        /// </summary>
        internal static readonly string[] SeasonGoals =
        {
            "Meisterschaft", "Erste Drei", "Aufstieg", "Intern. Wettbewerb", "oben mitspielen", "einstelliger Platz",
            "gute Show", "guter Fußball", "Mittelfeldplatz", "ehrliche Arbeit", "Klassenerhalt", "zu Ende bringen",
        };

        /// <summary>Highest form value the game sets (FUN_006FF0D0).</summary>
        internal const int MaxFormBase = 20;

        /// <summary>Skin colours, bits 0-2 of the appearance byte.</summary>
        internal static readonly string[] SkinColors = { "hell", "dunkel", "schwarz", "asiatisch" };

        /// <summary>Hair colours, bits 0-3 of the hair byte (database column after the skin colour).</summary>
        internal static readonly string[] HairColors = { "hellblond", "blond", "braun", "rot", "schwarz", "Glatze", "grau" };

        /// <summary>
        /// Hair types, bits 4-7 of the hair byte. The editor offers 0-3; the game's own generator also
        /// sets 4, the only value that gets the fourth hair model of the 3D match (table at 0x008A6A90:
        /// 0 and 1 share a model).
        /// </summary>
        internal static readonly string[] HairTypes = { "extrem kurz", "kurz", "Wuschelkopf", "lang", "Sonderfrisur" };

        /// <summary>
        /// Beards, one bit each in bits 4-7 of the appearance byte. Version 1.40 doesn't show them: its
        /// beard getter (FUN_007077AB) returns the skin colour.
        /// </summary>
        internal static readonly string[] Beards = { "unrasiert", "Bart", "Schnurrbart", "Ziegenbart" };

        /// <summary>Relationship, bits 0-1 of the relationship byte (text ids 4022-4025 via table 0x00888C08).</summary>
        internal static readonly string[] Relationships = { "keine", "Freundin", "verlobt", "verheiratet" };
        internal const int RelationshipMask = 0x03;

        /// <summary>State of the relationship, bits 2-4 of the relationship byte (text ids 4026-4030 via table 0x00888C10).</summary>
        internal static readonly string[] RelationshipStates = { "zusammen", "getrennt", "geschieden", "auseinander", "Scheidung" };
        internal const int RelationshipStateShift = 2;
        internal const int RelationshipStateMask = 0x1C;

        /// <summary>Hobbies by their value in the record (text ids via table 0x008A8C7E), 0 = none.</summary>
        internal static readonly string[] Hobbies =
        {
            "keins", "Ameisen kartieren", "Angeln", "ANSTOSS 3", "Aquarium", "Billard", "Börse", "Briefmarken sammeln",
            "Bücher schreiben", "Computerspielen", "Familie", "Football", "Frauen", "Frösche sammeln", "Fußball",
            "Gartenarbeit", "Gitarre spielen", "Glücksspiel", "Golf", "Gut essen gehen", "Imkerei", "Kampfsport", "Kino",
            "Kirchenchor", "Kunst sammeln", "Lesen", "Modellbau", "Modelleisenbahn", "Motorrad fahren", "Musik hören",
            "Radfahren", "Reisen", "Schwimmen", "Segeln", "Ski fahren", "Tauchen", "Tennis", "Terrierzucht", "Teure Autos",
            "Wandern",
        };

        /// <summary>Popularity levels of the manager above each threshold (table 0x0087F848).</summary>
        internal static string Popularity(int value) =>
            Level(value, new[] { 95, 87, 79, 69, 59, 49, 39, 19 },
                "Gottstatus", "Supermann", "Sehr gut", "Angesehen", "Mittel", "Gering", "Unbeliebt", "Rotes Tuch", "Feindbild");

        /// <summary>The board's verdict on its trust in percent, shortened from the game's texts 2470-2476 (FUN_005C6C1F).</summary>
        internal static string BoardTrust(int percent, string club) =>
            percent >= 100 ? "König von " + club : percent >= 90 ? "absolut unumstritten" : percent >= 80 ? "breites Vertrauen"
            : percent >= 70 ? "ziemliche Anzahl Kritiker" : percent >= 60 ? "keine sichere Stellung mehr"
            : percent >= 55 ? "Rauswurf steht unmittelbar bevor" : "ein Wunder, noch da zu sein";

        /// <summary>Club image levels above each threshold (table 0x0087F890); the game keeps the value in 10..50.</summary>
        internal static string ClubImage(int value) =>
            Level(value, new[] { 44, 34, 24, 14 }, "sehr beliebt", "recht angesehen", "neutral", "wenig angesehen", "vollkommen verhaßt");

        /// <summary>Standing with the association by trouble points (FUN_00695073): the fewer, the better.</summary>
        internal static string Association(int trouble) =>
            trouble < 2 ? "ausgezeichnet" : trouble < 5 ? "angesehen" : trouble < 9 ? "normal" : trouble < 23 ? "bescheiden" : "verzockt";

        internal const int AssociationTroubleMask = 0x1F;

        private static string Level(int value, int[] above, params string[] names)
        {
            int i = 0;
            while (i < above.Length && value <= above[i])
                i++;
            return names[i];
        }

        /// <summary>The skill names that apply to a player at the given position.</summary>
        internal static string[] SkillsFor(int position) => position == Goalkeeper ? GoalkeeperSkills : FieldSkills;

        /// <summary>Position name for a stored value, "?" for values outside the table.</summary>
        internal static string PositionName(int value) => value >= 0 && value < Positions.Length ? Positions[value] : "?";
    }
}
