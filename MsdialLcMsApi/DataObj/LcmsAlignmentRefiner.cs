﻿using System;
using System.Collections.Generic;
using System.Linq;
using CompMs.Common.Components;
using CompMs.Common.Enum;
using CompMs.Common.Extension;
using CompMs.Common.Mathematics.Basic;
using CompMs.Common.Parser;
using CompMs.Common.Utility;
using CompMs.MsdialCore.DataObj;
using CompMs.MsdialCore.Parameter;
using CompMs.MsdialLcmsApi.Parameter;

namespace CompMs.MsdialLcMsApi.DataObj
{
    public class LcmsAlignmentRefiner : AlignmentRefiner
    {
        public LcmsAlignmentRefiner(MsdialLcmsParameter param) : base(param) { }

        protected override List<AlignmentSpotProperty> GetCleanedSpots(List<AlignmentSpotProperty> alignments) {
            var cSpots = new List<AlignmentSpotProperty>();
            var donelist = new HashSet<int>();

            foreach (var spot in alignments.Where(spot => spot.MspID >= 0 && spot.IsReferenceMatched)) {
                TryMergeToMaster(spot, cSpots, donelist, _param);
            }

            foreach (var spot in alignments.Where(spot => spot.TextDbID >= 0 && spot.IsReferenceMatched)) {
                TryMergeToMaster(spot, cSpots, donelist, _param);
            }

            foreach (var spot in alignments) {
                if (spot.IsReferenceMatched) continue;
                if (spot.PeakCharacter.IsotopeWeightNumber > 0) continue;
                TryMergeToMaster(spot, cSpots, donelist, _param);
            }

            return cSpots;
        }

        protected override void SetAlignmentID(List<AlignmentSpotProperty> alignments) {
            alignments.Sort((x, y) => x.MassCenter.CompareTo(y.MassCenter));

            for (int i = 0; i < alignments.Count; i++) {
                alignments[i].MasterAlignmentID = alignments[i].AlignmentID = i;
            }
        }

        protected override void SetLinks(List<AlignmentSpotProperty> alignments) {
            //checking alignment spot variable correlations
            var rtMargin = 0.06F;
            AssignLinksByIonAbundanceCorrelations(alignments, rtMargin);

            // assigning peak characters from the identified spots
            AssignLinksByIdentifiedIonFeatures(alignments);

            // assigning peak characters from the representative file information
            alignments.Sort((x, y) => x.HeightAverage.CompareTo(y.HeightAverage));
            alignments.Reverse();
            AssignLinksByRepresentativeIonFeatures(alignments);

            // assign putative group IDs
            alignments.Sort((x, y) => x.AlignmentID.CompareTo(y.AlignmentID));
            AssignPutativePeakgroupIDs(alignments);
        }

        private static void TryMergeToMaster(AlignmentSpotProperty spot, List<AlignmentSpotProperty> cSpots, HashSet<int> donelist, ParameterBase param) {
            if (donelist.Contains(spot.AlignmentID)) return;
            var spotRt = spot.TimesCenter.Value;
            var spotMz = spot.MassCenter;

            var rtTol = Math.Min(param.RetentionTimeAlignmentTolerance, 0.1);
            foreach (var cSpot in cSpots.Where(n => Math.Abs(n.MassCenter - spotMz) < param.Ms1AlignmentTolerance)) {
                var cSpotRt = cSpot.TimesCenter.Value;
                if (Math.Abs(cSpotRt - spotRt) < rtTol * 0.5) return;
            }
            cSpots.Add(spot);
            donelist.Add(spot.AlignmentID);
        }

        private static void AssignLinksByIonAbundanceCorrelations(List<AlignmentSpotProperty> alignSpots, float rtMargin) {
            if (alignSpots == null || alignSpots.Count == 0) return;
            if (alignSpots[0].AlignedPeakProperties == null || alignSpots[0].AlignedPeakProperties.Count == 0) return;

            if (alignSpots[0].AlignedPeakProperties.Count > 9) {
                alignSpots = alignSpots.OrderBy(n => n.TimesCenter.Value).ToList();
                foreach (var spot in alignSpots) {
                    if (spot.PeakCharacter.IsotopeWeightNumber > 0) continue;
                    var spotRt = spot.TimesCenter.Value;
                    var startScanIndex = SearchCollection.LowerBound(
                        alignSpots,
                        new AlignmentSpotProperty { TimesCenter = new ChromXs(spotRt - rtMargin - 0.01f) },
                        (a, b) => a.TimesCenter.Value.CompareTo(b.TimesCenter.Value)
                        );

                    var searchedSpots = new List<AlignmentSpotProperty>();

                    for (int i = startScanIndex; i < alignSpots.Count; i++) {
                        if (spot.AlignmentID == alignSpots[i].AlignmentID) continue;
                        if (alignSpots[i].TimesCenter.Value < spotRt - rtMargin) continue;
                        if (alignSpots[i].PeakCharacter.IsotopeWeightNumber > 0) continue;
                        if (alignSpots[i].TimesCenter.Value > spotRt + rtMargin) break;

                        searchedSpots.Add(alignSpots[i]);
                    }

                    AlignmentSpotVariableCorrelationSearcher(spot, searchedSpots);
                }
            }
        }

        private static void AlignmentSpotVariableCorrelationSearcher(AlignmentSpotProperty spot, List<AlignmentSpotProperty> searchedSpots)
        {
            var sampleCount = spot.AlignedPeakProperties.Count;
            var spotPeaks = spot.AlignedPeakProperties;
            var peaks = spotPeaks.Select(peak => peak.PeakHeightTop).ToArray();

            foreach (var searchSpot in searchedSpots) {

                var searchedSpotPeaks = searchSpot.AlignedPeakProperties;
                var correlation = BasicMathematics.Coefficient(peaks, searchedSpotPeaks.Select(peak => peak.PeakHeightTop).ToArray());
                if (correlation >= 0.95) {
                    spot.AlignmentSpotVariableCorrelations.Add(
                        new AlignmentSpotVariableCorrelation() {
                            CorrelateAlignmentID = searchSpot.AlignmentID,
                            CorrelationScore = (float)correlation
                        });
                    spot.PeakCharacter.IsLinked = true;
                    spot.PeakCharacter.PeakLinks.Add(new LinkedPeakFeature() {
                        LinkedPeakID = searchSpot.AlignmentID,
                        Character = PeakLinkFeatureEnum.CorrelSimilar
                    });
                }
            }

            if (spot.AlignmentSpotVariableCorrelations.Count > 1)
                spot.AlignmentSpotVariableCorrelations = spot.AlignmentSpotVariableCorrelations.OrderBy(n => n.CorrelateAlignmentID).ToList();
        }
    }
}
