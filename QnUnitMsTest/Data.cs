//  Data.cs Latest resharper cleanup: 07/10/2020
//  Copyright © FAAC. All rights reserved.

using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace QnUnitMsTest
{
    public class Data
    {
        [XmlAttribute]public string SomeText { get; set; }
        [XmlAttribute]public int SomeInt { get; set; }
        public string SomeTextNode { get; set; }
        public int SomeIntNode { get; set; }
        public byte[] Bytes;
        public List<Data> Children;
        public Data Parent;
        public DateTime Date = DateTime.Now;
        public MyEn En = MyEn.Val1;
        public bool B = true;
        public string[] StringArray = new string[3];
        public Data[] DataArray = new Data[3];
    }
    public enum MyEn
    {
        // ReSharper disable UnusedMember.Global
        None=0, Val1, Val2
        // ReSharper restore UnusedMember.Global
    }

    public class DataWithD
    {
        public Dictionary<string, int> D; // not supported by XML

    }
}