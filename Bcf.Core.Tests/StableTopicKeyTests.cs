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
            // Navisworks может поменять местами элемент 1 и элемент 2 между
            // прогонами — на ключ это влиять не должно
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
            // Состав группы меняется от выгрузки к выгрузке; если считать ключ
            // по составу, каждое изменение давало бы новый топик — дубль ровно
            // там, где группировка и нужна
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
            // Схема 3.0 проверяет GUID шаблоном и заглавные буквы отвергает
            string guid = StableTopicKey.FormatGuid(StableTopicKey.ToTopicGuid(StableTopicKey.ForClash("t", Pair)));

            Assert.Matches("^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$", guid);
        }

        [Fact]
        public void TopicGuid_CarriesRfc4122Variant()
        {
            Guid guid = StableTopicKey.ToTopicGuid(StableTopicKey.ForClash("t", Pair));
            byte[] bytes = guid.ToByteArray();

            // Вариант RFC 4122 в старших битах девятого байта
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
            // Значения закреплены намеренно. Ключ и выведенный из него GUID —
            // это то, чем связаны выгрузки разных недель и записи на сервере.
            // Любое изменение алгоритма продублирует у клиента все топики,
            // поэтому такой тест обязан упасть и заставить об этом подумать,
            // а не тихо поменять поведение.
            string key = StableTopicKey.ForClash(
                "Clash Test 1",
                new[] { "1111111111111111111111", "2222222222222222222222" });

            Assert.Equal("1d5b52aa10366bb8b87145df767892e758035a149656b19a74f4e7c3341094b6", key);
            Assert.Equal("614ce774-2268-8a69-a7c8-71b52ea587f4", StableTopicKey.FormatGuid(StableTopicKey.ToTopicGuid(key)));
        }
    }
}
