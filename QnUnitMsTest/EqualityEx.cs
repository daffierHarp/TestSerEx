//  EqualityEx.cs Latest resharper cleanup: 07/10/2020
//  Copyright © FAAC. All rights reserved.

using System;
using System.Collections;
using System.Diagnostics;

namespace QnUnitMsTest
{
    static class EqualityEx
    {
        public static bool Eq(this DateTime d1, DateTime d2, bool withMilis = false, bool withSeconds = false)
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
        // test public properties/fields for equality
        public static bool EqVals(this object a, object b, bool sameType =true, bool nullEqEmptyLists = false, bool matchLineEnds = false)
        {
            if (ReferenceEquals(a, b)) return true;
            if (nullEqEmptyLists) {
                var aL = a as IList;
                var bL = b as IList;
                if (aL != null || bL != null) {
                    int aC = aL?.Count ??0;
                    int bC = bL?.Count ??0;
                    if (aC == 0 && bC == 0) return true;
                }
            }
            if (a == null || b == null) return false;
            var at = a.GetType();
            var bt = b.GetType();
            if (sameType && at != bt) return false;
            if (at.IsPrimitive || at.IsEnum) return Equals(a, b);
            if (a is string strA && b is string strB) {
                if (string.Equals(strA, strB))
                    return true;
                if (!matchLineEnds) return false;
                if (strA.IndexOf("\r\n", StringComparison.Ordinal)>=0 && strB.IndexOf("\r\n", StringComparison.Ordinal)<0) {
                    strB = strB.Replace("\n", "\r\n");
                }
                if (strB.IndexOf("\r\n", StringComparison.Ordinal)>=0 && strA.IndexOf("\r\n", StringComparison.Ordinal)<0) {
                    strA = strA.Replace("\n", "\r\n");
                }
                return string.Equals(strA, strB);
            }
            if (a is DateTime dA && b is DateTime dB)
                return dA.Eq(dB);
            // list/array/dic
            if (a is IDictionary aDic) {
                if (!(b is IDictionary bDic))
                    return false;
                var aE = aDic.GetEnumerator();
                while (aE.MoveNext()) {
                    if (!bDic.Contains(aE.Key)) return false;
                    if (!aE.Value.EqVals(bDic[aE.Key], nullEqEmptyLists:nullEqEmptyLists, matchLineEnds: matchLineEnds)) return false;
                }

                return true;
            }
            if (a is IEnumerable aEn) {
                if (!(b is IEnumerable bEn))
                    return false;
                var aE = aEn.GetEnumerator();
                var bE = bEn.GetEnumerator();
                bool aMv = true, bMv = true;
                while (true) {
                    aMv = aE.MoveNext();
                    bMv = bE.MoveNext();
                    if (!aMv || !bMv) break;
                    if (!aE.Current.EqVals(bE.Current, nullEqEmptyLists:nullEqEmptyLists, matchLineEnds: matchLineEnds)) return false;
                }
                return aMv == bMv;
            }
            // class
            foreach (var p in at.GetProperties()) {
                var apv = p.GetValue(a);
                object bpv = null;
                if (sameType)
                    bpv = p.GetValue(b);
                else {
                    var bp = bt.GetProperty(p.Name);
                    if (bp == null) continue;
                    bpv = bp.GetValue(b);
                }

                if (!apv.EqVals(bpv, nullEqEmptyLists: nullEqEmptyLists, matchLineEnds: matchLineEnds)) {
                    Debug.WriteLine(p.Name + " not equal");
                    return false;
                }
            }
            foreach (var f in at.GetFields()) {
                var afv = f.GetValue(a);
                object bfv = null;
                if (sameType)
                    bfv = f.GetValue(b);
                else {
                    var bf = bt.GetField(f.Name);
                    if (bf == null) continue;
                    bfv = bf.GetValue(b);
                }

                if (!afv.EqVals(bfv, nullEqEmptyLists: nullEqEmptyLists, matchLineEnds: matchLineEnds)) {
                    Debug.WriteLine(f.Name + " not equal");
                    return false;
                }
            }

            return true;
        }
    }
}