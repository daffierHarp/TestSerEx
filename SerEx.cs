//  SerEx.cs Latest resharper cleanup: 07/09/2020
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

// ReSharper disable once CheckNamespace
namespace Helpers
{
    // TODO: create method to build C# definition of model from json
    // TODO: fix json enum values to be within quotes

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
        public static string ToXml<T>(this T item, bool minimal, bool removeNamespace = true,
            bool newLineEntitize = true)
        {
            var ser = new XmlSerializer(typeof(T));
            var sb = new StringBuilder();
            var stream = new StringWriter(sb);

            var settings = new XmlWriterSettings {
                Indent = !minimal,
                NewLineHandling = newLineEntitize || !minimal ? NewLineHandling.Entitize : NewLineHandling.None,
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
        public static string ToQn<T>(this T item, QnConfig qnCfg = null) =>
            encodeComplexDataInner(item, 0, new HashSet<object>(), qnCfg);

        public static T FromQn<T>(string qn, bool tryQuotes = false) => (T) decodeComplexData(qn, typeof(T), tryQuotes);

        public static T FromQn<T>(string qn, QnConfig qnCfg, bool tryQuotes = false) =>
            (T) decodeQnInner(qn, typeof(T), qnCfg, tryQuotes);

        public static string ToJson<T>(this T o) => o.ToQn(QnConfig.Json);
        public static T FromJson<T>(string json) => FromQn<T>(json, QnConfig.Json);
        public static QnObject ParseQn(string qn) => FromQn<QnObject>(qn, QnConfig.Default); // QnObject isn't supported by the legacy parser that doesn't use QnConfig
        public static QnObject ParseJSon(string json) => FromQn<QnObject>(json, QnConfig.Json);

        #region QN

        // ReSharper disable once InconsistentNaming
        public static Action<string, int> doLog = (s, level) => { Debug.WriteLine($"{level}:\t{s}"); };

        // allow avoiding encoding of certain objects as QN, instead use a default value. Useful when existing legacy code
        // pushes encoding of huge object as a source of event, but the destination doesn't need the data.
        public static readonly Dictionary<object, string> ComplexOverride = new Dictionary<object, string>();

        static string encodeComplexDataInner(object data, int inDepth, HashSet<object> cyclesTraceList,
            QnConfig qnCfg = null)
        {
            if (qnCfg == null) qnCfg = QnConfig.Default;
            if (inDepth >= qnCfg.MaxDepth) {
                doLog($"complex data is deeper than {qnCfg.MaxDepth} connections!", 10);
                return "";
            }

            if (data is Exception ex) {
                if (qnCfg.ExceptionAsStringEncodedXml)
                    return "\"" +
                           ("<Exception Text=\"" + ex.Message.Replace("\"", "&quot;") + "\"/>").Replace("\"", "\"\"") +
                           "\"";
                return string.Format(qnCfg.ExceptionStringFormat, ex.Message.Escape(false));
            }

            if (data != null && data.GetType().IsClass && data.GetType()!=typeof(string)) {
                if (ComplexOverride.ContainsKey(data)) return ComplexOverride[data];
                if (cyclesTraceList.Contains(data)) {
                    doLog("complex data cycle found", 10);
                    return qnCfg.NullStr;
                }

                cyclesTraceList.Add(data);
            }

            try {
                if (data == null) return qnCfg.NullStr;
                var t = data.GetType();
                if (data is string dataStr)
                    return qnCfg.EncodeStringAsDoubleQuote
                        ? qnCfg.Quote + dataStr.Replace($"{qnCfg.Quote}", $"{qnCfg.Quote}{qnCfg.Quote}") + qnCfg.Quote
                        : dataStr.Escape(quoteChar: qnCfg.Quote);
                if (data is DateTime dt) {
                    var dateText = qnCfg.UseDateFormatter
                        ? dt.ToString(qnCfg.DateFormat)
                        : dt.ToString("g", CultureInfo.InvariantCulture);
                    if (!string.IsNullOrWhiteSpace(qnCfg.DateStringFormat))
                        return string.Format(qnCfg.DateStringFormat, dateText);
                    return $"{qnCfg.Quote}{dateText}{qnCfg.Quote}";
                }

                if (t == typeof(bool) && qnCfg.BooleanAsLowecase) return (bool) data ? "true" : "false";
                if (t.IsPrimitive || t.IsEnum) return Convert.ToString(data, CultureInfo.InvariantCulture);
                if (t == typeof(byte[])) return qnCfg.Quote + Convert.ToBase64String((byte[]) data) + qnCfg.Quote;
                var sb = new StringBuilder();
                if (data is IDictionary d && (t.HasElementType || t.GenericTypeArguments.Length == 2)) {
                    if (qnCfg.OpenDictionary.Length == 1 || qnCfg.OpenDictionary.IndexOf('{') < 0)
                        sb.Append(qnCfg.OpenDictionary);
                    else
                        sb.Append(string.Format(qnCfg.OpenDictionary, t.GetGenericArguments()[0].Name,
                            t.GetGenericArguments()[1].Name));
                    var firstDicV = true;
                    foreach (var key in d.Keys) {
                        if (!firstDicV)
                            sb.Append(',');
                        else
                            firstDicV = false;
                        if(!string.IsNullOrWhiteSpace(qnCfg.DictionaryItemOpen)) sb.Append(qnCfg.DictionaryItemOpen);
                        sb.Append(encodeComplexDataInner(key, inDepth + 1, cyclesTraceList, qnCfg));
                        sb.Append(qnCfg.DictionarySep);
                        var v = d[key];
                        sb.Append(encodeComplexDataInner(v, inDepth + 1, cyclesTraceList, qnCfg));
                        if (!string.IsNullOrWhiteSpace(qnCfg.DictionaryItemClose)) sb.Append(qnCfg.DictionaryItemClose);
                    }

                    sb.Append(qnCfg.CloseDictionary);
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
                    sb.Append(qnCfg.OpenArray);
                    for (var i = 0; i < array.Length; i++) {
                        // what about multi dimensional? don't support, don't do it.
                        var item = array.GetValue(i);
                        if (i > 0) sb.Append(',');
                        sb.Append(encodeComplexDataInner(item, inDepth + 1, cyclesTraceList, qnCfg));
                    }

                    sb.Append(qnCfg.CloseArray);
                    return sb.ToString();
                }

                if (qnCfg.AddClassName) {
                    if (qnCfg.AddNewKeywordToClassName) sb.Append("new ");
                    sb.Append(t.Name);
                }

                sb.Append(qnCfg.OpenRecord);
                var fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public);
                var props = t.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                if (fields.Length + props.Length > qnCfg.FieldsLimit) {
                    doLog("too many fields and properties!", 10);
                    return qnCfg.NullStr;
                }

                var firstV = true;
                foreach (var fi in fields) {
                    var v = fi.GetValue(data);
                    if (testDefault(v)) continue;
                    if (!firstV)
                        sb.Append(',');
                    else
                        firstV = false;
                    if (qnCfg.FieldNameInQuotes) sb.Append(qnCfg.Quote);
                    sb.Append(fi.Name);
                    if (qnCfg.FieldNameInQuotes) sb.Append(qnCfg.Quote);
                    sb.Append(qnCfg.FieldSep);
                    sb.Append(encodeComplexDataInner(v, inDepth + 1, cyclesTraceList, qnCfg));
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
                    if (qnCfg.FieldNameInQuotes) sb.Append(qnCfg.Quote);
                    sb.Append(pi.Name);
                    if (qnCfg.FieldNameInQuotes) sb.Append(qnCfg.Quote);
                    sb.Append(qnCfg.FieldSep);
                    sb.Append(encodeComplexDataInner(v, inDepth + 1, cyclesTraceList, qnCfg));
                }

                sb.Append(qnCfg.CloseRecord);
                return sb.ToString();
            } finally {
                if (data != null && data.GetType().IsClass && cyclesTraceList.Contains(data))
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

        static string decodeQnString(ParseHelper helper, QnConfig qnCfg)
        {
            Debug.Assert(helper.Current == qnCfg.Quote);
            helper.SkipOne();
            if (qnCfg.EncodeStringAsDoubleQuote) {
                var sb = new StringBuilder();
                while (!helper.HasEnded) {
                    sb.Append(helper.ReadToAndSkip(qnCfg.Quote));
                    if (helper.Current != qnCfg.Quote)
                        break;
                    sb.Append(qnCfg.Quote);
                    helper.SkipOne();
                }

                return sb.ToString();
            }

            var i0 = helper.CurrentIndex;
            var strQuoteAndEscape = $"\\{qnCfg.Quote}";
            while (!helper.HasEnded) {
                helper.ReadToAny(strQuoteAndEscape);
                if (helper.Current == qnCfg.Quote || helper.HasEnded)
                    break;
                helper.SkipOne();
                helper.SkipOne();
            }

            string s = helper.AllText.Substring(i0, helper.CurrentIndex - i0);
            if (helper.Current==qnCfg.Quote)
                helper.SkipOne();
            return s.Unescape();
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

                    if (ft == typeof(byte[]) && fieldValue is string str) fieldValue = decodeComplexData(str, ft);
                    if (ft == typeof(DateTime))
                        fieldValue = DateTime.TryParseExact((string) fieldValue, "g", CultureInfo.InvariantCulture,
                            DateTimeStyles.None, out var dateResult)
                            ? dateResult
                            : default(DateTime);
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
                        var str = decodeQuotedString(helper);
                        if (elT == typeof(byte[])) {
                            list.Add(Convert.FromBase64String(str));
                        } else {
                            Debug.Assert(elT == typeof(string));
                            list.Add(str);
                        }
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
            if (t == typeof(DateTime?)) {
                if (string.IsNullOrWhiteSpace(encoded) || encoded == "\"\"") return null;
                t = typeof(DateTime);
            }

            if (t == typeof(DateTime)) {
                if (DateTime.TryParseExact(encoded.Trim('\"'), "g", CultureInfo.InvariantCulture, DateTimeStyles.None,
                    out var dateResult))
                    return dateResult;
                return default(DateTime);
            }

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

        static object decodeQnInner(Slice encoded, Type t, QnConfig qnCfg, bool tryQuotes = false)
        {
            if (encoded == qnCfg.NullStr || encoded == "(null)") return null; // internal null object
            if (qnCfg.ExceptionAsStringEncodedXml) {
                if (encoded.StartsWith("\"<Exception "))
                    encoded = decodeQnString(new ParseHelper(encoded), qnCfg);
                if (encoded.StartsWith("<Exception ")) {
                    var exLine = encoded;
                    var i0 = exLine.IndexOf('\"');
                    var i1 = exLine.LastIndexOf('\"');
                    var msg = exLine.Substring(i0 + 1, i1 - i0 - 1).ToString().Replace("&quot;", "\"");
                    return new Dictionary<string, object> {{"IsException", true}, {"Message", msg}};
                }
            }

            if (t == null || t == typeof(object) || t == typeof(QnObject)) return new QnObject {Config = qnCfg, RawText = encoded};
            if (t.IsInterface) {
                doLog("Decoding data as interface not supported!", 10);
                return null;
            }

            var helper = new ParseHelper(encoded);
            if (t == typeof(string))
                return tryQuotes && helper.Current == qnCfg.Quote
                    ? decodeQnString(helper, qnCfg)
                    : qnCfg.EncodeStringAsDoubleQuote
                        ? encoded.ToString().Replace($"{qnCfg.Quote}{qnCfg.Quote}", $"{qnCfg.Quote}")
                        : encoded.Unescape(false);
            if (helper.IsCurrentWhiteSpace() || ParseHelper.IsWhiteSpace(encoded[encoded.Length - 1])) {
                encoded = encoded.Trim(' ', '\t', '\r', '\n');
                helper = new ParseHelper(encoded);
            }

            if (t == typeof(int)) return int.Parse(encoded);
            if (t == typeof(float)) return float.Parse(encoded, CultureInfo.InvariantCulture);
            if (t == typeof(double)) return double.Parse(encoded, CultureInfo.InvariantCulture);
            if (t == typeof(decimal)) return decimal.Parse(encoded, CultureInfo.InvariantCulture);
            if (t == typeof(bool)) {
                var str = encoded.ToString();
                if (bool.TryParse(str, out var b)) return b;
                if (str.Equals("true", StringComparison.InvariantCultureIgnoreCase)) return true;
                if (str.Equals("false", StringComparison.InvariantCultureIgnoreCase)) return false;
                doLog("invalid boolean value", 10);
                return false;
            }

            if (t == typeof(byte)) return byte.Parse(encoded);
            if (t == typeof(byte[])) {
                var txt = helper.Current == '\"' ? decodeQnString(helper, qnCfg) : (string) encoded;
                return Convert.FromBase64String(txt);
            }

            if (t.IsEnum) return Enum.Parse(t, encoded);
            if (t == typeof(DateTime?)) {
                if (string.IsNullOrWhiteSpace(encoded) || encoded == "\"\"") return null;
                t = typeof(DateTime);
            }

            if (t == typeof(DateTime)) {
                if (DateTime.TryParseExact(encoded.Trim('\"'), "g", CultureInfo.InvariantCulture, DateTimeStyles.None,
                    out var dateResult))
                    return dateResult;
                doLog($"failed to parse date-time {encoded}", 10);
                return default(DateTime);
            }

            if (typeof(IDictionary).IsAssignableFrom(t)) {
                var closeDic = qnCfg.CloseDictionary;
                var openDic = qnCfg.OpenDictionary;
                if (!helper.PeekPhrase(openDic)) {
                    if (helper.PeekPhrase(qnCfg.OpenRecord+""))
                    {
                        openDic = qnCfg.OpenRecord + "";
                        closeDic = qnCfg.CloseRecord;
                    }
                    else
                        throw new Exception("cannot parse data as dictionary");
                }
                    
                helper.SkipPhrase(openDic);
                // array or dictionary
                var dic = (IDictionary) Activator.CreateInstance(t);
                Debug.Assert(dic != null);
                var keyType = t.GetGenericArguments()[0];
                var valueType = t.GetGenericArguments()[1];

                while (helper.Current != closeDic) {
                    helper.SkipWhiteSpaces();
                    string keyPart;
                    // TODO: support new() and dictionary type formats
                    if (qnCfg.AddNewKeywordToClassName && helper.PeekPhrase("new ")) helper.SkipPhrase("new ");
                    if (qnCfg.AddClassName && char.IsLetter(helper.Current)) helper.SkipToAny($" {qnCfg.OpenRecord}{qnCfg.OpenArray}{qnCfg.OpenDictionary}");
                    if (helper.Current == qnCfg.OpenRecord || helper.PeekPhrase(qnCfg.OpenDictionary) || helper.PeekPhrase(qnCfg.OpenArray))
                        keyPart = readQnBlock(helper, qnCfg, true);
                    else
                        keyPart = helper.ReadToAny($"{qnCfg.DictionarySep}").Trim(' ', '\t', '\r', '\n');
                    // TODO: support dictionary item open/close characters
                    var key = decodeQnInner(keyPart, keyType, qnCfg, true);
                    helper.SkipOne(); // :
                    helper.SkipWhiteSpaces();
                    string vPart;
                    if (qnCfg.AddNewKeywordToClassName && helper.PeekPhrase("new ")) helper.SkipPhrase("new ");
                    if (qnCfg.AddClassName && char.IsLetter(helper.Current)) helper.SkipToAny($" {qnCfg.OpenRecord}{qnCfg.OpenArray}{qnCfg.OpenDictionary}");
                    if (helper.Current == qnCfg.OpenRecord || helper.PeekPhrase(qnCfg.OpenDictionary) || helper.PeekPhrase(qnCfg.OpenArray))
                        vPart = readQnBlock(helper, qnCfg);
                    else
                        vPart = helper.ReadToAny($"{closeDic},");
                    vPart = vPart.Trim(' ', '\t', '\r', '\n');
                    var v = decodeQnInner(vPart, valueType, qnCfg, true);
                    dic.Add(key, v);
                    helper.SkipWhiteSpaces();
                    if (helper.Current == ',') helper.SkipOne();
                }

                return dic;
            }

            if (qnCfg.AddNewKeywordToClassName && helper.PeekPhrase("new "))
                helper.SkipPhrase("new ");
            if (qnCfg.AddClassName && char.IsLetter(helper.Current)) helper.SkipToAny($" {qnCfg.OpenRecord}{qnCfg.OpenArray}{qnCfg.OpenDictionary}");
            if (encoded.StartsWith(qnCfg.OpenRecord)) {
                var resultRecord = Activator.CreateInstance(t);
                helper.SkipOne();

                // record
                while (helper.Current != qnCfg.CloseRecord) {
                    helper.SkipWhiteSpaces();

                    var fieldName = helper.ReadToAndSkip(qnCfg.FieldSep).Trim(' ', '\r', '\n', '\t');
                    if (qnCfg.FieldNameInQuotes)
                        fieldName = fieldName.Trim('\"', '\'');
                    helper.SkipWhiteSpaces();
                    var fi = t.GetField(fieldName);
                    var pi = t.GetProperty(fieldName);
                    var ft = fi == null ? pi == null ? null : pi.PropertyType : fi.FieldType;

                    // ReSharper disable once RedundantAssignment
                    object fieldValue = null;
                    if (qnCfg.AddNewKeywordToClassName && helper.PeekPhrase("new ")) helper.SkipPhrase("new ");
                    if (qnCfg.AddClassName && char.IsLetter(helper.Current)) helper.SkipToAny($" {qnCfg.OpenRecord}{qnCfg.OpenArray}{qnCfg.OpenDictionary}");
                    if (helper.Current == qnCfg.OpenRecord || helper.PeekPhrase(qnCfg.OpenDictionary) || helper.PeekPhrase(qnCfg.OpenArray)) {
                        var complex = readQnBlock(helper, qnCfg);
                        fieldValue = decodeQnInner(complex, ft, qnCfg);
                    } else if (helper.Current == qnCfg.Quote) {
                        fieldValue = decodeQnString(helper, qnCfg);
                    } else {
                        var dataStr = helper.ReadToAny($",{qnCfg.CloseRecord}");
                        fieldValue = decodeQnInner(dataStr, ft, qnCfg);
                    }

                    if (ft == typeof(byte[]) && fieldValue is string str) fieldValue = decodeQnInner(str, ft, qnCfg);
                    if (ft == typeof(DateTime))
                        fieldValue = DateTime.TryParseExact((string) fieldValue, qnCfg.DateFormat, CultureInfo.InvariantCulture,
                            DateTimeStyles.None, out var dateResult)
                            ? dateResult
                            : default(DateTime);
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

            if (encoded.StartsWith(qnCfg.OpenArray)) {
                helper.SkipOne();
                // array 
                var elT = t.GenericTypeArguments.Length == 1 ? t.GenericTypeArguments[0] : t.GetElementType();
                Debug.Assert(elT != null);
                var listType = typeof(List<>).MakeGenericType(elT);
                var list = (IList) Activator.CreateInstance(listType);
                Debug.Assert(list != null);
                while (helper.Current != qnCfg.CloseArray) {
                    helper.SkipWhiteSpaces();
                    if (helper.Current == qnCfg.Quote) {
                        // inStr
                        var str = decodeQnString(helper, qnCfg);
                        if (elT == typeof(byte[])) {
                            list.Add(Convert.FromBase64String(str));
                        } else {
                            Debug.Assert(elT == typeof(string));
                            list.Add(str);
                        }
                    } else {
                        string elPart;
                        if (qnCfg.AddNewKeywordToClassName && helper.PeekPhrase("new ")) helper.SkipPhrase("new ");
                        if (qnCfg.AddClassName && char.IsLetter(helper.Current)) helper.SkipToAny($" {qnCfg.OpenRecord}{qnCfg.OpenArray}{qnCfg.OpenDictionary}");
                        if (helper.Current == qnCfg.OpenRecord || helper.PeekPhrase(qnCfg.OpenDictionary) || helper.PeekPhrase(qnCfg.OpenArray))
                            elPart = readQnBlock(helper, qnCfg);
                        else
                            elPart = helper.ReadToAny($",{qnCfg.CloseArray}");
                        var item = decodeQnInner(elPart, elT, qnCfg, true);
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

            if (qnCfg.SupportLegacyStringArrayWithPipe && t == typeof(string[])) // legacy
                return encoded.ToString().Split('|');
            throw new NotImplementedException();
        }

        static Slice readQnBlock(ParseHelper helper, QnConfig qnCfg, bool stopAtNextColon = false)
        {
            var startIndex = helper.CurrentIndex;
            var blockStack = new Stack<char>();
            var inStr = false;
            blockStack.Push(',');
            var openChars = $"{qnCfg.OpenArray}{qnCfg.OpenRecord}{qnCfg.OpenDictionary}";
            while (openChars.IndexOf(helper.Current) >= 0) {
                blockStack.Push(helper.Current);
                helper.SkipOne();
            }

            var stopAtChars =
                $",;}}\\{qnCfg.OpenArray[0]}{qnCfg.CloseArray}{qnCfg.OpenRecord}{qnCfg.CloseRecord}{qnCfg.Quote}{qnCfg.DictionarySep}{qnCfg.FieldSep}{qnCfg.OpenDictionary[0]}{qnCfg.CloseDictionary}";
            var stopAtStrChars = $"\\\"\'";
            while (blockStack.Count > 0) {
#if DEBUG
                var soFar = helper.AllText.Substring(startIndex, helper.CurrentIndex - startIndex).ToString();
                if (helper.HasEnded) Debugger.Break();
#endif
                var readPart = inStr? helper.ReadToAny(stopAtStrChars) : helper.ReadToAny(stopAtChars);
                var lastChr = helper.Current;
                if (inStr) {
                    helper.SkipOne();
                    if (!qnCfg.EncodeStringAsDoubleQuote) {

                        if (lastChr == '\\') {
                            helper.SkipOne();
                            continue;
                        }
                    }

                    if (lastChr == qnCfg.Quote)
                        inStr = false;
                    continue;
                }

                if (lastChr == qnCfg.Quote) {
                    inStr = true;
                } else if (openChars.IndexOf(lastChr) >= 0) {
                    blockStack.Push(lastChr);
                } else if (blockStack.Peek() == qnCfg.OpenArray[0]) {
                    if (lastChr == qnCfg.CloseArray) blockStack.Pop();
                } else if (blockStack.Peek() == qnCfg.OpenRecord) {
                    if (lastChr == qnCfg.CloseRecord) blockStack.Pop();
                } else if (blockStack.Peek() == qnCfg.OpenDictionary[0]) {
                    if (lastChr == qnCfg.CloseDictionary) blockStack.Pop();
                }else if (blockStack.Peek() == ',') {
                    if (lastChr == ',' || lastChr == ';'
                                       || lastChr == qnCfg.CloseRecord || lastChr == qnCfg.CloseArray || lastChr == qnCfg.CloseDictionary 
                                       || (lastChr == qnCfg.FieldSep || lastChr == qnCfg.DictionarySep) &&
                                       stopAtNextColon) blockStack.Pop();
                }

                if (blockStack.Count > 0) helper.SkipOne();
            }

            return helper.AllText.Substring(startIndex, helper.CurrentIndex - startIndex);
        }

        #region string helpers

        public static string Escape(this string input, bool addQuotes = true, char quoteChar = '\"', bool excludeNonQuote = true)
        {
            var literal = new StringBuilder((int) (input.Length * 1.2 + 2));
            if (addQuotes) literal.Append(quoteChar);
            foreach (var c in input)
            {
                if (addQuotes && excludeNonQuote && (c == '\'' || c == '\"') && c != quoteChar) {
                    literal.Append(c);
                    continue;
                }
                switch (c) {
                    case '\'': literal.Append(@"\'"); break;
                    case '\"': literal.Append("\\\""); break;
                    case '\\': literal.Append(@"\\"); break;
                    case '\0': literal.Append(@"\0"); break;
                    case '\a': literal.Append(@"\a"); break;
                    case '\b': literal.Append(@"\b"); break;
                    case '\f': literal.Append(@"\f"); break;
                    case '\n': literal.Append(@"\n"); break;
                    case '\r': literal.Append(@"\r"); break;
                    case '\t': literal.Append(@"\t"); break;
                    case '\v': literal.Append(@"\v"); break;
                    default:
                        if (char.GetUnicodeCategory(c) != UnicodeCategory.Control) {
                            literal.Append(c);
                        } else {
                            literal.Append(@"\u");
                            literal.Append(((ushort) c).ToString("x4"));
                        }

                        break;
                }
            }

            if (addQuotes) literal.Append(quoteChar);
            return literal.ToString();
        }

        public static string Unescape(this string literal, bool stripQuotes = true, char quoteChar = '\"')
        {
            var result = new StringBuilder(literal.Length);
            var startIndex = 0;
            var endIndex = literal.Length;
            if (stripQuotes && literal.StartsWith(quoteChar)) {
                startIndex++;
                if (literal.EndsWith(quoteChar)) endIndex--;
            }

            var inEscape = false;
            for (var i = startIndex; i < endIndex; i++) {
                var c = literal[i];
                if (inEscape) {
                    inEscape = false;
                    switch (c) {
                        case '\'': result.Append('\''); break;
                        case '\"': result.Append('\"'); break;
                        case '\\': result.Append('\\'); break;
                        case '0': result.Append('\0'); break;
                        case 'a': result.Append('\a'); break;
                        case 'b': result.Append('\b'); break;
                        case 'f': result.Append('\f'); break;
                        case 'n': result.Append('\n'); break;
                        case 'r': result.Append('\r'); break;
                        case 't': result.Append('\t'); break;
                        case 'v': result.Append('\v'); break;
                        case 'u': 
                            var charValTxt = literal.Substring(i + 1, 4);
                            i += 4;
                            if (ushort.TryParse(charValTxt, NumberStyles.HexNumber, CultureInfo.InvariantCulture,
                                out var charVal)) {
                                result.Append((char) charVal);
                                break;
                            }

                            throw new Exception("Unexpected escape sequence in literal");
                        default:
                            throw new Exception("Unexpected escape sequence in literal");
                    }

                    continue;
                }

                if (c == '\\') {
                    inEscape = true;
                    continue;
                }

                result.Append(c);
            }

            return result.ToString();
        }


        public static Slice GetSlice(this string s, int index = 0, int length = -1) =>
            new Slice {OriginalString = s, StartIndex = index, LengthOfSubstring = length};

        #endregion

        #endregion
    }

    public class Slice
    {
        public static readonly Slice Empty = new Slice {OriginalString = "", StartIndex = 0, LengthOfSubstring = 0};
        public int LengthOfSubstring; // the length of the substring of the original string
        public string OriginalString;
        public int StartIndex;

        public char this[int i] => OriginalString[i + StartIndex];

        public int Length
        {
            get => LengthOfSubstring < 0 ? OriginalString.Length - StartIndex : Math.Min(LengthOfSubstring, OriginalString.Length - StartIndex);
            set => LengthOfSubstring = value;
        }

        public override string ToString() => LengthOfSubstring < 0 && StartIndex == 0 ? OriginalString :
            LengthOfSubstring > 0 ? OriginalString.Substring(StartIndex, LengthOfSubstring) :
            LengthOfSubstring < 0 ? OriginalString.Substring(StartIndex) : "";

        public static implicit operator string(Slice ss) => ss.ToString();

        public static implicit operator Slice(string originalString) => new Slice
            {OriginalString = originalString, StartIndex = 0, LengthOfSubstring = originalString.Length};

        public int IndexOf(char c)
        {
            var idx = OriginalString.IndexOf(c, StartIndex) - StartIndex;
            if (idx >= Length) return -1;
            return Math.Max(-1, idx);
        }

        public int IndexOf(char c, int startIndex)
        {
            var idx = OriginalString.IndexOf(c, StartIndex + startIndex) - StartIndex;
            if (idx >= Length) return -1;
            return Math.Max(-1, idx);
        }

        public Slice Substring(int index, int length) => new Slice
            {OriginalString = OriginalString, StartIndex = StartIndex + index, LengthOfSubstring = length};

        public Slice Substring(int index) => new Slice {
            OriginalString = OriginalString, StartIndex = StartIndex + index,
            LengthOfSubstring = Length - index - StartIndex
        };

        public Slice Trim(params char[] p)
        {
            var pStr = new string(p);
            var r = new Slice {OriginalString = OriginalString, StartIndex = StartIndex, LengthOfSubstring = Length};
            while (r.Length > 0 && pStr.IndexOf(r[r.Length - 1]) >= 0)
                r.Length--;
            while (r.Length > 0 && r.StartIndex < r.OriginalString.Length && pStr.IndexOf(r[0]) >= 0) {
                r.StartIndex++;
                r.Length--;
            }

            return r;
        }

        public static bool operator ==(Slice obj1, Slice obj2) =>
            ReferenceEquals(obj1, null) && ReferenceEquals(obj2, null) ||
            !ReferenceEquals(obj1, null) && !ReferenceEquals(obj2, null) &&
            obj1.Length == obj2.Length && (string) obj1 == (string) obj2;

        public static bool operator !=(Slice obj1, Slice obj2) => !(obj1 == obj2);
        public override int GetHashCode() => ((string) this).GetHashCode();
        public override bool Equals(object obj) => ((string) this).Equals(obj);

        public bool StartsWith(string s)
        {
            if (s.Length > Length) return false;
            for (var i = 0; i < s.Length; i++)
                if (this[i] != s[i])
                    return false;
            return true;
        }

        public bool StartsWith(char c) => this[0] == c;

        public int LastIndexOf(char c)
        {
            for (var i = Length - 1; i >= 0; i--)
                if (this[i] == c)
                    return i;
            return -1;
        }

        public string Unescape(bool stripQuotes = true, char quoteChar = '\"')
        {
            var result = new StringBuilder(Length);
            var startIndex = 0;
            var endIndex = Length;
            if (stripQuotes && this[0] == quoteChar) {
                startIndex++;
                if (this[Length - 1] == quoteChar) endIndex--;
            }

            var inEscape = false;
            for (var i = startIndex; i < endIndex; i++) {
                var c = this[i];
                if (inEscape) {
                    inEscape = false;
                    switch (c) {
                        case '\'': result.Append('\''); break;
                        case '\"': result.Append('\"'); break;
                        case '\\': result.Append('\\'); break;
                        case '0': result.Append('\0'); break;
                        case 'a': result.Append('\a'); break;
                        case 'b': result.Append('\b'); break;
                        case 'f': result.Append('\f'); break;
                        case 'n': result.Append('\n'); break;
                        case 'r': result.Append('\r'); break;
                        case 't': result.Append('\t'); break;
                        case 'v': result.Append('\v'); break;
                        case 'u':
                            var charValTxt = Substring(i + 1, 4);
                            i += 4;
                            if (ushort.TryParse(charValTxt, NumberStyles.HexNumber, CultureInfo.InvariantCulture,
                                out var charVal)) {
                                result.Append((char) charVal);
                                break;
                            }

                            throw new Exception("Unexpected escape sequence in literal");
                        default:
                            throw new Exception("Unexpected escape sequence in literal");
                    }

                    continue;
                }

                if (c == '\\') {
                    inEscape = true;
                    continue;
                }

                result.Append(c);
            }

            return result.ToString();
        }

        public char GetNextNonWhiteChar(out int index, int startAtIndex = 0)
        {
            var l = Length;
            for (int i = startAtIndex; i < l; i++) {
                var c = this[i];
                if (!char.IsWhiteSpace(c) && c != '\t' && c != '\r' && c != '\n') {
                    index = i;
                    return c;
                }
            }

            index = -1;
            return '\0';
        }

        public char FindChar(string possibleChars, out int index, int startAtIndex = 0)
        {
            var l = Length;
            for (int i = startAtIndex; i < l; i++) {
                var c = this[i];
                if (possibleChars.IndexOf(c)>=0) {
                    index = i;
                    return c;
                }
            }

            index = -1;
            return '\0';
        }
    }
    #region QnConfig
    public class QnConfig
    {
        public static readonly QnConfig Default = new QnConfig();

        public static readonly QnConfig Json = new QnConfig {
            Name = "Json",
            OpenRecord = '{',
            CloseRecord = '}',
            OpenDictionary = "{",
            CloseDictionary = '}',
            FieldNameInQuotes = true,
            SupportEitherQuoteChar = true,
            UseDateFormatter = true,
            DateFormat = "yyyy-MM-ddTHH:mm:ss.fffZ",
            ExceptionAsStringEncodedXml = false,
            ExceptionStringFormat = "{'ExceptionMessage':'{0}'}",
            NullStr = "null",
            EncodeStringAsDoubleQuote = false,
            BooleanAsLowecase = true,
            SupportLegacyStringArrayWithPipe = false
        };
        // CSharpObjectInit not supported for parsing because can not at this point parse configurations with open sections parts larger than 1 character
        public static readonly QnConfig CSharpObjectInit = new QnConfig {
            Name = "C# object init",
            OpenRecord = '{',
            CloseRecord = '}',
            OpenDictionary = "new Dictionary<{0},{1}>{{",
            DictionaryItemClose = "}",
            DictionaryItemOpen = "{",
            CloseDictionary = '}',
            OpenArray = "new[]{",
            CloseArray = ']',
            FieldSep = '=',
            DictionarySep = ',',
            FieldNameInQuotes = false,
            SupportEitherQuoteChar = false,
            ExceptionAsStringEncodedXml = false,
            NullStr = "null",
            EncodeStringAsDoubleQuote = false,
            BooleanAsLowecase = true,
            SupportLegacyStringArrayWithPipe = false,
            AddClassName = true,
            AddNewKeywordToClassName = true,
            DateFormat = "g",
            UseDateFormatter = true,
            // the DateTime is also preventing parsing through the QN mechanism
            DateStringFormat = "DateTime.ParseExact(\"{0}\",\"g\",CultureInfo.InvariantCulture)"
        };
        public string Name = "QN";
        public override string ToString() => Name;
        // defaults and decelerations
        public bool BooleanAsLowecase;
        public string DateStringFormat;
        public string DateFormat;
        public char DictionarySep = ':';
        public bool EncodeStringAsDoubleQuote = true;
        public bool ExceptionAsStringEncodedXml = true;
        public string ExceptionStringFormat;
        public bool FieldNameInQuotes;
        public char FieldSep = ':';
        public int FieldsLimit = 50;
        public int MaxDepth = 20;
        public string NullStr = "";
        public string OpenArray = "[";
        public char CloseArray = ']';
        public string OpenDictionary = "[";
        public char CloseDictionary = ']';
        public char OpenRecord = '(';
        public char CloseRecord = ')';
        public char Quote = '\"';
        public bool SupportEitherQuoteChar;
        public bool SupportLegacyStringArrayWithPipe = true;
        public bool UseDateFormatter;
        public bool AddClassName;
        public bool AddNewKeywordToClassName;
        public string DictionaryItemOpen = "";
        public string DictionaryItemClose = "";
    }
    #endregion
    /// <summary>
    ///     When the type is unknown or object allow late parsing
    /// </summary>
    public class QnObject
    {
        public QnConfig Config;
        public Slice RawText;
        QnObject[] _arr;
        Dictionary<string, QnObject> _dic;
        public override string ToString() => RawText.ToString();
        public T Parse<T>() => SerEx.FromQn<T>(RawText, Config);
        public bool IsArray => RawText.GetNextNonWhiteChar(out _) == Config.OpenArray[0];
        public QnObject[] ParseArray() => _arr ?? (_arr= IsArray ? Parse<QnObject[]>():new QnObject[0]);
        public bool IsClass
        {
            get
            {
                var c = RawText.GetNextNonWhiteChar(out var i0);
                if (c != Config.OpenRecord) return false;
                c = RawText.GetNextNonWhiteChar(out var i1, i0+1);
                if (c == Config.Quote && !Config.FieldNameInQuotes || Config.FieldNameInQuotes && c!=Config.Quote) return false;
                if (c == Config.Quote)
                    i1 = RawText.IndexOf(Config.Quote, i1 + 1);
                if (i1 < 0) return false;
                var trySep = RawText.FindChar($"{Config.FieldSep},", out _, i1 + 1);
                return trySep == Config.FieldSep;
            }
        }
        public bool IsString => RawText.GetNextNonWhiteChar(out _) == Config.Quote;
        public Dictionary<string, QnObject> ParseClass() => _dic ?? (_dic = IsClass?Parse<Dictionary<string, QnObject>>():new Dictionary<string, QnObject>());
        public bool IsNumber => char.IsDigit(RawText.GetNextNonWhiteChar(out _));
        public QnObject this[string key] =>ParseClass()[key];

        public QnObject this[int index]
        {
            get {
                if (index<0) return new QnObject { Config = Config, RawText=""};
                var a = ParseArray();
                if (index>=a.Length) return new QnObject { Config = Config, RawText = "" };
                return a[index];
            }
        }

        /*
        public bool IsDictionary
        {
            get
            {
                var c = RawText.GetNextNonWhiteChar(out var i0);
                if (c != Config.OpenDictionary[0]) return false;
                if (c == Config.OpenRecord || c == Config.OpenArray[0] || c == Config.OpenDictionary[0]) {
                    var h = new ParseHelper(RawText);
                    
                }
                throw new NotImplementedException();
            }
        }*/
    }

#region ParseHelper

    class ParseHelper
    {
        public ParseHelper(Slice str)
        {
            AllText = str;
        }

        public Slice AllText { get; }

        public char Current
        {
            get
            {
                if (HasEnded) return (char) 0;
                return AllText[CurrentIndex];
            }
        }

        public int CurrentIndex { get; set; }
        public bool HasEndedOrSquigglyBrackenEnd => CurrentIndex < 0 || CurrentIndex >= AllText.Length || AllText[CurrentIndex] == '}';
        public bool HasEnded => CurrentIndex < 0 || CurrentIndex >= AllText.Length;

        public void SkipOne()
        {
            CurrentIndex++;
        }

        bool skipTo(char c)
        {
            var nextC = AllText.IndexOf(c, CurrentIndex);
            if (nextC < 0)
                return false;
            CurrentIndex = nextC;
            return true;
        }

        // ReSharper disable once UnusedMember.Local
        public void SkipToAny(string characters)
        {
            //int oldI = iMsg;
            while (!HasEnded && characters.IndexOf(AllText[CurrentIndex]) < 0)
                CurrentIndex++;
        }

        // ReSharper disable once UnusedMember.Local
        public bool SkipPass(char c)
        {
            if (!skipTo(c)) return false;
            CurrentIndex++;
            return true;
        }

        Slice readTo(char c)
        {
            var nextC = AllText.IndexOf(c, CurrentIndex);
            if (nextC < 0)
                return null;
            var result = AllText.Substring(CurrentIndex, nextC - CurrentIndex);
            CurrentIndex = nextC;
            return result;
        }

        public Slice ReadToAny(string characters)
        {
            var nextC = CurrentIndex;
            while (nextC < AllText.Length && characters.IndexOf(AllText[nextC]) < 0)
                nextC++;
            if (nextC == AllText.Length)
                return null;
            var l = nextC - CurrentIndex;
            var result = l > 0 ? AllText.Substring(CurrentIndex, nextC - CurrentIndex) : Slice.Empty;
            CurrentIndex = nextC;
            return result;
        }

        public Slice ReadToAndSkip(char c)
        {
            var result = readTo(c);
            if (result == null)
                return null;
            CurrentIndex++;
            return result;
        }

        public void SkipWhiteSpaces()
        {
            while (IsCurrentWhiteSpace())
                CurrentIndex++;
        }

        // ReSharper disable once UnusedMember.Local
        public int IndexOfNext(char c)
        {
            if (HasEnded)
                return -1;
            return AllText.IndexOf(c, CurrentIndex);
        }

        public char NextNonWhiteSpace(out int indexOfNextNonWhiteSpace)
        {
            indexOfNextNonWhiteSpace = -1;
            if (HasEnded) return ' ';
            var atI = CurrentIndex;
            while (atI < AllText.Length && IsWhiteSpace(AllText[atI]))
                atI++;
            if (atI >= AllText.Length)
                return ' ';
            indexOfNextNonWhiteSpace = atI;
            return AllText[atI];
        }

        public static bool IsWhiteSpace(char c) =>
            char.IsWhiteSpace(c) || c == '\r' || c == '\n' ||
            c == '\t';

        // ReSharper disable once MemberCanBePrivate.Local
        public bool IsCurrentWhiteSpace()
        {
            if (HasEnded) return false;
            return IsWhiteSpace(AllText[CurrentIndex]);
        }

        public void SkipToIndex(int nextIndex)
        {
            if (nextIndex < 0) return;
            if (nextIndex >= AllText.Length)
                nextIndex = AllText.Length;
            if (nextIndex > CurrentIndex)
                CurrentIndex = nextIndex;
        }

        public bool PeekPhrase(string phrase) => AllText.Substring(CurrentIndex, phrase.Length) == phrase;

        public void SkipPhrase(string phrase) => CurrentIndex += phrase.Length;
    }

#endregion
}