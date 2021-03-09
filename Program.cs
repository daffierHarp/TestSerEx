using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using QN;
using static System.Console;
// ReSharper disable UnusedMember.Global

namespace TestSerEx
{
    public enum MyEn
    {
        None=0, Val1, Val2
    }
    public class Data
    {
        [XmlAttribute]public string SomeText { get; set; }
        [XmlAttribute]public int SomeInt { get; set; }
        public string SomeTextNode { get; set; }
        public int SomeIntNode { get; set; }
        public Byte[] Bytes;
        public List<Data> Children;
        public Data Parent;
        public DateTime Date = DateTime.Now;
        public MyEn En = MyEn.Val1;
        public bool B = true;
        public string[] StringArray = new string[3];
        public Data[] DataArray = new Data[3];
    }
    public class DataWithD
    {
        public Dictionary<string, int> D; // not supported by XML

    }

    public class CyclicalData
    {
        public CyclicalData Parent, Next;
        public List<CyclicalData> Children;
        public byte[] Buffer;
    }

    public class OwnerOfLineType
    {
        public LineTypeA[] Arr;
    }
    [XmlInclude(typeof(LineTypeFromA1)), XmlInclude(typeof(LineTypeFromA2))]
    public class LineTypeA
    {
        public string Str;
        // avoid crashes when future instances of class change
        // ReSharper disable UnusedMember.Global
        [XmlAnyAttribute,NonSerialized]
        public XmlAttribute[] AnyAttributes;

        [XmlAnyElement,NonSerialized]
        public XmlElement[] AnyElements;
        // ReSharper restore UnusedMember.Global
    }
    public class LineTypeFromA1 : LineTypeA
    {
        public int X;
    }
    public class LineTypeFromA2: LineTypeA
    {
        public float F;
    }
    
