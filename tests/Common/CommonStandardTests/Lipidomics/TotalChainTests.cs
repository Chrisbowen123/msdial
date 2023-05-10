﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace CompMs.Common.Lipidomics.Tests
{
    [TestClass()]
    public class TotalChainTests
    {
        [TestMethod()]
        public void TotalChainsTest() {
            var chains = new TotalChain(36, 2, 0, 2, 0, 0);
            Assert.AreEqual(36, chains.CarbonCount);
            Assert.AreEqual(2, chains.DoubleBondCount);
            Assert.AreEqual(0, chains.OxidizedCount);
            Assert.AreEqual(2, chains.ChainCount);
            Assert.AreEqual(530.5062805078201, chains.Mass, 1e-5);
            Assert.AreEqual("36:2", chains.ToString());
            var mockGenerator = new FakeGenerator();
            var candidates = ((ITotalChain)chains).GetCandidateSets(mockGenerator);
            CollectionAssert.AreEqual(new ITotalChain[0], candidates.ToArray());

            chains = new TotalChain(36, 2, 0, 1, 1, 0);
            Assert.AreEqual(36, chains.CarbonCount);
            Assert.AreEqual(2, chains.DoubleBondCount);
            Assert.AreEqual(0, chains.OxidizedCount);
            Assert.AreEqual(2, chains.ChainCount);
            Assert.AreEqual(516.5270163763599, chains.Mass, 1e-5);
            Assert.AreEqual("O-36:2", chains.ToString());

            chains = new TotalChain(52, 3, 0, 3, 0, 0);
            Assert.AreEqual(52, chains.CarbonCount);
            Assert.AreEqual(3, chains.DoubleBondCount);
            Assert.AreEqual(0, chains.OxidizedCount);
            Assert.AreEqual(3, chains.ChainCount);
            Assert.AreEqual(767.7281206334501, chains.Mass, 1e-5);
            Assert.AreEqual("52:3", chains.ToString());

            chains = new TotalChain(34, 1, 2, 1, 0, 1);
            Assert.AreEqual(34, chains.CarbonCount);
            Assert.AreEqual(1, chains.DoubleBondCount);
            Assert.AreEqual(2, chains.OxidizedCount);
            Assert.AreEqual(2, chains.ChainCount);
            Assert.AreEqual(537.5120937402901, chains.Mass, 1e-5);
            Assert.AreEqual("34:1;2O", chains.ToString());
        }

        [TestMethod]
        [DynamicData(nameof(IncludesTestData), DynamicDataSourceType.Property)]
        public void IncludesTest(ITotalChain chains1, ITotalChain chains2, bool expected) {
            var actual = chains1.Includes(chains2);
            Assert.AreEqual(expected, actual);
        }

        public static IEnumerable<object[]> IncludesTestData {
            get {
                ITotalChain chains3600 = new TotalChain(36, 0, 0, 0, 0, 0), chains3610 = new TotalChain(36, 1, 0, 0, 0, 0), chains3601 = new TotalChain(36, 0, 1, 0, 0, 0),
                    chains180_181 = new MolecularSpeciesLevelChains(new AcylChain(18, DoubleBond.CreateFromPosition(), Oxidized.CreateFromPosition()), new AcylChain(18, new DoubleBond(1), Oxidized.CreateFromPosition()));
                yield return new object[] { chains3610, chains3610, true, };
                yield return new object[] { chains3610, chains180_181, true, };
                yield return new object[] { chains3600, chains3610, false, };
                yield return new object[] { chains3600, chains3601, false, };
            }
        }

        class FakeGenerator : ITotalChainVariationGenerator
        {
            public bool Called { get; private set; } = false;

            public IEnumerable<ITotalChain> Permutate(MolecularSpeciesLevelChains chains) {
                throw new System.NotImplementedException();
            }

            public IEnumerable<ITotalChain> Product(PositionLevelChains chains) {
                throw new System.NotImplementedException();
            }

            public IEnumerable<ITotalChain> Separate(TotalChain chain) {
                Called = true;
                return Enumerable.Empty<ITotalChain>();
            }
        }
    }
}