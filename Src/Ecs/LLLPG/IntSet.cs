﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Loyc.Collections.Impl;
using System.Diagnostics;
using Loyc.Utilities;
using Loyc.Collections;

namespace Loyc.LLParserGenerator
{
	/// <summary>Represents a set of characters (e.g. 'A'..'Z' | 'a'..'z' | '_'), 
	/// or a set of token IDs.</summary>
	public class IntSet : ICloneable<IntSet>, IListSource<IntRange>, IEquatable<IntSet>
	{
		/// <summary>A list of non-overlapping character ranges, sorted by code 
		/// point. EOF can be included in this list as character -1 (hence CharRange 
		/// holds ints instead of chars).</summary>
		protected InternalList<IntRange> _ranges = InternalList<IntRange>.Empty;
		
		/// <summary>When Inverted is true, the set behaves as if it contains the
		/// opposite set of items. That is, membership tests that succeeded when
		/// Inverted was false will fail, and vice versa.</summary>
		public bool Inverted { get; set; }

		/// <summary>Controls the default stringization mode. When IsCharSet, the
		/// set "(36, 65..90, 126)" prints as "[$A-Z~]". IsCharSet is false by default.
		/// </summary>
		public bool IsCharSet = false;

		public static implicit operator IntSet(int c) { return new IntSet(new IntRange(c)); }
		public static implicit operator IntSet(IntRange r) { return new IntSet(r); }

		public static IntSet With(params int[] members)             { return new IntSet(false, false, false, members); }
		public static IntSet WithRanges(params int[] ranges)        { return new IntSet(false, false, true, ranges); }
		public static IntSet Without(params int[] members)          { return new IntSet(false, true, false, members); }
		public static IntSet WithoutRanges(params int[] ranges)     { return new IntSet(false, true, true, ranges); }
		public static IntSet WithChars(params int[] members)        { return new IntSet(true, false, false, members); }
		public static IntSet WithCharRanges(params int[] ranges)    { return new IntSet(true, false, true, ranges); }
		public static IntSet WithoutChars(params int[] members)     { return new IntSet(true, true, false, members); }
		public static IntSet WithoutCharRanges(params int[] ranges) { return new IntSet(true, true, true, ranges); }
		public static IntSet Empty() { return new IntSet(false, false); }
		public static IntSet All() { return new IntSet(false, true); }
		
		public static IntSet Parse(string members)
		{
			int errorIndex;
			var r = TryParse(members, new IntSet(), out errorIndex);
			if (r == null)
				throw new FormatException(string.Format(
					"Input string could not be parsed to an IntSet (error at index {0})", errorIndex));
			return r;
		}
		public static IntSet TryParse(string members)
		{
			int _;
			return TryParse(members, new IntSet(), out _);
		}
		protected internal static IntSet TryParse(string s, IntSet set, out int errorIndex)
		{
			bool success = false;
			int i = 1;
			if (s.StartsWith("[") && s.EndsWith("]"))
			{
				set.IsCharSet = true;
				if (set.Inverted = s[1] == '^')
					i++;

				while (i + 1 < s.Length)
				{
					int lo = ParseChar(s, ref i);
					if (s[i] == '-') {
						i++;
						int hi = ParseChar(s, ref i);
						set._ranges.Add(new IntRange(lo, hi));
					} else {
						set._ranges.Add(new IntRange(lo));
					}
				}
				success = true;
			}
			else if (s.EndsWith(")") && (s.StartsWith("(") || (set.Inverted = s.StartsWith("~("))))
			{
				set.IsCharSet = false;
				if (set.Inverted)
					i++;

				for(;;) {
					int lo, hi;
					if (!G.TryParseAt(s, ref i, out lo)) {
						if (i + 1 == s.Length && lo == 0)
							success = true;
						break;
					}
					hi = lo;
					if (s[i] == '.' && s[i + 1] == '.') {
						i += 2;
						if (!G.TryParseAt(s, ref i, out hi)) break;
					}
					if (s[i] == ',')
						i++;
					else if (s[i] != ')')
						break;
					set._ranges.Add(new IntRange(lo, hi));
				}
			}
			if (success) {
				errorIndex = -1;
				set.AutoSimplify();
				return set;
			} else {
				errorIndex = i;
				return null;
			}
		}
		private static int ParseChar(string s, ref int i) // used by TryParse
		{
			int oldi = i;
			char c = G.UnescapeChar(s, ref i);
			if (c == '\\' && i == oldi+1) {
				c = s[i++];
				if (c == '$')
					return -1; // \$ is -1 is EOF
			}
			return c;
		}