    class Program
    {
        static bool eq(DateTime d1, DateTime d2, bool withMilis = false, bool withSeconds = false)
        {
            if (!(d1.Year == d2.Year &&
                d1.Month == d2.Month &&
                d1.Day == d2.Day &&
                d1.Hour == d2.Hour &&
                d1.Minute == d2.Minute)) return false;
            if (!withSeconds) return true;
            if (d1.Second != d2.Second) return false;
            if (!withMilis) return true;
            return d1.Millisecond == d2.Millisecond;
        }
        // ReSharper disable once InconsistentNaming
        // ReSharper disable once UnusedParameter.Local
        static void Main(string[] args)
        {
            var data1 = new Data {
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
            //data1.Date = DateTime.ParseExact("","g",CultureInfo.InvariantCulture);

            // this will throw an exception when encoding as xml
            //data1.Children[0].Parent = i1;
            WriteLine("full xml:");
            WriteLine($"data1=\r\n{data1.ToXml()}\r\n");
            WriteLine("minimal xml, with schema namespace:");
            WriteLine($"data1={data1.ToXml(true,false)}");
            var clone1 = SerEx.FromXml<Data>(data1.ToXml(true, false));
            WriteLine($"clone1={clone1.ToXml(true, false)}");
            WriteLine($"data1.SomeTextNode==clone1.SomeTextNode?{data1.SomeTextNode==clone1.SomeTextNode}");

            WriteLine($"\r\nwithout namespace:\r\ndata1={data1.ToXml(true)}");
            var clone2 = SerEx.FromXml<Data>(data1.ToXml(true));
            WriteLine($"clone2={clone2.ToXml(true)}");
            WriteLine($"data1.SomeTextNode==clone2.SomeTextNode?{data1.SomeTextNode==clone2.SomeTextNode}");

            WriteLine("\r\nQN: (FAAC/MILO quick-server complex data notation)");
            data1.Children[0].Parent = data1; // test circular test on QN

            var data2 = new Data() {SomeTextNode = data1.SomeTextNode};
            var qn2 = data2.ToQn();
            var data2Clone = SerEx.FromQn<Data>(qn2);
            var qn1 = data1.ToQn();
            WriteLine($"data1={qn1}");
            WriteLine($"tabified qn:\r\n{qn1.Tabify(NotationConfig.Qn)}");
            var clone3 = SerEx.FromQn<Data>(data1.ToQn());
            WriteLine($"clone3={clone3.ToQn()}");
            WriteLine($"data1.SomeTextNode==clone3.SomeTextNode?{data1.SomeTextNode==clone3.SomeTextNode},data1.Date==clone3.Date?{eq(data1.Date,clone3.Date)}");

            // QN as JSON
            WriteLine("\r\nencode as QN with JSON config");
            var json = data1.ToNotation(NotationConfig.Json);
            WriteLine($"json={json}");
            WriteLine($"tabified json:\r\n{json.Tabify(NotationConfig.Json)}");
            var jsonClone = SerEx.FromText<Data>(json, NotationConfig.Json);
            WriteLine($"data1.SomeTextNode==jsonClone.SomeTextNode?{data1.SomeTextNode==jsonClone.SomeTextNode},data1.Date==jsonClone.Date?{eq(data1.Date,jsonClone.Date,withSeconds:true)}");

            // QN as C# Object Init
            WriteLine("\r\nencode as QN with \'C# Object Init\' config");
            var csoi = data1.ToNotation(NotationConfig.CSharpObjectInit);
            WriteLine($"csoi={csoi}");
            WriteLine("tabified C# object notation:");
            WriteLine(csoi.Tabify(NotationConfig.Json)); // tabify doesn't support c-sharp notation
            // cannot actually decode csoi

            // encode/decode dictionaries
            WriteLine("\r\nTesting dictionary encoding/decoding");
            var dd = new DataWithD { D = new Dictionary<string, int> { { "v", 2 }, { "item", 5} } };
            var ddQn = dd.ToQn(); WriteLine($"ddQn={ddQn}");
            var ddQnClone = SerEx.FromQn<DataWithD>(ddQn); 
            var ddJson = dd.ToNotation(NotationConfig.Json); WriteLine($"ddJson={ddJson}");
            var ddCs = dd.ToNotation(NotationConfig.CSharpObjectInit); WriteLine($"ddCs={ddCs}");
            var ddJsonClone = SerEx.FromText<DataWithD>(ddJson, NotationConfig.Json);

            //var ddCsClone = SerEx.FromQn<DataWithD>(ddCs); // not implemented
            var multiRecords = ddJson + "\r\n" + json + "\r\n";
            var r = new StringReader(multiRecords);
            var block1 = SerEx.ReadBlock(r, NotationConfig.Json);
            var block2 = SerEx.ReadBlock(r, NotationConfig.Json);
            var empty = SerEx.ReadBlock(r, NotationConfig.Json);
            

            // test unparsed decoding
            UnparsedItem jo = SerEx.UnparsedJson(json);
            Dictionary<string, UnparsedItem> joDic = jo.ParseClass();
            UnparsedItem[] joDicChildren = joDic["Children"].ParseArray();
            Data joChild = jo["Children"][0].Parse<Data>();
            var qo = SerEx.UnparsedQn(qn1);
            var qoDic = qo.ParseClass();

            // TODO: test decode of sample JSON from other sources
            const string sampleJson1 = @"
";

            // other:
            WriteLine($"SerEx.ToJson<string>(null)={SerEx.ToJson<string>(null)}");
            WriteLine($"new Data {{}}.ToJson()={new Data {}.ToJson()}");

            SerEx.VerifyNoCyclicalReference = false;
            var listOfData = new List<CyclicalData>();
            for (int i = 0; i < 50; i++) listOfData.Add( new CyclicalData {Buffer = getRandomBytes()});
            WriteLine("random data encoding to QN, no cyclical reference check:");
            WriteLine(listOfData.ToQn()/*.Tabify()*/);
            SerEx.VerifyNoCyclicalReference = true;
            SerEx.CyclicalVerificationParentOnly = false;
            listOfData[0].Children = listOfData.Skip(1).Take(49).ToList();
            listOfData[1].Parent = listOfData[0];
            listOfData[1].Next = listOfData[2];
            WriteLine(listOfData.ToQn());

            // test array of inherited types
            var inArr = new LineTypeA[] {
                new LineTypeA() {Str = "same type"},
                new LineTypeFromA1 {Str = "not a", X = 5},
                new LineTypeFromA2 {Str = "not b", F = 1.7f}
            };
            WriteLine("Going to encode array with inheritance lines");
            var qnInArr = inArr.ToQn();
            WriteLine("QN:\t\t" + qnInArr);
            var jsonInArr = inArr.ToJson();
            WriteLine("Json:\t\t" + jsonInArr);
            var inArr1 = SerEx.FromQn<LineTypeA[]>(qnInArr);
            var inArr2 = SerEx.FromJson<LineTypeA[]>(jsonInArr);
            WriteLine("clone over qn:\t\t"+inArr1.ToQn());
            WriteLine("clone over json:\t\t"+inArr2.ToJson());

            // test xsi:type issue work around
            var xmlInArr = inArr.ToXml(true, scanXsiDuplicates: false);
            var inArrFromXml = SerEx.FromXml<LineTypeA[]>(xmlInArr);
            try {
                var xmlInArr2 = inArrFromXml.ToXml(true, scanXsiDuplicates: false);
                var inArrFromXml2 = SerEx.FromXml<LineTypeA[]>(xmlInArr2);
            } catch { // expected to crash here
            }
            
            var xmlInArr3 = inArrFromXml.ToXml(true, scanXsiDuplicates: true); // expected to fix issue here
            var inArrFromXml3 = SerEx.FromXml<LineTypeA[]>(xmlInArr3);
        }

        static readonly Random _rnd = new Random();
        static byte[] getRandomBytes()
        {
            int l = _rnd.Next(40);
            if (l == 0) return null;
            var r = new byte[l];
            _rnd.NextBytes(r);
            return r;
        }

    }
}
