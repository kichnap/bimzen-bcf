using System;
using System.Text.RegularExpressions;
using Bcf.Core.Conversion;
using Xunit;

namespace Bcf.Core.Tests
{
    public class StableTopicKeyTests
    {
        private static readonly string[] Pair = { "2SugUv4EX5LAhcVpDp2dUH", "3woirKUVTF1wlWXy6aBfQJ" };

        [Fact]
        public void SameInput_GivesSameKey()
        {
            Assert.Equal(
                StableTopicKey.ForClash("ОВ vs КР", Pair),
                StableTopicKey.ForClash("ОВ vs КР", Pair));
        }

        [Fact]
        public void ElementOrder_DoesNotMatter()
        {
            // Navisworks may swap element 1 and element 2 between runs — that must not
            // affect the key
            Assert.Equal(
                StableTopicKey.ForClash("ОВ vs КР", new[] { Pair[0], Pair[1] }),
                StableTopicKey.ForClash("ОВ vs КР", new[] { Pair[1], Pair[0] }));
        }

        [Fact]
        public void DifferentTest_GivesDifferentKey()
        {
            Assert.NotEqual(
                StableTopicKey.ForClash("ОВ vs КР", Pair),
                StableTopicKey.ForClash("ВК vs КР", Pair));
        }

        [Fact]
        public void DifferentElements_GiveDifferentKey()
        {
            Assert.NotEqual(
                StableTopicKey.ForClash("ОВ vs КР", Pair),
                StableTopicKey.ForClash("ОВ vs КР", new[] { Pair[0], "1$1j4xEDn78A9oA4mCPCkL" }));
        }

        [Fact]
        public void WhitespaceAndDuplicates_AreIgnored()
        {
            Assert.Equal(
                StableTopicKey.ForClash("ОВ vs КР", Pair),
                StableTopicKey.ForClash("  ОВ vs КР  ", new[] { Pair[1], " " + Pair[0] + " ", Pair[1], null }));
        }

        [Fact]
        public void GroupKey_IgnoresMembership()
        {
            // The membership of a group changes from export to export; counting the key
            // over the membership would give a new topic on every change — a duplicate
            // exactly where the grouping is wanted
            Assert.Equal(
                StableTopicKey.ForGroup("ОВ vs КР", "Этаж 3"),
                StableTopicKey.ForGroup("ОВ vs КР", "Этаж 3"));

            Assert.NotEqual(
                StableTopicKey.ForGroup("ОВ vs КР", "Этаж 3"),
                StableTopicKey.ForGroup("ОВ vs КР", "Этаж 4"));
        }

        [Fact]
        public void Key_IsHexSha256()
        {
            string key = StableTopicKey.ForClash("test", Pair);

            Assert.Matches("^[0-9a-f]{64}$", key);
        }

        [Fact]
        public void TopicGuid_IsDeterministic()
        {
            string key = StableTopicKey.ForClash("ОВ vs КР", Pair);

            Assert.Equal(StableTopicKey.ToTopicGuid(key), StableTopicKey.ToTopicGuid(key));
        }

        [Fact]
        public void TopicGuid_MatchesBcfPattern()
        {
            // The 3.0 schema checks identifiers against a pattern and rejects upper case
            string guid = StableTopicKey.FormatGuid(StableTopicKey.ToTopicGuid(StableTopicKey.ForClash("t", Pair)));

            Assert.Matches("^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$", guid);
        }

        [Fact]
        public void TopicGuid_CarriesRfc4122Variant()
        {
            Guid guid = StableTopicKey.ToTopicGuid(StableTopicKey.ForClash("t", Pair));
            byte[] bytes = guid.ToByteArray();

            // The RFC 4122 variant in the high bits of the ninth byte
            Assert.Equal(0x80, bytes[8] & 0xC0);
        }

        [Fact]
        public void DifferentKeys_GiveDifferentGuids()
        {
            Assert.NotEqual(
                StableTopicKey.ToTopicGuid(StableTopicKey.ForClash("a", Pair)),
                StableTopicKey.ToTopicGuid(StableTopicKey.ForClash("b", Pair)));
        }

        [Fact]
        public void EmptyKey_Throws()
        {
            Assert.Throws<ArgumentException>(() => StableTopicKey.ToTopicGuid("  "));
        }

        [Fact]
        public void KnownInput_HasPinnedKeyAndGuid()
        {
            // The values are pinned on purpose. The key and the identifier derived from
            // it are what ties the exports of different weeks to the records on a server.
            // Any change to the algorithm duplicates every topic at the client, so a test
            // like this one has to fail and make somebody think about it rather than let
            // the behaviour change quietly.
            string key = StableTopicKey.ForClash(
                "Clash Test 1",
                new[] { "1111111111111111111111", "2222222222222222222222" });

            Assert.Equal("1d5b52aa10366bb8b87145df767892e758035a149656b19a74f4e7c3341094b6", key);
            Assert.Equal("614ce774-2268-8a69-a7c8-71b52ea587f4", StableTopicKey.FormatGuid(StableTopicKey.ToTopicGuid(key)));
        }
    }
}
