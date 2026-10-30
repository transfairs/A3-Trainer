using A3_Trainer.Controller;
using A3_Trainer.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace A3_Trainer.Tests
{
    /// <summary>
    /// Keeps the Cheat Engine table (A3-Trainer.ct) in step with the trainer: the addresses, offsets
    /// and value tables in its Lua script and its fixed entries must match the C# definitions.
    /// </summary>
    public class CheatTableTests
    {
        private const uint ImageBase = 0x400000;

        private static readonly XDocument Table = XDocument.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "A3-Trainer.ct"));

        private static string Script =>
            Table.Descendants("AssemblerScript").Single().Value;

        // "local A = { Name = 0x1F, Other = 12, ... }" -> name/value pairs
        private static Dictionary<string, long> LuaNumbers(string table)
        {
            Match block = Regex.Match(Script, @"local " + table + @" = \{(.*?)\n\}", RegexOptions.Singleline);
            Assert.True(block.Success, $"Lua table {table} not found");
            return Regex.Matches(block.Groups[1].Value, @"(\w+) = (0x[0-9A-Fa-f]+|\d+)")
                .Cast<Match>()
                .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value.StartsWith("0x")
                    ? long.Parse(m.Groups[2].Value.Substring(2), NumberStyles.HexNumber)
                    : long.Parse(m.Groups[2].Value));
        }

        private static long Constant(Type type, string name)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.True(field != null && field.IsLiteral, $"{type.Name}.{name} doesn't exist");
            return Convert.ToInt64(field.GetRawConstantValue());
        }

        [Theory]
        [InlineData("A", typeof(A3Addresses))]
        [InlineData("R", typeof(RecordOffset))]
        [InlineData("C", typeof(CompactOffset))]
        public void LuaAddressesMatchTheTrainer(string luaTable, Type type)
        {
            Dictionary<string, long> values = LuaNumbers(luaTable);
            Assert.NotEmpty(values);
            foreach (KeyValuePair<string, long> value in values)
                Assert.True(Constant(type, value.Key) == value.Value, $"{luaTable}.{value.Key} = 0x{value.Value:X}, trainer has 0x{Constant(type, value.Key):X}");
        }

        [Theory]
        [InlineData("Positions")]
        [InlineData("Talents")]
        [InlineData("Feet")]
        [InlineData("FieldSkills")]
        [InlineData("GoalkeeperSkills")]
        [InlineData("Traits")]
        [InlineData("Character")]
        public void LuaNamesMatchTheTrainer(string list)
        {
            Match line = Regex.Match(Script, @"\n  " + list + @" = \{ (.*?) \},");
            Assert.True(line.Success, $"Lua list {list} not found");
            string[] names = Regex.Matches(line.Groups[1].Value, "\"([^\"]*)\"").Cast<Match>().Select(m => m.Groups[1].Value).ToArray();
            var expected = (string[])typeof(Tables).GetField(list, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            Assert.Equal(expected, names);
        }

        [Fact]
        public void GoalkeeperValueMatchesTheTrainer()
        {
            Assert.Contains($"local Goalkeeper = {Tables.Goalkeeper}\n", Script.Replace("\r\n", "\n"));
        }

        [Fact]
        public void FixedEntriesPointAtKnownAddresses()
        {
            var known = typeof(A3Addresses).GetFields(BindingFlags.Static | BindingFlags.NonPublic)
                .Where(f => f.IsLiteral && f.FieldType == typeof(uint))
                .Select(f => (uint)f.GetRawConstantValue())
                .ToHashSet();

            List<string> addresses = Table.Descendants("Address").Select(a => a.Value).ToList();
            Assert.NotEmpty(addresses);
            foreach (string address in addresses)
            {
                Match m = Regex.Match(address, "^anstoss3\\.exe\\+([0-9A-F]+)$");
                Assert.True(m.Success, $"unexpected address {address}");
                uint absolute = ImageBase + uint.Parse(m.Groups[1].Value, NumberStyles.HexNumber);
                Assert.True(known.Contains(absolute), $"{address} (0x{absolute:X8}) isn't in A3Addresses");
            }
        }

        [Fact]
        public void IdsAreUnique()
        {
            List<string> ids = Table.Descendants("ID").Select(i => i.Value).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());
        }

        [Fact]
        public void FileHasNoByteOrderMark()
        {
            // Cheat Engine refuses a table that starts with a UTF-8 BOM ("data error").
            byte[] start = File.ReadAllBytes(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "A3-Trainer.ct")).Take(3).ToArray();
            Assert.NotEqual(new byte[] { 0xEF, 0xBB, 0xBF }, start);
        }
    }
}
