using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using static System.Console;

namespace testXml
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
            // this will throw an exception when encoding as xml
            //i1.Children[0].Parent = i1;
            WriteLine("full xml:");
            WriteLine($"i1=\r\n{data1.ToXml()}\r\n");
            WriteLine("minimal xml, with schema namespace:");
            WriteLine($"i1={data1.ToXml(true,false)}");
            var clone1 = SerEx.FromXml<Data>(data1.ToXml(true, false));
            WriteLine($"clone1={clone1.ToXml(true, false)}");
            WriteLine($"i1.SomeTextNode==clone1.SomeTextNode?{data1.SomeTextNode==clone1.SomeTextNode}");

            WriteLine($"\r\nwithout namespace:\r\ni1={data1.ToXml(true)}");
            var clone2 = SerEx.FromXml<Data>(data1.ToXml(true));
            WriteLine($"clone2={clone2.ToXml(true)}");
            WriteLine($"i1.SomeTextNode==clone2.SomeTextNode?{data1.SomeTextNode==clone2.SomeTextNode}");

            WriteLine("\r\nQN: (FAAC/MILO quick-server complex data notation)");
            data1.Children[0].Parent = data1; // test circular test on QN
            WriteLine($"i1={data1.ToQn()}");
            var clone3 = SerEx.FromQn<Data>(data1.ToQn());
            WriteLine($"clone3={clone3.ToQn()}");
            WriteLine($"i1.SomeTextNode==clone3.SomeTextNode?{data1.SomeTextNode==clone3.SomeTextNode}");
        }
    }
}
