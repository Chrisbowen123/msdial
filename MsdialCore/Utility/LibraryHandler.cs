﻿using CompMs.Common.Components;
using CompMs.Common.DataObj.Result;
using CompMs.Common.Enum;
using CompMs.Common.Extension;
using CompMs.Common.Parameter;
using CompMs.Common.Parser;
using CompMs.Common.Proteomics.DataObj;
using CompMs.Common.Proteomics.Function;
using CompMs.Common.Query;
using CompMs.MsdialCore.DataObj;
using CompMs.MsdialCore.Enum;
using CompMs.MsdialCore.Parameter;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CompMs.MsdialCore.Utility {
    public sealed class LibraryHandler {
        private LibraryHandler() { }

        public static List<MoleculeMsReference> ReadLipidMsLibrary(string filepath, ParameterBase param) {
            return ReadLipidMsLibrary(filepath, param.LipidQueryContainer, param.IonMode);
        }

        public static List<MoleculeMsReference> ReadLipidMsLibrary(string filepath, LipidQueryBean lipidQueries, IonMode ionMode) {

            var collosionType = lipidQueries.CollisionType;
            var solventType = lipidQueries.SolventType;

            var queries = new List<LbmQuery>();

            foreach (var lQuery in lipidQueries.LbmQueries) {
                if (lQuery.IsSelected == true && lQuery.IonMode == ionMode)
                    queries.Add(lQuery);
            }

            List<MoleculeMsReference> mspQueries = null;
            var extension = Path.GetExtension(filepath).ToLower();
            if (extension == ".lbm")
                mspQueries = MspFileParser.LbmFileReader(filepath, queries, ionMode, solventType, collosionType);
            else if (extension == ".lbm2")
                mspQueries = MspFileParser.ReadSerializedLbmLibrary(filepath, queries, ionMode, solventType, collosionType);

            return mspQueries;
        }

        public static List<MoleculeMsReference> ReadMspLibrary(string filepath) {
            List<MoleculeMsReference> mspQueries = null;
            var extension = System.IO.Path.GetExtension(filepath).ToLower();
            if (extension.Contains("2"))
                mspQueries = MspFileParser.ReadSerializedMspObject(filepath);
            else
                mspQueries = MspFileParser.MspFileReader(filepath);

            return mspQueries;
        }

        //public static ShotgunProteomicsDB GenerateShotgunProteomicsDB(string file, string id, ProteomicsParameter proteomicsParam, MsRefSearchParameterBase msrefSearchParam) {
        //    var db = new ShotgunProteomicsDB(file, id, proteomicsParam, msrefSearchParam.MassRangeBegin, msrefSearchParam.MassRangeEnd);
        //    return db;
        //}


        public static List<Peptide> GenerateTargetPeptideReference(List<FastaProperty> quereis,
                List<string> cleavageSites, ModificationContainer modContainer, ProteomicsParameter parameter) {
            var maxMissedCleavage = parameter.MaxMissedCleavage;
            var maxNumberOfModificationsPerPeptide = parameter.MaxNumberOfModificationsPerPeptide;
            var adduct = AdductIonParser.GetAdductIonBean("[M+H]+");
            var minimumPeptideLength = parameter.MinimumPeptideLength;
            var maxPeptideMass = parameter.MaxPeptideMass;
            var char2AA = PeptideCalc.GetSimpleChar2AminoAcidDictionary();
            var syncObj = new object();
            var error = string.Empty;
            var peptides = new List<Peptide>();
            var sequence2Count = new Dictionary<string, int>();

            Parallel.ForEach(quereis, fQuery => {
                if (fQuery.IsValidated) {
                    var sequence = fQuery.Sequence;
                    var digestedPeptides = ProteinDigestion.GetDigestedPeptideSequences(sequence, cleavageSites, char2AA, maxMissedCleavage, fQuery.UniqueIdentifier, fQuery.Index);
                    if (!digestedPeptides.IsEmptyOrNull()) {
                        var mPeptides = ModificationUtility.GetModifiedPeptides(digestedPeptides, modContainer, maxNumberOfModificationsPerPeptide);
                        lock (syncObj) {
                            foreach (var peptide in mPeptides) {
                                peptides.Add(peptide);
                            }

                            // generating peptidekey2unique dictionary
                            foreach (var oPeptide in digestedPeptides) {
                                var pepSeq = oPeptide.Sequence;
                                if (sequence2Count.ContainsKey(pepSeq)) {
                                    sequence2Count[pepSeq]++;
                                }
                                else {
                                    sequence2Count[pepSeq] = 1;
                                }
                            }
                        }
                    }
                }
            });
            foreach (var peptide in peptides) {
                //Console.WriteLine(peptide.ModifiedSequence);
                peptide.SamePeptideNumberInSearchedProteins = sequence2Count[peptide.Sequence];
            }

            return peptides.OrderBy(n => n.ExactMass).ToList();
        }

        public static List<Peptide> GenerateFastTargetPeptideReference(List<FastaProperty> quereis,
               List<string> cleavageSites, ModificationContainer modContainer, ProteomicsParameter parameter) {
            var maxMissedCleavage = parameter.MaxMissedCleavage;
            var maxNumberOfModificationsPerPeptide = parameter.MaxNumberOfModificationsPerPeptide;
            var adduct = AdductIonParser.GetAdductIonBean("[M+H]+");
            var minimumPeptideLength = parameter.MinimumPeptideLength;
            var maxPeptideMass = parameter.MaxPeptideMass;
            var char2AA = PeptideCalc.GetSimpleChar2AminoAcidDictionary();
            var syncObj = new object();
            var error = string.Empty;
            var peptides = new List<Peptide>();
            var sequence2Count = new Dictionary<string, int>();

            Parallel.ForEach(quereis, fQuery => {
                if (fQuery.IsValidated) {
                    var sequence = fQuery.Sequence;
                    var digestedPeptides = ProteinDigestion.GetDigestedPeptideSequences(sequence, cleavageSites, char2AA, maxMissedCleavage, fQuery.UniqueIdentifier, fQuery.Index);
                    if (!digestedPeptides.IsEmptyOrNull()) {
                        var mPeptides = ModificationUtility.GetFastModifiedPeptides(digestedPeptides, modContainer, maxNumberOfModificationsPerPeptide);
                        lock (syncObj) {
                            foreach (var peptide in mPeptides) {
                                peptides.Add(peptide);
                            }

                            // generating peptidekey2unique dictionary
                            foreach (var oPeptide in digestedPeptides) {
                                var pepSeq = oPeptide.Sequence;
                                if (sequence2Count.ContainsKey(pepSeq)) {
                                    sequence2Count[pepSeq]++;
                                }
                                else {
                                    sequence2Count[pepSeq] = 1;
                                }
                            }
                        }
                    }
                }
            });
            foreach (var peptide in peptides) {
                //Console.WriteLine(peptide.ModifiedSequence);
                peptide.SamePeptideNumberInSearchedProteins = sequence2Count[peptide.Sequence];
            }

            return peptides.OrderBy(n => n.ExactMass).ToList();
        }


        public static List<Peptide> GenerateDecoyPeptideReference(List<Peptide> forwardPeps) {
            if (forwardPeps.IsEmptyOrNull()) return null;
            var pepArray = new Peptide[forwardPeps.Count];
            Parallel.For(0, forwardPeps.Count, i => {
                var decoyPep = DecoyCreator.Convert2DecoyPeptide(forwardPeps[i]);
                pepArray[i] = decoyPep;
            });

            return new List<Peptide>(pepArray);
        }
    }
}
