using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace Loyc.Essentials
{
	public static partial class StringExt
	{
		public static bool SplitAt(this string s, char c, out string s1, out string s2)
		{
			int i = s.IndexOf(c);
			if (i == -1) {
				s1 = s; s2 = null;
				return false;
			} else {
				s1 = s.Substring(0, i);
				s2 = s.Substring(i + 1);
				return true;
			}
		}
		public static string Right(this string s, int count)
		{
			if (count >= s.Length)
				return s;
			else
				return s.Substring(s.Length - count);
		}
		public static string Left(this string s, int count)
		{
			if (count >= s.Length)
				return s;
			else
				return s.Substring(0, count);
		}
		
		public static string Join(string separator, IEnumerable value) { return Join(separator, value.GetEnumerator()); }
		public static string Join(string separator, IEnumerator value) 
		{
			if (!value.MoveNext())
				return string.Empty;
			StringBuilder sb = new StringBuilder (value.Current.ToString());
			while (value.MoveNext()) {
				sb.Append(separator);
				sb.Append(value.Current.ToString());
			}
			return sb.ToString();
		}

		/// <summary>
		/// This formatter works like string.Format, except that named 
		/// placeholders accepted as well as numeric placeholders. This method
		/// replaces named placeholders with numbers, then calls string.Format.
		/// </summary>
		/// <remarks>
		/// Named placeholders are useful for communicating information about a
		/// placeholder to a human translator. Here is an example:
		/// <code>
		/// Not enough memory to {load/parse} '{filename}'.
		/// </code>
		/// In some cases a translator might have difficulty translating a phrase
		/// without knowing what a numeric placeholder ({0} or {1}) refers to, so 
		/// a named placeholder can provide an important clue. The localization  
		/// system is invoked as follows:
		/// <code>
		/// string msg = Localize.From("{man's name} meets {woman's name}.",
		///		"man's name", mansName, "woman's name", womansName);
		/// </code>
		/// The placeholder names are not case sensitive.
		/// 
		/// You can use numeric placeholders, alignment and formatting codes also:
		/// <code>
		/// string msg = Localize.From("You need to run {km,6:###.00} km to reach {0}",
		///		cityName, "KM", 2.9);
		/// </code>
		/// DefaultFormatter will ignore the first N+1 arguments in args, where {N}
		/// is the largest numeric placeholder. It is assumed that the placeholder 
		/// name ends at the first comma or colon; hence the placeholder in this 
		/// example is called "km", not "km,6:###.00".
		/// 
		/// If a placeholder name is not found in the argument list then it is not
		/// replaced with a number before the call to string.Format, so a 
		/// FormatException will occur.
		/// </remarks>
		public static string Format(string format, params object[] args)
		{
			format = EliminateNamedArgs(format, args);
			return string.Format(format, args);
		}

		/// <summary>Called by Format to replace named placeholders with numeric
		/// placeholders in format strings.</summary>
		/// <returns>A format string that can be used to call string.Format.</returns>
		/// <seealso cref="Format"/>
		public static string EliminateNamedArgs(string format, params object[] args)
		{
			char c;
			bool containsNames = false;
			int highestIndex = -1;

			for (int i = 0; i < format.Length - 1; i++)
				if (format[i] == '{' && format[i + 1] != '{')
				{
					int j = ++i;
					for (; (c = format[i]) >= '0' && c <= '9'; i++) { }
					if (i == j)
						containsNames = true;
					else
						highestIndex = int.Parse(format.Substring(j, i - j));
				}

			if (!containsNames)
				return format;

			StringBuilder sb = new StringBuilder(format);
			int correction = 0;
			for (int i = 0; i < sb.Length - 1; i++)
			{
				if (sb[i] == '{' && sb[i + 1] != '{')
				{
					int j = ++i; // Placeholder name starts here.
					for (; (c = format[i]) != '}' && c != ':' && c != ','; i++) { }

					// StringBuilder lacks Substring()! Instead, get the name 
					// from the original string and keep track of a correction 
					// factor so that in subsequent iterations, we get the 
					// substring from the right position in the original string.
					string name = format.Substring(j - correction, i - j);

					for (int arg = highestIndex + 1; arg < args.Length; arg += 2)
						if (args[arg] != null && string.Compare(name, args[arg].ToString(), true) == 0)
						{
							// Matching argument found. Replace name with index:
							string idxStr = (arg + 1).ToString();
							sb.Remove(j, i - j);
							sb.Insert(j, idxStr);
							int dif = i - j - idxStr.Length;
							correction += dif;
							i -= dif;
							break;
						}
				}
			}
			return sb.ToString();
		}
	}
	
	public static class ArrayExt
	{
		public static T[] Clone<T>(this T[] array) { return (T[]) array.Clone(); }
	}

	public static class ListExt
	{
		public static void RemoveRange<T>(this List<T> list, int index, int count)
		{
			if (index + count > list.Count)
				throw new IndexOutOfRangeException(index.ToString() + " + " + count.ToString() + " > " + list.Count.ToString());
			if (index < 0)
				throw new IndexOutOfRangeException(index.ToString() + " < 0");
			if (count > 0) {
				for (int i = index; i < list.Count - count; i++)
					list[i] = list[i + count];
				Resize(list, list.Count - count);
			}
		}
		public static void RemoveRange<T>(this IList<T> list, int index, int count)
		{
			if (index + count > list.Count)
				throw new IndexOutOfRangeException(index.ToString() + " + " + count.ToString() + " > " + list.Count.ToString());
			if (index < 0)
				throw new IndexOutOfRangeException(index.ToString() + " < 0");
			if (count > 0) {
				for (int i = index; i < list.Count - count; i++)
					list[i] = list[i + count];
				Resize(list, list.Count - count);
			}
		}
		public static void Resize<T>(this List<T> list, int newSize)
		{
			int dif = newSize - list.Count;
			if (dif > 0) {
				do list.Add(default(T));
				while (--dif > 0);
			} else if (dif < 0) {
				int i = list.Count;
				do list.RemoveAt(--i);
				while (--dif > 0);
			}
		}
		public static void Resize<T>(this IList<T> list, int newSize)
		{
			int dif = newSize - list.Count;
			if (dif > 0) {
				do list.Add(default(T));
				while (--dif > 0);
			} else if (dif < 0) {
				int i = list.Count;
				do list.RemoveAt(--i);
				while (--dif > 0);
			}
		}
	}

	public static class TypeExt
	{
		public static string NameWithGenericArgs(this Type type)
		{
			string result = type.Name;
			if (type.IsGenericType)
			{
				// remove generic parameter count (e.g. `1)
				int i = result.LastIndexOf('`');
				if (i > 0)
					result = result.Substring(0, i);

				result = string.Format(
					"{0}<{1}>",
					result,
					StringExt.Join(", ", type.GetGenericArguments()
					                     .Select(t => NameWithGenericArgs(t))));
			}
			return result;
		}
	}

	public static class ExceptionExt
	{
		public static string ToDetailedString(this Exception ex) { return ToDetailedString(ex, 3); }
		
		public static string ToDetailedString(this Exception ex, int maxInnerExceptions)
		{
			StringBuilder sb = new StringBuilder();
			try {
				for (;;)
				{
					sb.AppendFormat("{0}: {1}\n", ex.GetType().Name, ex.Message);
					AppendDataList(ex.Data, sb, "  ", " = ", "\n");
					sb.Append(ex.StackTrace);
					if ((ex = ex.InnerException) == null)
						break;
					sb.Append("\n\n");
					sb.Append(Localize.From("Inner exception:"));
					sb.Append(' ');
				}
			} catch { }
			return sb.ToString();
		}

		public static string DataList(this Exception ex)
		{
			return DataList(ex, "", " = ", "\n");
		}
		public static string DataList(this Exception ex, string linePrefix, string keyValueSeparator, string newLine)
		{
			return AppendDataList(ex.Data, null, linePrefix, keyValueSeparator, newLine).ToString();
		}

		public static StringBuilder AppendDataList(IDictionary dict, StringBuilder sb, string linePrefix, string keyValueSeparator, string newLine)
		{
			sb = sb ?? new StringBuilder();
			foreach (DictionaryEntry kvp in dict)
			{
				sb.Append(linePrefix);
				sb.Append(kvp.Key);
				sb.Append(keyValueSeparator);
				sb.Append(kvp.Value);
				sb.Append(newLine);
			}
			return sb;
		}
	}
}
