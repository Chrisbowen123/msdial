﻿using CompMs.Common.Algorithm.Scoring;
using CompMs.Common.DataObj.NodeEdge;
using CompMs.Common.Enum;
using CompMs.Common.Extension;
using CompMs.Common.Interfaces;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace CompMs.Common.Algorithm.Function {

    public class LinkNode {
        public double[] Score { get; set; }
        public IMSScanProperty Node { get; set; }
        public int Index { get; set; }
    }

    public sealed class MolecularNetworkingQuery {
        public MsmsSimilarityCalc MsmsSimilarityCalc { get; set; }
        public double MassTolerance { get; set; }
        public double AbsoluteAbundanceCutOff { get; set; }
        public double RelativeAbundanceCutOff { get; set; }
        public double SpectrumSimilarityCutOff { get; set; }
        public double MinimumPeakMatch { get; set; }
        public double MaxEdgeNumberPerNode { get; set; }
        public double MaxPrecursorDifference { get; set; }
        public double MaxPrecursorDifferenceAsPercent { get; set; }
    }

    public sealed class MoleculerNetworkingBase {
        public static void ExportNodesEdgesFiles(string folder, RootObject rootObj) {

            var nodes = rootObj.nodes;
            var edges = rootObj.edges;

            var dt = DateTime.Now;
            var nodepath = Path.Combine(folder, $"node-{dt:yyMMddhhmm}.txt");
            var edgepath = Path.Combine(folder, $"edge-{dt:yyMMddhhmm}.txt");
            var cypath = Path.Combine(folder, $"cyelements-{dt:yyMMddhhmm}.js");
        

            using (StreamWriter sw = new StreamWriter(nodepath, false, Encoding.ASCII)) {

                sw.WriteLine("ID\tMetaboliteName\tRt\tMz\tFormula\tOntology\tInChIKey\tSMILES\tSize\tBorderColor\tBackgroundColor\tMs2");
                foreach (var nodeObj in nodes) {
                    var node = nodeObj.data;
                    sw.Write(node.id + "\t" + node.Name + "\t" + node.Rt + "\t" + node.Mz + "\t" + node.Formula + "\t" + node.Ontology + "\t" +
                       node.InChiKey + "\t" + node.Smiles + "\t" + node.Size + "\t" + node.bordercolor + "\t" + node.backgroundcolor + "\t");

                    var ms2String = GetMsString(node.MSMS);
                    sw.WriteLine(ms2String);
                }
            }
            
            using (StreamWriter sw = new StreamWriter(edgepath, false, Encoding.ASCII)) {

                sw.WriteLine("SourceID\tTargetID\tScore\tType");
                foreach (var edgeObj in edges) {
                    var edge = edgeObj.data;
                    sw.WriteLine(edge.source + "\t" + edge.target + "\t" + edge.score + "\t" + edgeObj.classes);
                }
            }

            var rootCy = new RootObj4Cytoscape() { elements = rootObj };
            using (StreamWriter sw = new StreamWriter(cypath, false, Encoding.ASCII)) {
                var json = JsonConvert.SerializeObject(rootCy, Formatting.Indented);
                sw.WriteLine(json.ToString());
            }
        }

        public static void SendToCytoscapeJs(RootObject rootObj) {
            if (rootObj.nodes.IsEmptyOrNull() || rootObj.edges.IsEmptyOrNull()) return;
            var curDir = System.AppDomain.CurrentDomain.BaseDirectory;
            var cytoDir = Path.Combine(curDir, "CytoscapeLocalBrowser");
            var url = Path.Combine(cytoDir, "MsdialCytoscapeViewer.html");
            var cyjsexportpath = Path.Combine(Path.Combine(cytoDir, "data"), "elements.js");

            var counter = 0;
            var edges = new List<Edge>();
            var nodekeys = new List<int>();
            foreach (var edge in rootObj.edges.OrderByDescending(n => n.data.score)) {
                if (counter > 3000) break;
                edges.Add(edge);

                if (!nodekeys.Contains(edge.data.source))
                    nodekeys.Add(edge.data.source);
                if (!nodekeys.Contains(edge.data.target))
                    nodekeys.Add(edge.data.target);

                counter++;
            }

            var nodes = new List<Node>();
            foreach (var node in rootObj.nodes.Where(n => n.data.MsmsMin > 0)) {
                if (nodekeys.Contains(node.data.id))
                    nodes.Add(node);
            }
            var nRootObj = new RootObject() { nodes = nodes, edges = edges };

            var json = JsonConvert.SerializeObject(nRootObj, Formatting.Indented);
            using (StreamWriter sw = new StreamWriter(cyjsexportpath, false, Encoding.ASCII)) {
                sw.WriteLine("var dataElements =\r\n" + json.ToString() + "\r\n;");
            }
            System.Diagnostics.Process.Start(url);
        }

        private static string GetMsString(List<List<double>> msList) {
            if (msList == null || msList.Count == 0) {
                return string.Empty;
            }
            return string.Join(" ", msList.Select(ms => $"{Math.Round(ms[0], 5)}:{Math.Round(ms[1], 0)}"));
        }

        public RootObject GetMoleculerNetworkingRootObj<T>(IReadOnlyList<T> spots, IReadOnlyList<IMSScanProperty> scans, MolecularNetworkingQuery query, Action<double> report) where T : IMoleculeProperty, IChromatogramPeak {
            List<PeakScanPair<T>> peakScans = spots.Zip(scans, (spot, scan) => new PeakScanPair<T>(spot, scan)).ToList();
            var nodes = GetSimpleNodes(peakScans);
            var edges = GenerateEdgesBySpectralSimilarity(peakScans, query, report);
            return new RootObject() { nodes = nodes, edges = edges };
        }

        public static RootObject GetMoleculerNetworkingRootObj<T>(IReadOnlyList<T> spots, IReadOnlyList<IMSScanProperty> scans,
            MsmsSimilarityCalc msmsSimilarityCalc, double masstolerance, double absoluteAbsCutoff, double relativeAbsCutoff, double spectrumSimilarityCutoff,
            double minimumPeakMatch, double maxEdgeNumberPerNode, double maxPrecursorDifference, double maxPrecursorDifferenceAsPercent, Action<double> report) where T : IMoleculeProperty, IChromatogramPeak {
            var network = new MoleculerNetworkingBase();
            var query = new MolecularNetworkingQuery
            {
                MsmsSimilarityCalc = msmsSimilarityCalc,
                MassTolerance = masstolerance,
                AbsoluteAbundanceCutOff = absoluteAbsCutoff,
                RelativeAbundanceCutOff = relativeAbsCutoff,
                SpectrumSimilarityCutOff = spectrumSimilarityCutoff,
                MinimumPeakMatch = minimumPeakMatch,
                MaxEdgeNumberPerNode = maxEdgeNumberPerNode,
                MaxPrecursorDifference = maxPrecursorDifference,
                MaxPrecursorDifferenceAsPercent = maxPrecursorDifferenceAsPercent,
            };
            return network.GetMoleculerNetworkingRootObj(spots, scans, query, report);
        }

        public static RootObject GetMoleculerNetworkingRootObjForTargetSpot<T>(
            T targetSpot, IMSScanProperty targetScan, IReadOnlyList<T> spots, IReadOnlyList<IMSScanProperty> scans,
            MsmsSimilarityCalc msmsSimilarityCalc, double masstolerance, double absoluteAbsCutoff, double relativeAbsCutoff, double spectrumSimilarityCutoff,
            double minimumPeakMatch, double maxEdgeNumberPerNode, double maxPrecursorDifference, double maxPrecursorDifferenceAsPercent, Action<double> report) where T : IMoleculeProperty, IChromatogramPeak {

            var query = new MolecularNetworkingQuery
            {
                MsmsSimilarityCalc = msmsSimilarityCalc,
                MassTolerance = masstolerance,
                AbsoluteAbundanceCutOff = absoluteAbsCutoff,
                RelativeAbundanceCutOff = relativeAbsCutoff,
                SpectrumSimilarityCutOff = spectrumSimilarityCutoff,
                MinimumPeakMatch = minimumPeakMatch,
                MaxEdgeNumberPerNode = maxEdgeNumberPerNode,
                MaxPrecursorDifference = maxPrecursorDifference,
                MaxPrecursorDifferenceAsPercent = maxPrecursorDifferenceAsPercent,
            };
            List<PeakScanPair<T>> peakScans = spots.Zip(scans, (spot, scan) => new PeakScanPair<T>(spot, scan)).ToList();
            var edges = GenerateEdgesBySpectralSimilarity(new PeakScanPair<T>(targetSpot, targetScan), spots, scans, query, report);

            var idlist = new HashSet<int>(edges.SelectMany(edge => new[] { edge.data.source, edge.data.target }));
            var filteredPeakScans = peakScans.Where(pair => idlist.Contains(pair.Peak.ID)).ToList();
            var nodes = GetSimpleNodes(filteredPeakScans);
            return new RootObject() { nodes = nodes, edges = edges };
        }

        private static List<Node> GetSimpleNodes<T>(List<PeakScanPair<T>> peakScans) where T : IMoleculeProperty, IChromatogramPeak {
            if (peakScans.IsEmptyOrNull()) {
                return new List<Node>(0);
            }
            var minValue = Math.Log10(peakScans.Min(n => n.Peak.Intensity));
            var maxValue = Math.Log10(peakScans.Max(n => n.Peak.Intensity));
            return peakScans.Select(peakScan => GetSimpleNode(peakScan, minValue, maxValue)).ToList();
        }

        private static Node GetSimpleNode<T>(PeakScanPair<T> peakScanPair, double minValue, double maxValue) where T : IMoleculeProperty, IChromatogramPeak {
            var spot = peakScanPair.Peak;
            IMSScanProperty scan = peakScanPair.Scan;
            return new Node
            {
                data = new NodeData
                {
                    id = spot.ID,
                    Name = spot.Name,
                    Rt = spot.ChromXs.RT.Value.ToString(),
                    Mz = spot.Mass.ToString(),
                    Method = "MSMS",
                    Property = $"RT {Math.Round(spot.ChromXs.RT.Value, 3)}_m/z {Math.Round(spot.Mass, 5)}",
                    Formula = spot.Formula.FormulaString,
                    InChiKey = spot.InChIKey,
                    Ontology = spot.Ontology,
                    Smiles = spot.SMILES,
                    Size = (int)((Math.Log10(spot.Intensity) - minValue) / (maxValue - minValue) * 100 + 20),
                    bordercolor = "white",
                    backgroundcolor = GetOntologyColor(spot),
                    MSMS = scan.Spectrum.Select(spec => new List<double> { spec.Mass, spec.Intensity }).ToList(),
                    MsmsMin = scan.Spectrum.FirstOrDefault()?.Mass ?? 0d,
                    MsMsLabel = scan.Spectrum.Select(spec => Math.Round(spec.Mass, 5).ToString()).ToList(),
                },
            };
        }

        private static string GetOntologyColor<T>(T spot) where T : IMoleculeProperty, IChromatogramPeak {
            var isCharacterized = !spot.Name.IsEmptyOrNull() && !spot.Name.Contains("Unknown") && !spot.Name.Contains("w/o MS2") && !spot.Name.Contains("RIKEN");
            if (isCharacterized && MetaboliteColorCode.metabolite_colorcode.TryGetValue(spot.Ontology, out var backgroundcolor)) {
                return backgroundcolor;
            }
            return "rgb(0,0,0)";
        }

        private static List<Edge> GenerateEdgesBySpectralSimilarity<T>(List<PeakScanPair<T>> peakScans, MolecularNetworkingQuery query, Action<double> report) where T:IMoleculeProperty, IChromatogramPeak {
            foreach (var peakScan in peakScans) {
                var scan = peakScan.Scan;
                if (scan.Spectrum.Count > 0) {
                    scan.Spectrum = MsScanMatching.GetProcessedSpectrum(scan.Spectrum, scan.PrecursorMz, absoluteAbundanceCutOff: query.AbsoluteAbundanceCutOff, relativeAbundanceCutOff: query.RelativeAbundanceCutOff);
                }
            }
            var edges = GenerateEdges(peakScans, query, report);

            return edges.Select(edge => new Edge { data = edge, classes = "ms_similarity" }).ToList();
        }

        private static List<Edge> GenerateEdgesBySpectralSimilarity<T>(PeakScanPair<T> targetPeakScan, IReadOnlyList<T> spots, IReadOnlyList<IMSScanProperty> scans, MolecularNetworkingQuery query, Action<double> report) where T : IMoleculeProperty, IChromatogramPeak {
            var targetScan = targetPeakScan.Scan;
            var targetSpot = targetPeakScan.Peak;
            if (targetScan.Spectrum.IsEmptyOrNull()) {
                return new List<Edge>();
            }
            targetScan.Spectrum = MsScanMatching.GetProcessedSpectrum(targetScan.Spectrum, targetScan.PrecursorMz, absoluteAbundanceCutOff: query.AbsoluteAbundanceCutOff, relativeAbundanceCutOff: query.RelativeAbundanceCutOff);
            if (targetScan.Spectrum.IsEmptyOrNull()) {
                return new List<Edge>();
            }

            foreach (var scan in scans) {
                if (scan.Spectrum.Count > 0) {
                    scan.Spectrum = MsScanMatching.GetProcessedSpectrum(scan.Spectrum, scan.PrecursorMz, absoluteAbundanceCutOff: query.AbsoluteAbundanceCutOff, relativeAbundanceCutOff: query.RelativeAbundanceCutOff);
                }
            }

            var edges = GenerateEdges(targetPeakScan, spots, scans, query, report);
            return edges.Select(n => new Edge() { data = n, classes = "ms_similarity" }).ToList();
        }

        private static List<EdgeData> GenerateEdges<T>(PeakScanPair<T> targetPeakScan, IReadOnlyList<T> spots, IReadOnlyList<IMSScanProperty> peaks, MolecularNetworkingQuery query, Action<double> report) where T : IMoleculeProperty, IChromatogramPeak {
            var edges = new List<EdgeData>();
            var counter = 0;
            var max = peaks.Count;
            var links = new List<LinkNode>();
            Debug.WriteLine(peaks.Count);
            var targetScan = targetPeakScan.Scan;
            for (int i = 0; i < peaks.Count; i++) {
                if (peaks[i].Spectrum.Count <= 0) continue;
                if (peaks[i].ScanID == targetScan.ScanID) continue;
                counter++;
                Debug.WriteLine("{0} / {1}", counter, max);
                report?.Invoke(counter / (double)max);
                double[] scoreitem = GetLinkNode(targetScan, peaks[i], query);
                if (scoreitem == null) continue;
                links.Add(new LinkNode() { Score = scoreitem, Node = peaks[i], Index = i });
            }

            foreach (var link in links) {
                var source_node_id = targetPeakScan.Peak.ID;
                var target_node_id = spots[link.Index].ID;

                var edge = new EdgeData() {
                    score = link.Score[0], matchpeakcount = link.Score[1], source = source_node_id, target = target_node_id
                };
                edges.Add(edge);
            }
            return edges;
        }

        private static List<EdgeData> GenerateEdges<T>(List<PeakScanPair<T>> peakScans, MolecularNetworkingQuery query, Action<double> report) where T : IMoleculeProperty, IChromatogramPeak {
            var edges = new List<EdgeData>();
            var counter = 0;
            var max = peakScans.Count;
            var node2links = new Dictionary<int, List<LinkNode>>();
            Debug.WriteLine(peakScans.Count);
            for (int i = 0; i < peakScans.Count; i++) {
                if (peakScans[i].Scan.Spectrum.Count <= 0) continue;
                counter++;
                Debug.WriteLine("{0} / {1}", counter, max);
                report?.Invoke(counter / (double)max);

                node2links[i] = new List<LinkNode>();
                for (int j = i + 1; j < peakScans.Count; j++) {
                    if (peakScans[j].Scan.Spectrum.Count <= 0) continue;
                    double[] scoreitem = GetLinkNode(peakScans[i].Scan, peakScans[j].Scan, query);
                    if (scoreitem == null) continue;
                    node2links[i].Add(new LinkNode() { Score = scoreitem, Node = peakScans[j].Scan, Index = j });
                }
            }

            var cNode2Links = new Dictionary<int, List<LinkNode>>();
            foreach (var item in node2links) {
                if (item.Value.IsEmptyOrNull()) continue;
                var nitem = item.Value.OrderByDescending(n => n.Score[0]).ToList();
                cNode2Links[item.Key] = new List<LinkNode>();
                for (int i = 0; i < nitem.Count; i++) {
                    if (i > query.MaxEdgeNumberPerNode - 1) break;
                    cNode2Links[item.Key].Add(nitem[i]);
                }
            }

            foreach (var item in cNode2Links) {
                foreach (var link in item.Value) {
                    var source_node_id = peakScans[item.Key].Peak.ID;
                    var target_node_id = peakScans[link.Index].Peak.ID;

                    var edge = new EdgeData() {
                        score = link.Score[0], matchpeakcount = link.Score[1], source = source_node_id, target = target_node_id
                    };
                    edges.Add(edge);
                }
            }
            return edges;
        }

        private static double[] GetLinkNode(IMSScanProperty prop1, IMSScanProperty prop2, MolecularNetworkingQuery query) {
            var massDiff = Math.Abs(prop1.PrecursorMz - prop2.PrecursorMz);
            if (massDiff > query.MaxPrecursorDifference) return null;
            // if (Math.Max(prop1.PrecursorMz, prop2.PrecursorMz) * maxPrecursorDiff_Percent * 0.01 - Math.Min(prop1.PrecursorMz, prop2.PrecursorMz) < 0) continue;
            double[] scoreitem = new double[2];
            switch (query.MsmsSimilarityCalc) {
                case MsmsSimilarityCalc.Bonanza:
                    scoreitem = MsScanMatching.GetBonanzaScore(prop1, prop2, query.MassTolerance);
                    break;
                case MsmsSimilarityCalc.ModDot:
                    scoreitem = MsScanMatching.GetModifiedDotProductScore(prop1, prop2, query.MassTolerance);
                    break;
            }
            if (scoreitem[1] < query.MinimumPeakMatch) return null; 
            if (scoreitem[0] < query.SpectrumSimilarityCutOff * 0.01) return null;

            return scoreitem;
        }

        public static List<EdgeData> GenerateEdges(
           IReadOnlyList<IMoleculeMsProperty> peaks1,
           IReadOnlyList<IMoleculeMsProperty> peaks2,
           double massTolerance,
           double minimumPeakMatch,
           double matchThreshold,
           double maxEdgeNumPerNode,
           double maxPrecursorDiff,
           double maxPrecursorDiff_Percent,
           bool isBonanza,
           Action<double> report) {

            var edges = new List<EdgeData>();
            var counter = 0;
            var max = peaks1.Count;
            var node2links = new Dictionary<int, List<LinkNode>>();
            Console.WriteLine("Query1 {0}, Query2 {1}, Total {2}", peaks1.Count, peaks2.Count, peaks1.Count * peaks2.Count);
            for (int i = 0; i < peaks1.Count; i++) {
                if (peaks1[i].Spectrum.Count <= 0) continue;
                counter++;
                report?.Invoke(counter / (double)max);
                if (counter % 100 == 0) {
                    Console.Write("{0} / {1}", counter, max);
                    Console.SetCursorPosition(0, Console.CursorTop);
                }

                for (int j = 0; j < peaks2.Count; j++) {
                    if (peaks2[j].Spectrum.Count <= 0) continue;
                    var prop1 = peaks1[i];
                    var prop2 = peaks2[j];
                    var massDiff = Math.Abs(prop1.PrecursorMz - prop2.PrecursorMz);
                    if (massDiff > maxPrecursorDiff) continue;
                    double[] scoreitem = new double[2];
                    if (isBonanza) {
                        scoreitem = MsScanMatching.GetBonanzaScore(prop1, prop2, massTolerance);
                    }
                    else {
                        scoreitem = MsScanMatching.GetModifiedDotProductScore(prop1, prop2, massTolerance);
                    }
                    if (scoreitem[1] < minimumPeakMatch) continue;
                    if (scoreitem[0] < matchThreshold * 0.01) continue;

                    if (node2links.ContainsKey(i)) {
                        node2links[i].Add(new LinkNode() { Score = scoreitem, Node = peaks2[j], Index = j });
                    }
                    else {
                        node2links[i] = new List<LinkNode>() { new LinkNode() { Score = scoreitem, Node = peaks2[j], Index = j } };
                    }
                }
            }

            var cNode2Links = new Dictionary<int, List<LinkNode>>();
            foreach (var item in node2links) {
                var nitem = item.Value.OrderByDescending(n => n.Score[0]).ToList();
                cNode2Links[item.Key] = new List<LinkNode>();
                for (int i = 0; i < nitem.Count; i++) {
                    if (i > maxEdgeNumPerNode - 1) break;
                    cNode2Links[item.Key].Add(nitem[i]);
                }
            }

            foreach (var item in cNode2Links) {
                foreach (var link in item.Value) {
                    var source_node_id = peaks1[item.Key].ScanID;
                    var target_node_id = peaks2[link.Index].ScanID;

                    var edge = new EdgeData() {
                        score = link.Score[0], matchpeakcount = link.Score[1], source = source_node_id, target = target_node_id
                    };
                    edges.Add(edge);
                }
            }
            return edges;
        }

        class PeakScanPair<T> {
            public PeakScanPair(T peak, IMSScanProperty scan) {
                Peak = peak;
                Scan = scan;
            }

            public T Peak { get; }
            public IMSScanProperty Scan { get; }
        }
    }
}
