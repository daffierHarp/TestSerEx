# XML and QN Serialization

Serialization helpers through extension methods and introducing QN.

QN stands for Quick-server data objects Notation.

This form of serialization and de-serialization is used by FAAC/MILO in certain communication
protocols and provides notation that is similar to JSON, yet entirely built in C#. The Serialization-Extension class called SerEx implements
all of the required code.

For example, to serialize POCO classes to XML as a formatted document:

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
var data3 = SerEx.FromQn(qn);
```
