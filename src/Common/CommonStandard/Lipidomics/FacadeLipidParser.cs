﻿using System.Collections.Generic;

namespace CompMs.Common.Lipidomics
{
    public class FacadeLipidParser : ILipidParser
    {
        private readonly Dictionary<string, List<ILipidParser>> map = new Dictionary<string, List<ILipidParser>>();

        public string Target { get; } = string.Empty;

        public ILipid Parse(string lipidStr) {
            var key = lipidStr.Split()[0];
            if (map.TryGetValue(key, out var parsers)) {
                foreach (var parser in parsers) {
                    if (parser.Parse(lipidStr) is ILipid lipid) {
                        return lipid;
                    }
                }
            }
            return null;
        }

        public void Add(ILipidParser parser) {
            if (!map.ContainsKey(parser.Target)) {
                map.Add(parser.Target, new List<ILipidParser>());
            }
            map[parser.Target].Add(parser);
        }

        public void Remove(ILipidParser parser) {
            if (map.ContainsKey(parser.Target)) {
                map[parser.Target].Remove(parser);
            }
        }

        public static ILipidParser Default {
            get {
                if (@default is null) {
                    var parser = new FacadeLipidParser();
                    new List<ILipidParser>{
                        new BMPLipidParser(),
                        new CLLipidParser(),
                        new DGLipidParser(),
                        new HBMPLipidParser(),
                        new LPCLipidParser(),
                        new LPELipidParser(),
                        new LPGLipidParser(),
                        new LPILipidParser(),
                        new LPSLipidParser(),
                        new MGLipidParser(),
                        new PALipidParser(),
                        new PCLipidParser(),
                        new PELipidParser(),
                        new PGLipidParser(),
                        new PILipidParser(),
                        new PSLipidParser(),
                        new TGLipidParser(),
                        new DGLipidParser(),
                        new EtherPCLipidParser(),
                        new EtherPELipidParser(),
                        new EtherLPCLipidParser(),
                        new EtherLPELipidParser(),
                        new SMLipidParser(),
                        new CeramideLipidParser(),
                        new HexCerLipidParser(),
                        new DGTALipidParser(),
                        new DGTSLipidParser(),
                        new LDGTALipidParser(),
                        new LDGTSLipidParser(),
                        new GM3LipidParser(),
                        new SHexCerLipidParser(),
                        new CARLipidParser(),
                        new CLLipidParser(),
                        new DMEDFAHFALipidParser(),
                        new DMEDFALipidParser(),
                        new PCd5LipidParser(),
                        new PEd5LipidParser(),
                        new PId5LipidParser(),
                        new PGd5LipidParser(),
                        new PSd5LipidParser(),
                        new LPCd5LipidParser(),
                        new LPEd5LipidParser(),
                        new LPId5LipidParser(),
                        new LPGd5LipidParser(),
                        new LPSd5LipidParser(),
                        new CeramideD7LipidParser(),
                        new SMd9LipidParser(),
                        new DGd5LipidParser(),
                        new TGd5LipidParser(),

                    }.ForEach(parser.Add);
                    @default = parser;
                }
                return @default;
            }
        }
        private static ILipidParser @default;
    }
}
