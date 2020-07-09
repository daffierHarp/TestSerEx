using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Serialization;
using Helpers;
using static System.Console;

namespace TestSerEx
{
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
    }

    class Program
    {
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
                }
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

            WriteLine($"data1={data1.ToQn()}");
            var clone3 = SerEx.FromQn<Data>(data1.ToQn());
            WriteLine($"clone3={clone3.ToQn()}");
            WriteLine($"data1.SomeTextNode==clone3.SomeTextNode?{data1.SomeTextNode==clone3.SomeTextNode}");

            // QN as JSON
            WriteLine("encode as QN with JSON config");
            var json = data1.ToQn(QnConfig.Json);
            WriteLine($"json={json}");
            var jsonClone = SerEx.FromQn<Data>(json, QnConfig.Json);
            WriteLine($"data1.SomeTextNode==jsonClone.SomeTextNode?{data1.SomeTextNode==jsonClone.SomeTextNode}");

            // QN as C# Object Init
            WriteLine("\r\nencode as QN with \'C# Object Init\' config");
            var csoi = data1.ToQn(QnConfig.CSharpObjectInit);
            WriteLine($"csoi={csoi}");
            // cannot actually decode csoi

            // TODO: test decode of sample JSON from other sources
            const string sampleJson1 = @"
";
        }
    }
}
