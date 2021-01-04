# XML, QN and JSON Serialization

Serialization helpers through extension methods and introducing QN.

QN stands for Quick-server data objects Notation.

This form of serialization and de-serialization is used by FAAC/MILO in certain communication
protocols and provides notation that is similar to JSON, yet entirely built in C#. The Serialization-Extension class called SerEx implements
all of the required code.

Encoding and decoding of QN format was later generalized to allow configuration as JSON. This results in a stand-alone, single code file to encode/decode JSON. 

The XML serialization provided is simply a shortcut to .Net XML Serialization foundational classes. It is easier though to simply call extension methods.

As a first example, to serialize POCO classes to XML as a formatted document:

```csharp
var xml = data.ToXml();
```

This results as:

```xml
<?xml version="1.0" encoding="utf-16"?>
<Data xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema" SomeText="xyz&#xD;&#xA;next line" SomeInt="4">
    <Bytes>AQIDBAU=</Bytes>
    <Children>
    <Data SomeText="I'm a child" SomeInt="0">
        <SomeIntNode>0</SomeIntNode>
    </Data>
    <Data SomeText="I'm a child" SomeInt="0">
        <SomeIntNode>0</SomeIntNode>
    </Data>
    </Children>
    <SomeIntNode>5</SomeIntNode>
</Data>
```

To serialize a minimal XML:

```csharp
var minXml = data.ToXml(true);
```

This results as:

```xml
<Data SomeText="xyz&#xD;&#xA;next line" SomeInt="4"><Bytes>AQIDBAU=</Bytes><Children><Data SomeText="I'm a child" SomeInt="0"><SomeIntNode>0</SomeIntNode></Data><Data SomeText="I'm a child" SomeInt="0"><SomeIntNode>0</SomeIntNode></Data></Children><SomeIntNode>5</SomeIntNode></Data>
```

To de-serialize the data:

```csharp
var data2 = SerEx.FromXml<Data>(xml);
```

To serialize as QN:

```csharp
var qn = data.ToQn();
```

This results as:

```text
(Children:[(SomeText:"I'm a child"),(SomeText:"I'm a child")],SomeText:"xyz",SomeInt:4)
```

And de-serialize in same fashion:

```csharp
var data3 = SerEx.FromQn<Data>(qn);
```

And in the same fashion, serialize and de-serialize of JSON :
```csharp
var json = data.ToJson();
var cloneOverJson = SerEx.FromJson<Data>(json);
```

And the JSON data looks as such:
```json
{"Bytes":"AQIDBAU=","Children":[{"Parent":null,"Date":"2020-07-12T03:48:36.976Z","En":"Val1","SomeText":"I'm a child"},{"Date":"2020-07-12T03:48:36.976Z","En":"Val1","SomeText":"I'm a child"}],"Date":"2020-07-12T03:48:36.933Z","En":"Val1","SomeText":"xyz\r\nnext line","SomeInt":4,"SomeTextNode":"node value & with special characters \r\n>>\t great!\"yay!\"","SomeIntNode":5}
```

Finally, you could treat unparsed data dynamically without predefined class structures:
```csharp
UnparsedItem jo = SerEx.UnparsedJson(json);
Dictionary<string, UnparsedItem> joDic = jo.ParseClass();
UnparsedItem[] joDicChildren = joDic["Children"].ParseArray();
Data joChild = jo["Children"][0].Parse<Data>();
```
When the class defining the structure has a public field of type object, the JSON or QN will be parsed as "UnparsedItem", allowing late parsing/translation of text-data to usable models.