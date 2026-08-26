using System;
using Bcf.Core.Conversion;
using Xunit;

namespace Bcf.Core.Tests
{
    public class IfcGuidConverterTests
    {
        /// <summary>
        /// Real GlobalId values from the buildingSMART reference model
        /// (BCF-XML, Test Cases/IFCs/Architectural.ifc). They test the parsing
        /// against data written by someone else's implementation, not ours.
        /// </summary>
        public static TheoryData<string> RealIfcGuids => new TheoryData<string>
        {
            "2SugUv4EX5LAhcVpDp2dUH",
            "3woirKUVTF1wlWXy6aBfQJ",
            "1$1j4xEDn78A9oA4mCPCkL",
            "1bbI761TbBCOoIa5Kt6PXt",
            "1E8YkwPMfB$h99jtn_uAjI",
            "3XkFdvSY15ygxIfmwr$7Mi"
        };

        [Theory]
        [MemberData(nameof(RealIfcGuids))]
        public void RealIfcGuid_SurvivesRoundTrip(string ifcGuid)
        {
            Guid guid = IfcGuidConverter.FromIfcGuid(ifcGuid);

            Assert.Equal(ifcGuid, IfcGuidConverter.ToIfcGuid(guid));
        }

        [Fact]
        public void RandomGuids_SurviveRoundTrip()
        {
            for (int i = 0; i < 500; i++)
            {
                Guid guid = Guid.NewGuid();
                string ifcGuid = IfcGuidConverter.ToIfcGuid(guid);

                Assert.Equal(IfcGuidConverter.IfcGuidLength, ifcGuid.Length);
                Assert.Equal(guid, IfcGuidConverter.FromIfcGuid(ifcGuid));
            }
        }

        [Fact]
        public void EmptyGuid_IsAllZeroDigits()
        {
            Assert.Equal(new string('0', IfcGuidConverter.IfcGuidLength), IfcGuidConverter.ToIfcGuid(Guid.Empty));
        }

        [Fact]
        public void FirstCharacter_CarriesOnlyTwoBits()
        {
            // 22 characters of 6 bits give 132 bits where 128 are significant: the
            // leading character cannot exceed '3'. If it does, the split into groups has
            // drifted, and other implementations will read the file wrongly.
            for (int i = 0; i < 200; i++)
            {
                string ifcGuid = IfcGuidConverter.ToIfcGuid(Guid.NewGuid());

                Assert.True(IfcGuidConverter.Alphabet.IndexOf(ifcGuid[0]) <= 3, "Первый символ: " + ifcGuid[0]);
            }
        }

        [Fact]
        public void AllCharacters_AreFromBuildingSmartAlphabet()
        {
            string ifcGuid = IfcGuidConverter.ToIfcGuid(Guid.NewGuid());

            foreach (char c in ifcGuid)
            {
                Assert.True(IfcGuidConverter.Alphabet.IndexOf(c) >= 0, "Символ вне алфавита: " + c);
            }
        }

        [Fact]
        public void RevitUniqueId_XorsElementIdIntoGuidTail()
        {
            // The tail of the episode identifier 00000001 is XORed with the element
            // identifier 000000ff and gives 000000fe — that is the whole algorithm.
            Guid guid = IfcGuidConverter.FromRevitUniqueId("1a2b3c4d-5e6f-4a7b-8c9d-000000000001-000000ff");

            Assert.Equal(Guid.ParseExact("1a2b3c4d-5e6f-4a7b-8c9d-0000000000fe", "D"), guid);
        }

        [Fact]
        public void RevitUniqueId_DifferentElements_GiveDifferentIds()
        {
            // The property that matters: elements of one file differ only in the tail.
            // Taking the episode identifier as it is would collapse every element into
            // one identifier, and the highlight in a receiving tool would mean nothing.
            string first = IfcGuidConverter.RevitUniqueIdToIfcGuid("1a2b3c4d-5e6f-4a7b-8c9d-000000000001-000000ff");
            string second = IfcGuidConverter.RevitUniqueIdToIfcGuid("1a2b3c4d-5e6f-4a7b-8c9d-000000000001-00000100");

            Assert.NotEqual(first, second);
        }

        [Fact]
        public void RevitUniqueId_LongElementId_UsesLowerBits()
        {
            // From Revit 2024 the identifiers are 64-bit; the low 32 bits take part in the XOR
            Guid guid = IfcGuidConverter.FromRevitUniqueId("1a2b3c4d-5e6f-4a7b-8c9d-000000000000-1000000ff");

            Assert.Equal(Guid.ParseExact("1a2b3c4d-5e6f-4a7b-8c9d-0000000000ff", "D"), guid);
        }

        [Fact]
        public void RevitUniqueId_IsRecognized()
        {
            Assert.True(IfcGuidConverter.IsRevitUniqueId("1a2b3c4d-5e6f-4a7b-8c9d-000000000001-000000ff"));
            Assert.False(IfcGuidConverter.IsRevitUniqueId("2SugUv4EX5LAhcVpDp2dUH"));
            Assert.False(IfcGuidConverter.IsRevitUniqueId("1a2b3c4d-5e6f-4a7b-8c9d-000000000001"));
            Assert.False(IfcGuidConverter.IsRevitUniqueId(null));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("short")]
        [InlineData("2SugUv4EX5LAhcVpDp2dU")]      // 21 символ
        [InlineData("2SugUv4EX5LAhcVpDp2dUHH")]    // 23 символа
        [InlineData("2SugUv4EX5LAhcVpDp2d-H")]     // дефис вне алфавита
        [InlineData("9SugUv4EX5LAhcVpDp2dUH")]     // старший символ больше '3'
        public void InvalidIfcGuid_IsRejected(string value)
        {
            Assert.False(IfcGuidConverter.IsValidIfcGuid(value));
            Assert.Throws<ArgumentException>(() => IfcGuidConverter.FromIfcGuid(value));
        }

        [Theory]
        [InlineData("")]
        [InlineData("not-a-unique-id")]
        [InlineData("zzzzzzzz-5e6f-4a7b-8c9d-000000000001-000000ff")]
        [InlineData("1a2b3c4d-5e6f-4a7b-8c9d-000000000001-zzzzzzzz")]
        public void InvalidRevitUniqueId_Throws(string value)
        {
            Assert.Throws<ArgumentException>(() => IfcGuidConverter.FromRevitUniqueId(value));
        }
    }
}