		public IntSet(bool isCharSet = false, bool inverted = false)
		{
			IsCharSet = isCharSet;
			Inverted = inverted; 
		}
		public IntSet(IntRange r, bool isCharSet = false, bool inverted = false)
		{
			IsCharSet = isCharSet;
			Inverted = inverted; 
			_ranges.Add(r);
		}
		public IntSet(bool isCharSet, bool inverted, params IntRange[] list)
		{
			_ranges = new InternalList<IntRange>(list, list.Length);
			AutoSimplify();
		}
		protected IntSet(bool isCharSet, bool inverted, bool ranges, params int[] list)
		{
			IsCharSet = isCharSet;
			Inverted = inverted;
			if (ranges) {
				_ranges = new InternalList<IntRange>(list.Length >> 1);
				for (int i = 0; i < list.Length; i += 2)
					_ranges.Add(new IntRange(list[i], list[i + 1]));
			} else {
				_ranges = new InternalList<IntRange>(list.Length);
				for (int i = 0; i < list.Length; i++)
					_ranges.Add(new IntRange(list[i]));
			}
			AutoSimplify();
		}
		private void AutoSimplify()
		{
			for (int i = 1; i < _ranges.Count; i++)
				if (!(_ranges[i - 1] < _ranges[i]) || _ranges[i - 1].CanMerge(_ranges[i])) {
					Simplify();
					return;
				}
		}
		private void Simplify()
		{
			if (_ranges.Count < 2)
				return;

			// First sort, then merge where possible.
			var rs = _ranges;
			rs.Sort(IntRange.CompareLo);
			var current = rs[0];
			int to = 0;
			for (int i = 1; i < rs.Count; i++)
				if (current.CanMerge(rs[i]))
					current = current.Merged(rs[i]);
				else {
					rs[to++] = current;
					current = rs[i];
				}
			rs[to++] = current;
			rs.Resize(to);
			_ranges = rs;
		}

		/// <summary>Returns true iff the set is empty. When the set is inverted 
		/// and contains the set of all integers, that also counts as empty.</summary>
		public bool IsEmptySet
		{
			get { return IsEmptyOrFull(false); }
		}
		/// <summary>Returns true iff the set covers all integers. This includes 
		/// the common scenario that the set is empty but inverted.</summary>
		public bool ContainsEverything
		{
			get { return IsEmptyOrFull(true); }
		}
		bool IsEmptyOrFull(bool full)
		{
			return Inverted ^ full ? (_ranges.Count == 1 && _ranges[0].Lo <= int.MinValue && _ranges[0].Hi >= int.MaxValue) : _ranges.Count == 0;
		}
		

		public bool Contains(int ch)
		{
			for (int i = 0; i < _ranges.Count; i++)
				if (_ranges[i].Contains(ch))
					return !Inverted;
				else if (_ranges[i].Lo > ch)
					break;
			return Inverted;
		}

		protected virtual IntSet New(bool isCharSet, bool inverted, InternalList<IntRange> ranges)
		{
			return new IntSet(isCharSet, inverted) { _ranges = ranges };
		}

