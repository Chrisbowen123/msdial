﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace CompMs.MsdialCore.DataObj.Tests
{
    [TestClass()]
    public class AnnotationQualitySearchQueryTests
    {
        [DataTestMethod()]
        [DynamicData(nameof(IsMatchedTestData), DynamicDataSourceType.Property)]
        public void IsMatchedTest(List<AnnotationQualityType> types, AnnotationQualitySearchQuery query, bool expected) {
            var actual = query.IsMatched(types);
            Assert.AreEqual(expected, actual);
        }

        public static IEnumerable<object[]> IsMatchedTestData {
            get {
                List<AnnotationQualityType> empty = new List<AnnotationQualityType>(),
                    lowQuality = new List<AnnotationQualityType> { AnnotationQualityType.LOW_QUALITY_SPECTRUM, },
                    misannotation = new List<AnnotationQualityType> { AnnotationQualityType.MISANNOTATION, },
                    both = new List<AnnotationQualityType> { AnnotationQualityType.LOW_QUALITY_SPECTRUM, AnnotationQualityType.MISANNOTATION, };
                AnnotationQualitySearchQuery orSingle = AnnotationQualitySearchQuery.Any(AnnotationQualityType.LOW_QUALITY_SPECTRUM),
                    andSingle = AnnotationQualitySearchQuery.All(AnnotationQualityType.LOW_QUALITY_SPECTRUM),
                    or = AnnotationQualitySearchQuery.Any(AnnotationQualityType.LOW_QUALITY_SPECTRUM, AnnotationQualityType.MISANNOTATION),
                    and = AnnotationQualitySearchQuery.All(AnnotationQualityType.LOW_QUALITY_SPECTRUM, AnnotationQualityType.MISANNOTATION),
                    orEmpty = AnnotationQualitySearchQuery.Any(),
                    andEmpty = AnnotationQualitySearchQuery.All(),
                    none = AnnotationQualitySearchQuery.None(AnnotationQualityType.LOW_QUALITY_SPECTRUM, AnnotationQualityType.MISANNOTATION),
                    notall = AnnotationQualitySearchQuery.NotAll(AnnotationQualityType.LOW_QUALITY_SPECTRUM, AnnotationQualityType.MISANNOTATION);

                yield return new object[] { empty, orSingle, false, };
                yield return new object[] { empty, andSingle, false, };
                yield return new object[] { empty, or, false, };
                yield return new object[] { empty, and, false, };
                yield return new object[] { empty, orEmpty, true, };
                yield return new object[] { empty, andEmpty, true, };
                yield return new object[] { empty, none, true, };
                yield return new object[] { empty, notall, true, };
                yield return new object[] { lowQuality, orSingle, true, };
                yield return new object[] { lowQuality, andSingle, true, };
                yield return new object[] { lowQuality, or, true, };
                yield return new object[] { lowQuality, and, false, };
                yield return new object[] { lowQuality, orEmpty, true, };
                yield return new object[] { lowQuality, andEmpty, true, };
                yield return new object[] { lowQuality, none, false, };
                yield return new object[] { lowQuality, notall, true, };
                yield return new object[] { misannotation, orSingle, false, };
                yield return new object[] { misannotation, andSingle, false, };
                yield return new object[] { misannotation, or, true, };
                yield return new object[] { misannotation, and, false, };
                yield return new object[] { misannotation, orEmpty, true, };
                yield return new object[] { misannotation, andEmpty, true, };
                yield return new object[] { misannotation, none, false, };
                yield return new object[] { misannotation, notall, true, };
                yield return new object[] { both, orSingle, true, };
                yield return new object[] { both, andSingle, true, };
                yield return new object[] { both, or, true, };
                yield return new object[] { both, and, true, };
                yield return new object[] { both, orEmpty, true, };
                yield return new object[] { both, andEmpty, true, };
                yield return new object[] { both, none, false, };
                yield return new object[] { both, notall, false, };
            }
        }
    }
}