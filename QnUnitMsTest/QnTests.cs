using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using QN;
// ReSharper disable UnusedMember.Global

namespace QnUnitMsTest
{


    [TestClass]
    public class QnTests
    {
        readonly Data _data1;
        // ReSharper disable once NotAccessedField.Local
        readonly DataWithD _dd;

        public QnTests()
        {
            _data1 = new Data {
                SomeText = "xyz\r\nnext line", 
                SomeInt = 4, 
                SomeTextNode = "node value & with special characters \r\n>>\t great!\"yay!\"", 
                SomeIntNode = 5,
                Bytes = new byte[]{1,2,3,4,5},
                Children = new List<Data> {
                    new Data { SomeText = "I'm a child"},
                    new Data { SomeText = "I'm a child"},
                },
            };
            _dd = new DataWithD { D = new Dictionary<string, int> { { "v", 2 }, { "item", 5} } };
        }

        static void runEqTest(object a, object b, Type t)
        {
            foreach (var fi in t.GetFields()) {
                var originalV = fi.GetValue(a);
                var cloneV = fi.GetValue(b);
                bool areEq = originalV.EqVals(cloneV, nullEqEmptyLists: true, matchLineEnds: true);
                if (!areEq && Debugger.IsAttached) Debugger.Break();
                Assert.IsTrue(areEq, $"{fi.Name} values are not equal");
                if (originalV is string origStr && cloneV is string cloneStr && origStr!=cloneStr)
                    Debug.WriteLine($"{fi.Name} values do not really match\r\n{origStr.Escape()}\r\n!=\r\n{cloneStr.Escape()}");
            }
            foreach (var pi in t.GetProperties()) {
                var originalV = pi.GetValue(a);
                var cloneV = pi.GetValue(b);
                var areEq = originalV.EqVals(cloneV, nullEqEmptyLists: true, matchLineEnds: true);
                if (!areEq && Debugger.IsAttached) Debugger.Break();
                Assert.IsTrue(areEq, $"{pi.Name} values are equal");
                if (originalV is string origStr && cloneV is string cloneStr && origStr!=cloneStr)
                    Debug.WriteLine($"{pi.Name} values do not really match\r\n{origStr.Escape()}\r\n!=\r\n{cloneStr.Escape()}");
            }
            Assert.IsTrue(a.EqVals(b, nullEqEmptyLists:true, matchLineEnds:true), "some of clone values are not equal");
        }
        [TestMethod]
        public void TestFullXml()
        {
            var fullXml = _data1.ToFullXml();
            Assert.IsInstanceOfType(fullXml, typeof(string));
            Assert.IsNotNull(fullXml);
            Debug.WriteLine(fullXml);
            var cloneOverFullXml = SerEx.FromXml<Data>(fullXml);
            Assert.IsNotNull(cloneOverFullXml);
            runEqTest(_data1, cloneOverFullXml, typeof(Data));
        }

        [TestMethod]
        public void TestMinXml()
        {
            var minXml = _data1.ToXml(true);
            Assert.IsInstanceOfType(minXml, typeof(string));
            Assert.IsNotNull(minXml);
            Debug.WriteLine(minXml);
            var cloneOverMinXml = SerEx.FromXml<Data>(minXml);
            Assert.IsNotNull(cloneOverMinXml);
            runEqTest(_data1, cloneOverMinXml, typeof(Data));
        }

        [TestMethod]
        public void TestMinXmlWithNamespace()
        {
            var minXml = _data1.ToXml(true, false);
            Assert.IsInstanceOfType(minXml, typeof(string));
            Assert.IsNotNull(minXml);
            Debug.WriteLine(minXml);
            var cloneOverMinXml = SerEx.FromXml<Data>(minXml);
            Assert.IsNotNull(cloneOverMinXml);
            runEqTest(_data1, cloneOverMinXml, typeof(Data));
        }

        [TestMethod]
        public void TestQn()
        {
            var qn = _data1.ToQn();
            Assert.IsInstanceOfType(qn, typeof(string));
            Assert.IsNotNull(qn);
            Debug.WriteLine(qn);
            var cloneOverQn = SerEx.FromQn<Data>(qn);
            Assert.IsNotNull(cloneOverQn);
            runEqTest(_data1, cloneOverQn, typeof(Data));
        }

        [TestMethod]
        public void TestJson()
        {
            var json = _data1.ToJson();
            Assert.IsInstanceOfType(json, typeof(string));
            Assert.IsNotNull(json);
            Debug.WriteLine(json);
            var cloneOverJson = SerEx.FromJson<Data>(json);
            Assert.IsNotNull(cloneOverJson);
            runEqTest(_data1, cloneOverJson, typeof(Data));
        }

        [TestMethod]
        public void TestQnDictionary()
        {
            var ddQn = _dd.ToQn(); 
            Assert.IsInstanceOfType(ddQn, typeof(string));
            Assert.IsNotNull(ddQn);
            Debug.WriteLine($"ddQn={ddQn}");
            var ddQnClone = SerEx.FromQn<DataWithD>(ddQn);
            Assert.IsNotNull(ddQnClone);
            runEqTest(_dd, ddQnClone, typeof(DataWithD));
        }

        [TestMethod]
        public void TestJsonDictionary()
        {
            var ddJson = _dd.ToJson(); 
            Assert.IsInstanceOfType(ddJson, typeof(string));
            Assert.IsNotNull(ddJson);
            Debug.WriteLine($"ddJson={ddJson}");
            var ddJsonClone = SerEx.FromJson<DataWithD>(ddJson);
            Assert.IsNotNull(ddJsonClone);
            runEqTest(_dd, ddJsonClone, typeof(DataWithD));
        }
    }
}
