//  SerEx.cs Latest resharper cleanup: 07/07/2020
//  Copyright © FAAC. All rights reserved.

#region using

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
// ReSharper disable UnusedMember.Global

#endregion

namespace testXml
{
    public static class SerEx
    {
        // the problem with this version is that it includes XML line, and specifies the encoding as Unicode, though it might
        // be later encoded as UTF8 binary data. Another issue is that new-lines are not entitized by default
        public static string ToXml<T>(this T item)
        {
            var ser = new XmlSerializer(typeof(T));
            var sb = new StringBuilder();
            var w = new StringWriter(sb);
            ser.Serialize(w, item);
            w.Flush();
            return sb.ToString();
        }

        // this version either makes a minimal text, or the non-minimal one is well-formed document, and the xml line has UTF8 encoding
        public static string ToXml<T>(this T item, bool minimal, bool removeNamespace = true, bool newLineEntitize = true)
        {
            var ser = new XmlSerializer(typeof(T));
            var sb = new StringBuilder();
            var stream = new StringWriter(sb);

            var settings = new XmlWriterSettings {
                Indent = !minimal,
                NewLineHandling = newLineEntitize || !minimal ? NewLineHandling.Entitize: NewLineHandling.None,
                Encoding = Encoding.UTF8, // minimal ? Encoding.UTF8 : Encoding.Unicode,
                OmitXmlDeclaration = minimal,
                ConformanceLevel = ConformanceLevel.Auto,
                NamespaceHandling = NamespaceHandling.OmitDuplicates,
            };
            using (var writer = XmlWriter.Create(stream, settings)) {
                ser.Serialize(writer, item);
            }

            stream.Flush();
            if (removeNamespace)
                sb.Replace(
                    " xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\"",
                    "");
            return sb.ToString();
        }

        public static T FromXml<T>(string xml)
        {
            if (xml == null)
                return default(T);
            var ser = new XmlSerializer(typeof(T));
            var reader = new StringReader(xml);
            var result = (T) ser.Deserialize(reader);
            reader.Close();
            return result;
        }

        public static void SaveXml<T>(this T item, string path)
        {
            var ser = new XmlSerializer(typeof(T));
            // ReSharper disable once ConvertToUsingDeclaration
            using (var w = new StreamWriter(path)) {
                ser.Serialize(w, item);
            }
        }

        public static T FromXmlFile<T>(string path)
        {
            var ser = new XmlSerializer(typeof(T));
            var reader = new StreamReader(path);
            var result = (T) ser.Deserialize(reader);
            reader.Close();
            return result;
        }

        // create QuickServer complex data object notation
        public static string ToQn<T>(this T item) => encodeComplexDataInner(item, 0, new HashSet<object>());

        public static T FromQn<T>(string qn, bool tryQuotes = false) => (T) decodeComplexData(qn, typeof(T), tryQuotes);

        #region QN

        // ReSharper disable once InconsistentNaming
        public static Action<string, int> doLog = (s, level) => { Debug.WriteLine($"{level}:\t{s}"); };

        // allow avoiding encoding of certain objects as QN, instead use a default value. Useful when existing legacy code
        // pushes encoding of huge object as a source of event, but the destination doesn't need the data.
        public static readonly Dictionary<object, string> ComplexOverride = new Dictionary<object, string>();
        public static int MaxFieldsComplexDataFields = 50;

