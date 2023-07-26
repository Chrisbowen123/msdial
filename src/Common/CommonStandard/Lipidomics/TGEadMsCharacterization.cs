﻿using CompMs.Common.Components;
using CompMs.Common.Interfaces;

namespace CompMs.Common.Lipidomics
{
    public static class TGEadMsCharacterization
    {
        public static (ILipid, double[]) Characterize(
            IMSScanProperty scan, ILipid molecule, MoleculeMsReference reference,
            float tolerance, float mzBegin, float mzEnd)
        {
            var class_cutoff = 0;
            var chain_cutoff = 2;
            var position_cutoff = 1;
            var double_cutoff = 0.5;

            if (molecule.Chains.ChainCount > 1) {
                if (molecule.Chains.GetChain(1).CarbonCount == molecule.Chains.GetChain(2).CarbonCount &&
                    molecule.Chains.GetChain(2).CarbonCount == molecule.Chains.GetChain(3).CarbonCount &&
                    molecule.Chains.GetChain(1).DoubleBond == molecule.Chains.GetChain(2).DoubleBond &&
                    molecule.Chains.GetChain(2).DoubleBond == molecule.Chains.GetChain(3).DoubleBond) {
                    chain_cutoff = 1;
                } 
                else if (molecule.Chains.GetChain(1).CarbonCount == molecule.Chains.GetChain(2).CarbonCount &&
                    molecule.Chains.GetChain(2).CarbonCount != molecule.Chains.GetChain(3).CarbonCount &&
                    molecule.Chains.GetChain(1).DoubleBond == molecule.Chains.GetChain(2).DoubleBond &&
                    molecule.Chains.GetChain(2).DoubleBond != molecule.Chains.GetChain(3).DoubleBond) {
                    chain_cutoff = 2;
                }
                else if (molecule.Chains.GetChain(1).CarbonCount != molecule.Chains.GetChain(2).CarbonCount &&
                    molecule.Chains.GetChain(2).CarbonCount == molecule.Chains.GetChain(3).CarbonCount &&
                    molecule.Chains.GetChain(1).DoubleBond != molecule.Chains.GetChain(2).DoubleBond &&
                    molecule.Chains.GetChain(2).DoubleBond == molecule.Chains.GetChain(3).DoubleBond) {
                    chain_cutoff = 2;
                }
                else if (molecule.Chains.GetChain(1).CarbonCount == molecule.Chains.GetChain(3).CarbonCount &&
                    molecule.Chains.GetChain(2).CarbonCount != molecule.Chains.GetChain(3).CarbonCount &&
                    molecule.Chains.GetChain(1).DoubleBond == molecule.Chains.GetChain(3).DoubleBond &&
                    molecule.Chains.GetChain(2).DoubleBond != molecule.Chains.GetChain(3).DoubleBond) {
                    chain_cutoff = 2;
                }
                else {
                    chain_cutoff = 3;
                }
            }
            if (reference.AdductType.AdductIonName == "[M+NH4]+") {
                position_cutoff = 0;
            }

            var defaultResult = EieioMsCharacterizationUtility.GetDefaultScore(
                    scan, reference, tolerance, mzBegin, mzEnd, class_cutoff, chain_cutoff, position_cutoff, double_cutoff);
            return StandardMsCharacterizationUtility.GetDefaultCharacterizationResultForTriacylGlycerols(molecule, defaultResult);
        }
    }
}