		public IntSet Union(IntSet r, bool cloneWhenOneIsEmpty = false)
		{
			// Union of inverted sets is accomplished via intersection code: ~a | ~b => ~(a & b)
			IntSet l = this;
			if (l.Inverted || r.Inverted)
			{
				if (!l.Inverted) l = l.EquivalentInverted();
				if (!r.Inverted) r = l.EquivalentInverted();
				return New(IsCharSet, true, IntersectCore(l, r));
			}
			else
			{
				if (cloneWhenOneIsEmpty) {
					if (l._ranges.Count == 0)
						return r.Clone();
					if (r._ranges.Count == 0)
						return l.Clone();
				}
				return New(IsCharSet, false, UnionCore(l, r));
			}
		}
		public IntSet Intersection(IntSet r, bool subtract = false, bool invertThis = false)
		{
			IntSet l = this, oldr = r;
			bool lInv = l.Inverted ^ invertThis, rInv = r.Inverted ^ subtract;
			if (lInv && rInv)
			{
				IntSet cl = null;
				if (l._ranges.Count == 0)
				    cl = r.Clone();
				if (r._ranges.Count == 0)
					cl = l.Clone();
				if (cl != null) {
					cl.Inverted = true;
					return cl;
				} else
					return New(IsCharSet, true, UnionCore(l, r));
			}
			else
			{
				if (lInv) l = l.EquivalentInverted();
				if (rInv) r = r.EquivalentInverted();
				return New(IsCharSet, false, IntersectCore(l, r));
			}
		}
		public IntSet Subtract(IntSet other)
		{
			return Intersection(other, true);
		}
		
		static InternalList<IntRange> UnionCore(IntSet l, IntSet r)
		{
			var e0 = l._ranges.GetEnumerator();
			if (!e0.MoveNext())
				return r._ranges;
			var e1 = r._ranges.GetEnumerator();
			if (!e1.MoveNext())
				return l._ranges;
			
			var result = new InternalList<IntRange>(l._ranges.Count + r._ranges.Count);
			while (e0 != null && e1 != null)
			{
				var r0 = e0.Current;
				var r1 = e1.Current;
				if (r0 < r1)
					e0 = AddAndMoveNext(ref result, r0, e0);
				else
					e1 = AddAndMoveNext(ref result, r1, e1);
			}

			if (e0 != null) do
				e0 = AddAndMoveNext(ref result, e0.Current, e0);
			while (e0 != null);
			
			if (e1 != null) do
				e1 = AddAndMoveNext(ref result, e1.Current, e1);
			while (e1 != null);

			return result;
		}
		private static IEnumerator<IntRange> AddAndMoveNext(ref InternalList<IntRange> result, IntRange r, IEnumerator<IntRange> e)
		{
			IntRange last;
			if (result.Count > 0 && (last = result.Last).CanMerge(r))
				result.Last = last.Merged(r);
			else
				result.Add(r);
			return e.MoveNext() ? e : null;
		}

		static InternalList<IntRange> IntersectCore(IntSet l, IntSet r)
		{
			var result = InternalList<IntRange>.Empty;
			var e0 = l._ranges.GetEnumerator();
			var e1 = r._ranges.GetEnumerator();
			if (e0.MoveNext() && e1.MoveNext())
				for (;;)
				{
					var r0 = e0.Current;
					var r1 = e1.Current;
					if (r0.Overlaps(r1))
						result.Add(r0.Intersection(r1));

					if (r0.Hi < r1.Hi) {
						if (!e0.MoveNext()) break;
					} else {
						if (!e1.MoveNext()) break;
					}
				}
			return result;
		}

		/// <summary>Computes the equivalent inverted set, e.g. if the set is 
		/// <c>'b'..'y'</c>, the equivalent inverted set is 
		/// <c>~(int.MinValue..'a' | 'z'..int.MaxValue)</c>.</summary>
		protected internal IntSet EquivalentInverted()
		{
			if (_ranges.Count == 0)
				return new IntSet(new IntRange(int.MinValue, int.MaxValue), IsCharSet, !Inverted);

			var result = new IntSet(IsCharSet, !Inverted) { _ranges = new InternalList<IntRange>(_ranges.Count + 1) };
			
			int lowest = _ranges[0].Lo, highest = _ranges[_ranges.Count-1].Hi;
			if (lowest > int.MinValue)
				result._ranges.Add(new IntRange(int.MinValue, _ranges[0].Lo - 1));
			for (int i = 1; i < _ranges.Count; i++) {
				var r = new IntRange(_ranges[i - 1].Hi + 1, _ranges[i].Lo - 1);
				Debug.Assert(r.Lo <= r.Hi);
				result._ranges.Add(r);
			}
			if (highest < int.MaxValue)
				result._ranges.Add(new IntRange(highest + 1, int.MaxValue));

			return result;
		}