        static string encodeComplexDataInner(object data, int inDepth, HashSet<object> cyclesTraceList,
            int maxDepth = 12)
        {
            if (inDepth >= maxDepth) {
                doLog($"complex data is deeper than {maxDepth} connections!", 10);
                return "";
            }

            if (data is Exception ex) {
                return "\"" +
                       ("<Exception Text=\"" + ex.Message.Replace("\"", "&quot;") + "\"/>").Replace("\"", "\"\"") +
                       "\"";
            }

            if (data != null && data.GetType().IsClass) {
                if (ComplexOverride.ContainsKey(data)) return ComplexOverride[data];
                if (cyclesTraceList.Contains(data)) {
                    doLog("complex data cycle found", 10);
                    return "";
                }

                cyclesTraceList.Add(data);
            }

            try {
                if (data == null) return "";
                var t = data.GetType();
                if (data is string dataStr) return "\"" + dataStr.Replace("\"", "\"\"") + "\"";
                if (t.IsPrimitive || t.IsEnum) return Convert.ToString(data, CultureInfo.InvariantCulture);
                if (t == typeof(byte[])) return "\"" + Convert.ToBase64String((byte[]) data) + "\"";
                var sb = new StringBuilder();
                if (data is IDictionary d && (t.HasElementType || t.GenericTypeArguments.Length == 2)) {
                    sb.Append('[');
                    var firstDicV = true;
                    foreach (var key in d.Keys) {
                        if (!firstDicV)
                            sb.Append(',');
                        else
                            firstDicV = false;
                        sb.Append(encodeComplexDataInner(key, inDepth + 1, cyclesTraceList, maxDepth));
                        sb.Append(':');
                        var v = d[key];
                        sb.Append(encodeComplexDataInner(v, inDepth + 1, cyclesTraceList, maxDepth));
                    }

                    sb.Append(']');
                    return sb.ToString();
                }

                if (!t.IsArray && data is IEnumerable e && (t.HasElementType || t.GenericTypeArguments.Length == 1)) {
                    // turn unknown enumerable with an element type to an array
                    var elType = t.GenericTypeArguments.Length == 1
                        ? t.GenericTypeArguments[0]
                        : t.GetElementType();
                    var list = new ArrayList();
                    foreach (var line in e)
                        list.Add(line);
                    // ReSharper disable once AssignNullToNotNullAttribute
                    var arr = Array.CreateInstance(elType, list.Count);
                    list.CopyTo(arr);
                    data = arr;
                }

                if (data is Array array) {
                    sb.Append('[');
                    for (var i = 0; i < array.Length; i++) {
                        // what about multi dimensional? don't support, don't do it.
                        var item = array.GetValue(i);
                        if (i > 0) sb.Append(',');
                        sb.Append(encodeComplexDataInner(item, inDepth + 1, cyclesTraceList, maxDepth));
                    }

                    sb.Append(']');
                    return sb.ToString();
                }

                sb.Append('(');
                var fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public);
                var props = t.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                if (fields.Length + props.Length > MaxFieldsComplexDataFields) {
                    doLog("too many fields and properties!", 10);
                    return "";
                }

                var firstV = true;
                foreach (var fi in fields) {
                    var v = fi.GetValue(data);
                    if (testDefault(v)) continue;
                    if (!firstV)
                        sb.Append(',');
                    else
                        firstV = false;
                    sb.Append(fi.Name);
                    sb.Append(':');
                    sb.Append(encodeComplexDataInner(v, inDepth + 1, cyclesTraceList, maxDepth));
                }

                foreach (var pi in props) {
                    if (pi.IsSpecialName || !pi.CanRead || !pi.CanWrite || pi.GetIndexParameters().Length > 0)
                        continue;
                    var v = pi.GetValue(data, null);
                    if (testDefault(v)) continue;
                    if (!firstV)
                        sb.Append(',');
                    else
                        firstV = false;
                    sb.Append(pi.Name);
                    sb.Append(':');
                    sb.Append(encodeComplexDataInner(v, inDepth + 1, cyclesTraceList, maxDepth));
                }

                sb.Append(')');
                return sb.ToString();
            } finally {
                if (data != null && data.GetType().IsClass)
                    cyclesTraceList.Remove(data);
            }
        }

        static bool testDefault(object v, Type t = null)
        {
            if (v == null) return true;
            if (t == null) t = v.GetType();
            if (!t.IsPrimitive && !t.IsValueType || v.GetType() != t) return false;
            var def = Activator.CreateInstance(t);
            return Equals(v, def);
        }

