using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Logic.Mapek;

namespace SmartNode.TestProject
{
    public class MapekPlanTestsTest
    {

        private void AssertCombinationsEqual(IEnumerable<IEnumerable<object>> expected, IEnumerable<IEnumerable<object>> actual)
        {
            var exp = expected.Select(e => string.Join("||", e.Select(x => x?.ToString() ?? "null"))).OrderBy(s => s).ToList();
            var act = actual.Select(e => string.Join("||", e.Select(x => x?.ToString() ?? "null"))).OrderBy(s => s).ToList();
            Assert.Equal(exp, act);
        }

        [Fact]
        public void TwoSequences_Ints_ReturnsAllPairs()
        {
            var inputs = new List<IEnumerable<object>>
            {
                new object[] { 3, 4 },
                new object[] { 1, 2 }
            };

            var actual = MapekPlan.OldGetNaryCartesianProducts(inputs);

            var expected = new List<IEnumerable<object>>
            {
                new object[] { 1, 3 },
                new object[] { 1, 4 },
                new object[] { 2, 3 },
                new object[] { 2, 4 }
            };
            var expected2 = new List<IEnumerable<object>>
            {
                new object[] { 3, 1 },
                new object[] { 4, 1 },
                new object[] { 3, 2 },
                new object[] { 4, 2 }
            };


            AssertCombinationsEqual(expected, actual);
            // TODO: reduce visibility again
            AssertCombinationsEqual(expected2, MapekPlan.GetNaryCartesianProducts(inputs));
        }

        [Fact]
        public void SingleSequence_Strings_ReturnsSingleElementTuples()
        {
            var inputs = new List<IEnumerable<object>>
            {
                new object[] { "a", "b", "c" }
            };

            var actual = MapekPlan.OldGetNaryCartesianProducts(inputs);

            var expected = new List<IEnumerable<object>>
            {
                new object[] { "a" },
                new object[] { "b" },
                new object[] { "c" }
            };

            AssertCombinationsEqual(expected, actual);
            AssertCombinationsEqual(expected, MapekPlan.GetNaryCartesianProducts(inputs));
        }

        [Fact]
        public void OneInnerEmptySequence_ReturnsNoCombinations()
        {
            var inputs = new List<IEnumerable<object>>
            {
                new object[] { 1, 2 },
                new object[] { },            // empty inner sequence should make result empty
                new object[] { 5 }
            };

            var actual = MapekPlan.OldGetNaryCartesianProducts(inputs);

            Assert.True(actual.Count == 0);
            Assert.True(MapekPlan.GetNaryCartesianProducts(inputs).Count() == 0);
        }

        [Fact]
        public void VaryingLengths_ReturnsCorrectCountAndContents()
        {
            var inputs = new List<IEnumerable<object>>
            {
                new object[] { true, false },
                new object[] { 10, 20, 30 },
                new object[] { "x" }
            };

            var actual = MapekPlan.OldGetNaryCartesianProducts(inputs);

            // Expect 1 * 3 * 2 = 6 combinations
            Assert.Equal(6, actual.Count);

            var expected = new List<IEnumerable<object>>
            {
                new object[] { "x", 10, true },
                new object[] { "x", 10, false },
                new object[] { "x", 20, true },
                new object[] { "x", 20, false },
                new object[] { "x", 30, true },
                new object[] { "x", 30, false }
            };
            var expected2 = new List<IEnumerable<object>>
            {
                new object[] { true, 10, "x" },
                new object[] { false, 10, "x" },
                new object[] { true, 20, "x" },
                new object[] { false, 20, "x" },
                new object[] { true, 30, "x" },
                new object[] { false, 30, "x"}
            };


            AssertCombinationsEqual(expected, actual);
            AssertCombinationsEqual(expected2, MapekPlan.GetNaryCartesianProducts(inputs));
        }
    }
}