		public IntSet Clone()
		{
			return New(IsCharSet, Inverted, _ranges.CloneAndTrim());
		}

		/// <summary>Prints the character set using regex syntax, e.g. [\$a-z] 
		/// means "EOF or a to z", [^\n\r] means "not \n or \r". Use 
		/// ToString(false) if this is an integer set.</summary>
		public override string ToString()
		{
			return ToString(IsCharSet);
		}
		public string ToString(bool charSet)
		{
			StringBuilder sb;
			if (charSet)
			{
				sb = new StringBuilder(Inverted ? "[^" : "[");
				for (int i = 0; i < _ranges.Count; i++) {
					var r = _ranges[i];
					if (!r.CanPrintAsCharRange)
						goto intSet; // oops, invalid character range
					r.AppendTo(sb, true);
				}
				sb.Append(']');
				return sb.ToString();
			}
		intSet:
			sb = new StringBuilder(Inverted ? "~(" : "(");
			for (int i = 0; i < _ranges.Count; i++) {
				if (i != 0)
					sb.Append(", ");
				_ranges[i].AppendTo(sb, false);
			}
			sb.Append(')');
			return sb.ToString();
		}

		/// <summary>Gets the number of integers whose membership test would 
		/// succeed (the maximum possible value is 0x100000000L).</summary>
		public long Size
		{
			get {
				long size = SizeIgnoringInversion;
				return Inverted ? 0x100000000L - size : size;
			}
		}
		protected long SizeIgnoringInversion
		{
			get {
				long size = 0;
				for (int i = 0; i < _ranges.Count; i++)
					size += (long)_ranges[i].Hi - _ranges[i].Lo + 1;
				return size;
			}
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { return GetEnumerator(false); }
		public IEnumerator<IntRange> GetEnumerator() { return GetEnumerator(false); }
		public IEnumerator<IntRange> GetEnumerator(bool obeyInversion)
		{
			if (obeyInversion && Inverted)
				return EquivalentInverted().GetEnumerator();
			else
				return _ranges.GetEnumerator();
		}
		public IntRange this[int index]
		{
			get { return _ranges[index]; }
		}
		public IntRange TryGet(int index, ref bool fail)
		{
			return _ranges.TryGet(index, ref fail);
		}
		public Iterator<IntRange> GetIterator()
		{
			return _ranges.GetIterator();
		}
		public int Count
		{
			get { return _ranges.Count; }
		}

		public override bool Equals(object obj)
		{
			var r = obj as IntSet;
			return r != null && Equals(r, S_Equivalent);
		}
		public bool Equals(IntSet other) { return Equals(other, S_Equivalent); }

		public override int GetHashCode()
		{
			int hc = (Inverted ? -1 : 0) ^ _ranges.Count;
			for (int i = 0; i < _ranges.Count; i++)
				hc = (hc * 13) ^ _ranges[i].GetHashCode();
			return hc;
		}
		
		public static readonly Symbol S_Equivalent = GSymbol.Get("Equivalent");
		public static readonly Symbol S_SameRangeList = GSymbol.Get("SameRangeList");
		public static readonly Symbol S_Identical = GSymbol.Get("Identical");
		public bool Equals(IntSet other, Symbol mode)
		{
			if (Inverted == other.Inverted)
				return _ranges.AllEqual(other._ranges);
			
			if (mode == S_Equivalent) {
				int dif = _ranges.Count - other._ranges.Count;
				return dif >= -1 && dif <= 1 && _ranges.AllEqual(other.EquivalentInverted()._ranges);
			} else
				return mode == S_SameRangeList && _ranges.AllEqual(other._ranges);
		}
	}