        static string decodeQuotedString(ParseHelper helper)
        {
            Debug.Assert(helper.Current == '\"');
            helper.SkipOne();
            var sb = new StringBuilder();
            while (!helper.HasEnded) {
                sb.Append(helper.ReadToAndSkip('\"'));
                if (helper.Current != '\"')
                    break;
                sb.Append('\"');
                helper.SkipOne();
            }

            return sb.ToString();
        }

        static object decodeComplexData(string encoded, Type t, bool tryQuotes = false)
        {
            if (encoded == "" || encoded == "(null)") return null; // internal null object
            if (encoded.StartsWith("\"<Exception "))
                encoded = decodeQuotedString(new ParseHelper(encoded));
            if (encoded.StartsWith("<Exception ")) {
                var exLine = encoded;
                var i0 = exLine.IndexOf('\"');
                var i1 = exLine.LastIndexOf('\"');
                var msg = exLine.Substring(i0 + 1, i1 - i0 - 1).Replace("&quot;", "\"");
                return new Dictionary<string, object> {{"IsException", true}, {"Message", msg}};
            }

            if (t == null) return encoded;
            if (t.IsInterface) {
                doLog("Decoding data as interface not supported!", 10);
                return null;
            }

            var helper = new ParseHelper(encoded);
            if (t == typeof(string))
                return tryQuotes && helper.Current == '\"' ? decodeQuotedString(helper) : encoded;
            if (encoded.StartsWith("(")) {
                var resultRecord = Activator.CreateInstance(t);
                helper.SkipOne();

                // record
                while (helper.Current != ')') {
                    helper.SkipWhiteSpaces();

                    var fieldName = helper.ReadToAndSkip(':').Trim(' ', '\r', '\n', '\t');
                    helper.SkipWhiteSpaces();
                    var fi = t.GetField(fieldName);
                    var pi = t.GetProperty(fieldName);
                    var ft = fi == null ? pi == null ? null : pi.PropertyType : fi.FieldType;

                    // ReSharper disable once RedundantAssignment
                    object fieldValue = null;
                    if (helper.Current == '(' || helper.Current == '[') {
                        var complex = readComplexBlock(helper);
                        fieldValue = decodeComplexData(complex, ft);
                    } else if (helper.Current == '\"') {
                        fieldValue = decodeQuotedString(helper);
                    } else {
                        var dataStr = helper.ReadToAny(",)");
                        fieldValue = decodeComplexData(dataStr, ft);
                    }

                    if (ft == typeof(byte[]) && fieldValue is string str)
                        fieldValue = decodeComplexData(str, ft);
                    if (fi != null) fi.SetValue(resultRecord, fieldValue);
                    else if (pi != null)
                        try {
                            pi.SetValue(resultRecord, fieldValue, null);
                        } catch { }

                    helper.SkipWhiteSpaces();
                    if (helper.Current == ',') helper.SkipOne();
                    helper.SkipWhiteSpaces();
                }

                return resultRecord;
            }

