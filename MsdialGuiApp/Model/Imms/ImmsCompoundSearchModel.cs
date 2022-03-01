﻿using CompMs.App.Msdial.Model.Search;
using CompMs.Common.Components;
using CompMs.Common.DataObj.Property;
using CompMs.Common.DataObj.Result;
using CompMs.Common.Interfaces;
using CompMs.MsdialCore.Algorithm.Annotation;
using CompMs.MsdialCore.DataObj;
using CompMs.MsdialCore.MSDec;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CompMs.App.Msdial.Model.Imms
{
    public class ImmsCompoundSearchModel<T> : CompoundSearchModel<T> where T : IMSIonProperty, IMoleculeProperty
    {
        public ImmsCompoundSearchModel(
            IFileBean fileBean,
            T property,
            MSDecResult msdecResult,
            IReadOnlyList<CompoundSearcher> compoundSearchers)
            :base(
                 fileBean,
                 property,
                 msdecResult,
                 compoundSearchers) {

        }

        [Obsolete]
        public ImmsCompoundSearchModel(
            IFileBean fileBean,
            T property,
            MSDecResult msdecResult,
            IReadOnlyList<IsotopicPeak> isotopes,
            IReadOnlyList<IAnnotatorContainer<IAnnotationQuery, MoleculeMsReference, MsScanMatchResult>> annotatorContaienrs)
            :base(
                 fileBean,
                 property,
                 msdecResult,
                 isotopes,
                 annotatorContaienrs) {

        }

        public override CompoundResultCollection Search() {
            return new ImmsCompoundResultCollection
            {
                Results = SearchCore().Select(c => new ImmsCompoundResult(c)).ToList<ICompoundResult>(),
            };
        }
    }

    public class ImmsCompoundResult : CompoundResult
    {
        public ImmsCompoundResult(MoleculeMsReference msReference, MsScanMatchResult matchResult)
            : base(msReference, matchResult) {

        }

        public ImmsCompoundResult(ICompoundResult compound)
            : this(compound.MsReference, compound.MatchResult) {

        }

        public double CollisionCrossSection => msReference.CollisionCrossSection;
        public double CcsSimilarity => matchResult.CcsSimilarity;
    }

    public class ImmsCompoundResultCollection : CompoundResultCollection
    {

    }
}