	/// <summary>Represents a range of single characters (e.g. 'A'..'Z').</summary>
	public struct IntRange : IComparable<IntRange>, IEquatable<IntRange>
	{
		public IntRange(int c) { Lo = Hi = c; }
		public IntRange(int lo, int hi) {
			Lo = lo; Hi = hi;
			if (lo > hi)
				throw new ArgumentException(Localize.From("Character range Lo > Hi: '{0}' > '{1}'", lo, hi));
		}

		public int Lo, Hi;

		public override bool Equals(object obj)
		{
			return obj is IntRange && Equals((IntRange)obj);
		}
		public bool Equals(IntRange other)
		{
			return Lo == other.Lo && Hi == other.Hi;
		}
		public override int GetHashCode()
		{
			return (Lo << 1) + Hi;
		}

		public bool Contains(int ch)
		{
			return ch >= Lo && ch <= Hi;
		}
		public bool Overlaps(IntRange r)
		{
			Debug.Assert(Lo <= Hi && r.Lo <= r.Hi);
			if (Lo <= r.Lo)
				return Hi >= r.Lo;
			else // r.Lo < Lo
				return r.Hi >= Lo;
		}
		public bool CanMerge(IntRange r)
		{
			Debug.Assert(Lo <= Hi && r.Lo <= r.Hi);
			if (Lo <= r.Lo)
				return Hi >= r.Lo - 1;
			else // r.Lo < Lo
				return r.Hi >= Lo - 1;
		}
		public IntRange Merged(IntRange r)
		{
			if (Lo <= r.Lo)
				return new IntRange { Lo = Lo, Hi = (Hi > r.Hi ? Hi : r.Hi) };
			else
				return r.Merged(this);
		}
		/// <summary>Compares only the <see cref="Lo"/> values of two ranges.</summary>
		public int CompareTo(IntRange other)
		{
			return ((int)Lo).CompareTo(((int)other.Lo));
		}
		public static readonly Comparison<IntRange> CompareLo = (a, b) => a.Lo.CompareTo(b.Lo);

		public static bool operator >(IntRange a, IntRange b) { return a.Lo > b.Lo; }
		public static bool operator <(IntRange a, IntRange b) { return a.Lo < b.Lo; }
		public static bool operator ==(IntRange a, IntRange b) { return a.Equals(b); }
		public static bool operator !=(IntRange a, IntRange b) { return !a.Equals(b); }
		
		public override string ToString()
		{
			var sb = new StringBuilder("(", 8);
			AppendTo(sb, false);
			sb.Append(')');
			return sb.ToString();
		}
		public void AppendTo(StringBuilder sb, bool asCharRange)
		{
			if (asCharRange) {
				if (Lo == Hi)
					Append(sb, Lo);
				else {
					Append(sb, Lo);
					sb.Append("-");
					Append(sb, Hi);
				}
			} else {
				if (Lo == Hi)
					sb.Append(Lo);
				else {
					sb.Append(Lo);
					sb.Append("..");
					sb.Append(Hi);
				}
			}
		}
		private static void Append(StringBuilder sb, int c)
		{
			if (c < 32 || c == '\\' || c == ']') {
				if (c <= -1)
					sb.Append(@"\$");
				else 
					sb.Append(G.EscapeCStyle(((char)c).ToString(), EscapeC.Control | EscapeC.ABFV, ']'));
			} else if (c == '-' || c == '^' && sb.Length == 1) {
				sb.Append('\\');
				sb.Append((char)c);
			} else
				sb.Append((char)c);
		}

		internal IntRange Intersection(IntRange r)
		{
			return new IntRange(System.Math.Max(Lo, r.Lo), System.Math.Min(Hi, r.Hi));
		}

		public bool CanPrintAsCharRange
		{
			get { return (Lo >= -1 && Hi < 0xD800) || (Lo > 0xDFFF && Hi < 0xFFFE); }
		}
	}
}