            if (encoded.StartsWith("[")) {
                helper.SkipOne();
                // array or dictionary
                if (typeof(IDictionary).IsAssignableFrom(t)) {
                    var dic = (IDictionary) Activator.CreateInstance(t);
                    Debug.Assert(dic != null);
                    var keyType = t.GetGenericArguments()[0];
                    var valueType = t.GetGenericArguments()[1];

                    while (helper.Current != ']') {
                        helper.SkipWhiteSpaces();
                        string keyPart;
                        if (helper.Current == '(' || helper.Current == '[')
                            keyPart = readComplexBlock(helper, true);
                        else
                            keyPart = helper.ReadToAny(":").Trim(' ', '\t', '\r', '\n');

                        var key = decodeComplexData(keyPart, keyType, true);
                        helper.SkipOne(); // :
                        helper.SkipWhiteSpaces();
                        string vPart;
                        if (helper.Current == '(' || helper.Current == '[')
                            vPart = readComplexBlock(helper);
                        else
                            vPart = helper.ReadToAny("],");
                        vPart = vPart.Trim(' ', '\t', '\r', '\n');
                        var v = decodeComplexData(vPart, valueType, true);
                        dic.Add(key, v);
                        helper.SkipWhiteSpaces();
                        if (helper.Current == ',') helper.SkipOne();
                    }

                    return dic;
                }

                var elT = t.GenericTypeArguments.Length == 1 ? t.GenericTypeArguments[0] : t.GetElementType();
                Debug.Assert(elT != null);
                var listType = typeof(List<>).MakeGenericType(elT);
                var list = (IList) Activator.CreateInstance(listType);
                Debug.Assert(list != null);
                while (helper.Current != ']') {
                    helper.SkipWhiteSpaces();
                    if (helper.Current == '\"') {
                        // inStr
                        Debug.Assert(elT == typeof(string));
                        var str = decodeQuotedString(helper);
                        list.Add(str);
                    } else {
                        string elPart;
                        if (helper.Current == '(' || helper.Current == '[')
                            elPart = readComplexBlock(helper);
                        else
                            elPart = helper.ReadToAny(",]");
                        var item = decodeComplexData(elPart, elT, true);
                        list.Add(item);
                    }

                    helper.SkipWhiteSpaces();
                    if (helper.Current == ',') helper.SkipOne();
                }

                if (t.IsArray) {
                    var arr = Array.CreateInstance(elT, list.Count);
                    list.CopyTo(arr, 0);
                    return arr;
                }

                return list; // IEnumerable<T> or list<T>
            }

            if (t == typeof(string)) {
                if (tryQuotes && helper.IsCurrentWhiteSpace() &&
                    helper.NextNonWhiteSpace(out var nextNonWhiteIndex) == '\"')
                    helper.SkipToIndex(nextNonWhiteIndex);
                return tryQuotes && helper.Current == '\"' ? decodeQuotedString(helper) : encoded;
            }

            if (helper.IsCurrentWhiteSpace() || ParseHelper.IsWhiteSpace(encoded[encoded.Length - 1])) {
                encoded = encoded.Trim(' ', '\t', '\r', '\n');
                helper = new ParseHelper(encoded);
            }

            if (t == typeof(int)) return int.Parse(encoded);
            if (t == typeof(float)) return float.Parse(encoded, CultureInfo.InvariantCulture);
            if (t == typeof(double)) return double.Parse(encoded, CultureInfo.InvariantCulture);
            if (t == typeof(decimal)) return decimal.Parse(encoded, CultureInfo.InvariantCulture);
            if (t == typeof(bool)) return bool.Parse(encoded);
            if (t == typeof(byte)) return byte.Parse(encoded);
            if (t == typeof(byte[])) {
                var txt = helper.Current == '\"' ? decodeQuotedString(helper) : encoded;
                return Convert.FromBase64String(txt);
            }

            if (t.IsEnum) return Enum.Parse(t, encoded);
            if (t == typeof(string[])) // legacy
                return encoded.Split('|');
            throw new NotImplementedException();
        }

        static string readComplexBlock(ParseHelper helper, bool stopAtNextColon = false)
        {
            var sb = new StringBuilder();
            var blockStack = new Stack<char>();
            var inStr = false;
            blockStack.Push(',');
            while ("[(".IndexOf(helper.Current) >= 0) {
                sb.Append(helper.Current);
                blockStack.Push(helper.Current);
                helper.SkipOne();
            }

            while (blockStack.Count > 0) {
                var readPart = helper.ReadToAny(",;}[]()\":");
                sb.Append(readPart);
                var lastChr = helper.Current;
                if (inStr) {
                    if (lastChr == '\"') inStr = false;

                    sb.Append(lastChr);
                    helper.SkipOne();
                    continue;
                }

                if (lastChr == '\"')
                    inStr = true;
                else if ("[(".IndexOf(lastChr) >= 0)
                    blockStack.Push(lastChr);
                else
                    switch (blockStack.Peek()) {
                        case '[':
                            if (lastChr == ']') blockStack.Pop();
                            break;
                        case '(':
                            if (lastChr == ')') blockStack.Pop();
                            break;
                        case ',':
                            if (lastChr == ',' || lastChr == ';'
                                               || lastChr == ')' || lastChr == ']'
                                               || lastChr == ':' && stopAtNextColon) blockStack.Pop();
                            break;
                    }

                if (blockStack.Count > 0) {
                    sb.Append(lastChr);
                    helper.SkipOne();
                }
            }

            var complexDataString = sb.ToString();
            return complexDataString;
        }

        #region ParseHelper

        class ParseHelper
        {
            readonly string _msg;
            int _iMsg;

            public ParseHelper(string msg)
            {
                _msg = msg;
            }

            public char Current
            {
                get
                {
                    if (HasEnded) return (char) 0;
                    return _msg[_iMsg];
                }
            }

            public bool HasEnded => _iMsg < 0 || _iMsg >= _msg.Length || _msg[_iMsg] == '}';

            public void SkipOne()
            {
                _iMsg++;
            }

            bool skipTo(char c)
            {
                var nextC = _msg.IndexOf(c, _iMsg);
                if (nextC < 0)
                    return false;
                _iMsg = nextC;
                return true;
            }

            // ReSharper disable once UnusedMember.Local
            public void SkipToAny(string characters)
            {
                //int oldI = iMsg;
                while (!HasEnded && characters.IndexOf(_msg[_iMsg]) < 0)
                    _iMsg++;
            }

            // ReSharper disable once UnusedMember.Local
            public bool SkipPass(char c)
            {
                if (!skipTo(c)) return false;
                _iMsg++;
                return true;
            }

            string readTo(char c)
            {
                var nextC = _msg.IndexOf(c, _iMsg);
                if (nextC < 0)
                    return null;
                var result = _msg.Substring(_iMsg, nextC - _iMsg);
                _iMsg = nextC;
                return result;
            }

            public string ReadToAny(string characters)
            {
                var nextC = _iMsg;
                while (nextC < _msg.Length && characters.IndexOf(_msg[nextC]) < 0)
                    nextC++;
                if (nextC == _msg.Length)
                    return null;
                var l = nextC - _iMsg;
                var result = l > 0 ? _msg.Substring(_iMsg, nextC - _iMsg) : "";
                _iMsg = nextC;
                return result;
            }

            public string ReadToAndSkip(char c)
            {
                var result = readTo(c);
                if (result == null)
                    return null;
                _iMsg++;
                return result;
            }

            public void SkipWhiteSpaces()
            {
                while (IsCurrentWhiteSpace())
                    _iMsg++;
            }

            // ReSharper disable once UnusedMember.Local
            public int IndexOfNext(char c)
            {
                if (HasEnded)
                    return -1;
                return _msg.IndexOf(c, _iMsg);
            }

            public char NextNonWhiteSpace(out int indexOfNextNonWhiteSpace)
            {
                indexOfNextNonWhiteSpace = -1;
                if (HasEnded) return ' ';
                var atI = _iMsg;
                while (atI < _msg.Length && IsWhiteSpace(_msg[atI]))
                    atI++;
                if (atI >= _msg.Length)
                    return ' ';
                indexOfNextNonWhiteSpace = atI;
                return _msg[atI];
            }

            public static bool IsWhiteSpace(char c) =>
                char.IsWhiteSpace(c) || c == '\r' || c == '\n' ||
                c == '\t';

            // ReSharper disable once MemberCanBePrivate.Local
            public bool IsCurrentWhiteSpace()
            {
                if (HasEnded) return false;
                return IsWhiteSpace(_msg[_iMsg]);
            }

            public void SkipToIndex(int nextIndex)
            {
                if (nextIndex < 0) return;
                if (nextIndex >= _msg.Length)
                    nextIndex = _msg.Length;
                if (nextIndex > _iMsg)
                    _iMsg = nextIndex;
            }
        }

        #endregion

        #endregion
    }
}