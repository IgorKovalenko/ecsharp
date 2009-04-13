﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using System.Threading;

namespace Loyc.Utilities
{
	/// <summary>
	/// WList is the mutable variant of the VList data structure.
	/// </summary>
	/// <remarks>See the remarks of <see cref="VListBlock{T}"/> for more information
	/// about VLists and WLists. It is most efficient to add items to the front of
	/// a WList (at index 0) or the back of an RWList (at index Count-1).</remarks>
	public sealed class RWList<T> : WListBase<T>, ICloneable
	{
		protected override int AdjustWListIndex(int index, int size) { return Count - size - index; }

		#region Constructors

		internal RWList(VListBlock<T> block, int localCount, bool isOwner)
			: base(block, localCount, isOwner) {}
		public RWList() {} // empty list is all null
		public RWList(int initialSize)
		{
			VListBlock<T>.MuAddEmpty(this, initialSize);
		}
		public RWList(T itemZero, T itemOne)
		{
			Block = new VListBlockOfTwo<T>(itemZero, itemOne, true);
			LocalCount = 2;
		}
		
		#endregion
		
		#region AddRange, InsertRange, RemoveRange

		public void AddRange(IEnumerable<T> items) { AddRange(items.GetEnumerator()); }
		public new void AddRange(IEnumerator<T> items) { base.AddRange(items); }
		public void AddRange(IList<T> list) { AddRangeBase(list, true); }
		public void InsertRange(int index, IList<T> list) { InsertRangeAtDff(Count - index, list, true); }
		public void RemoveRange(int index, int count)     { RemoveRangeBase(Count - (index + count), count); }

		#endregion

		#region IList<T>/ICollection<T> Members

		public new T this[int index]
		{
			get {
				if ((uint)index >= (uint)Count)
					throw new IndexOutOfRangeException();
				return GetAtDff(Count - (index + 1));
			}
			set {
				if ((uint)index >= (uint)Count)
					throw new IndexOutOfRangeException();
				int dff = Count - (index + 1);
				VListBlock<T>.EnsureMutable(this, dff + 1);
				SetAtDff(dff, value);
			}
		}

		public new void Insert(int index, T item) { InsertAtDff(Count - index, item); }
		public new void RemoveAt(int index) { RemoveAtDff(Count - (index + 1)); }

		/// <summary>Gets an item from the list at the specified index; returns 
		/// defaultValue if the index is not valid.</summary>
		public T this[int index, T defaultValue]
		{
			get {
				if ((uint)index >= (uint)Count)
					return defaultValue;
				return GetAtDff(Count - (index + 1));
			}
		}

		#endregion

		#region IEnumerable<T> Members

		protected override IEnumerator<T> GetWListEnumerator() { return GetEnumerator(); }
		public new RVList<T>.Enumerator GetEnumerator()
		{
			return new RVList<T>.Enumerator(InternalVList); 
		}
		public VList<T>.Enumerator ReverseEnumerator()
		{
			return new VList<T>.Enumerator(InternalVList);
		}

		#endregion

		#region ICloneable Members

		public RWList<T> Clone() {
			VListBlock<T>.EnsureImmutable(Block, LocalCount);
			return new RWList<T>(Block, LocalCount, false);
		}
		object ICloneable.Clone() { return Clone(); }

		#endregion

		#region Other stuff

		/// <summary>Returns the last item of the list (at index Count-1).</summary>
		public T Back
		{
			get {
				return Block.Front(LocalCount);
			}
		}
		public bool IsEmpty
		{
			get {
				return Count == 0;
			}
		}
		/// <summary>Removes the back item (at index Count-1) from the list and returns it.</summary>
		public T Pop()
		{
			if (Block == null)
				throw new InvalidOperationException("Pop: The list is empty.");
			T item = Back;
			RemoveAtDff(0);
			return item;
		}

		public RVList<T> WithoutLast(int numToRemove)
		{
			return VListBlock<T>.EnsureImmutable(Block, LocalCount - numToRemove).ToRVList();
		}

		/// <summary>Returns this list as a WList, which effectively reverses 
		/// the order of the elements.</summary>
		/// <remarks>This operation marks the items of the list as immutable.
		/// You can modify either list afterward, but some or all of the list 
		/// may have to be copied.</remarks>
		public static explicit operator WList<T>(RWList<T> list) { return list.ToWList(); }
		/// <summary>Returns this list as a WList, which effectively reverses 
		/// the order of the elements.</summary>
		/// <remarks>This operation marks the items of the list as immutable.
		/// You can modify either list afterward, but some or all of the list 
		/// may have to be copied.</remarks>
		public WList<T> ToWList()
		{
			VListBlock<T>.EnsureImmutable(Block, LocalCount);
			return new WList<T>(Block, LocalCount, false);
		}

		/// <summary>Returns the RWList converted to an array.</summary>
		public T[] ToArray()
		{
			return VListBlock<T>.ToArray(Block, LocalCount, true);
		}

		/// <summary>Resizes the list to the specified size.</summary>
		/// <remarks>If the new size is larger than the old size, empty elements 
		/// are added to the end. If the new size is smaller, elements are 
		/// truncated from the end.
		/// <para/>
		/// I decided not to offer a Resize() method for the WList because the 
		/// natural place to insert or remove items in a WList is at the beginning.
		/// For a Resize() method to do so, I felt, would come as too much of a 
		/// surprise to some programmers.
		/// </remarks>
		public void Resize(int newSize)
		{
			int change = newSize - Count;
			if (change > 0)
				VListBlock<T>.MuAddEmpty(this, change);
			else if (change < 0)
				RemoveRangeBase(0, -change);
		}

		#endregion
	}
	
	[TestFixture]
	public class RWListTests
	{
		[Test]
		public void SimpleTests()
		{
			// Tests simple adds and removes from the front of the list. It
			// makes part of its tail immutable, but doesn't make it mutable
			// again. Also, we test operations that don't modify the list.

			RWList<int> list = new RWList<int>();
			Assert.That(list.IsEmpty);
			
			// create VListBlockOfTwo
			list = new RWList<int>(10, 20);
			ExpectList(list, 10, 20);

			// Add()
			list.Clear();
			list.Add(1);
			Assert.That(!list.IsEmpty);
			list.Add(2);
			Assert.AreEqual(1, list.BlockChainLength);
			list.Add(3);
			Assert.AreEqual(2, list.BlockChainLength);

			ExpectList(list, 1, 2, 3);
			RVList<int> snap = list.ToRVList();
			ExpectList(snap, 1, 2, 3);
			
			// AddRange(), Push(), Pop()
			list.Push(4);
			list.AddRange(new int[] { 5, 6 });
			ExpectList(list, 1, 2, 3, 4, 5, 6);
			Assert.AreEqual(list.Pop(), 6);
			ExpectList(list, 1, 2, 3, 4, 5);
			list.RemoveRange(3, 2);
			ExpectList(list, 1, 2, 3);

			// Double the list
			list.AddRange(list);
			ExpectList(list, 1, 2, 3, 1, 2, 3);
			list.RemoveRange(3, 3);

			// Fill a third block
			list.AddRange(new int[] { 4, 5, 6, 7, 8, 9 });
			list.AddRange(new int[] { 10, 11, 12, 13, 14 });
			ExpectList(list, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14);
			
			// Remove(), enumerator
			list.Remove(14);
			list.Remove(13);
			list.Remove(12);
			list.Remove(11);
			ExpectListByEnumerator(list, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
			
			// IndexOutOfRangeException
			AssertThrows<IndexOutOfRangeException>(delegate() { int i = list[-1]; });
			AssertThrows<IndexOutOfRangeException>(delegate() { int i = list[10]; });
			AssertThrows<IndexOutOfRangeException>(delegate() { list.Insert(-1, -1); });
			AssertThrows<IndexOutOfRangeException>(delegate() { list.Insert(list.Count+1, -1); });
			AssertThrows<IndexOutOfRangeException>(delegate() { list.RemoveAt(-1); });
			AssertThrows<IndexOutOfRangeException>(delegate() { list.RemoveAt(list.Count); });

			// Front, Contains, IndexOf
			Assert.That(list.Back == 10);
			Assert.That(list.Contains(9));
			Assert.That(list[list.IndexOf(2)] == 2);
			Assert.That(list[list.IndexOf(9)] == 9);
			Assert.That(list[list.IndexOf(7)] == 7);
			Assert.That(list.IndexOf(-1) == -1);

			// snap is still the same
			ExpectList(snap, 1, 2, 3);
		}

		private void AssertThrows<Type>(TestDelegate @delegate)
		{
			try {
				@delegate();
			} catch (Exception exc) {
				Assert.IsInstanceOf<Type>(exc);
				return;
			}
			Assert.Fail("Delegate did not throw '{0}' as expected.", typeof(Type).Name);
		}

		private static void ExpectList<T>(IList<T> list, params T[] expected)
		{
			Assert.AreEqual(expected.Length, list.Count);
			for (int i = 0; i < expected.Length; i++)
				Assert.AreEqual(expected[i], list[i]);
		}
		private static void ExpectListByEnumerator<T>(IList<T> list, params T[] expected)
		{
			Assert.AreEqual(expected.Length, list.Count);
			int i = 0;
			foreach (T item in list) {
				Assert.AreEqual(expected[i], item);
				i++;
			}
		}

		[Test]
		public void TestFork()
		{
			RWList<int> A = new RWList<int>();
			A.AddRange(new int[] { 1, 2, 3 });
			RWList<int> B = A.Clone();
			
			A.Push(4);
			ExpectList(B, 1, 2, 3);
			ExpectList(A, 1, 2, 3, 4);
			B.Push(-4);
			ExpectList(B, 1, 2, 3, -4);

			Assert.That(A.WithoutLast(2) == B.WithoutLast(2));
		}

		[Test]
		public void TestMutabilification()
		{
			// Make a single block mutable
			RVList<int> v = new RVList<int>(1, 0);
			RWList<int> w = v.ToRWList();
			ExpectList(w, 1, 0);
			w[1] = 2;
			ExpectList(w, 1, 2);
			ExpectList(v, 1, 0);

			// Make another block, make the front block mutable, then the block-of-2
			v.Push(-1);
			w = v.ToRWList();
			w[2] = 3;
			ExpectList(w, 1, 0, 3);
			Assert.That(w.WithoutLast(1) == v.WithoutLast(1));
			w[1] = 2;
			ExpectList(w, 1, 2, 3);
			Assert.That(w.WithoutLast(1) != v.WithoutLast(1));

			// Now for a more complicated case: create a long immutable chain by
			// using a nasty access pattern, add a mutable block in front, then 
			// make some of the immutable blocks mutable. This will cause several
			// immutable blocks to be consolidated into one mutable block, 
			// shortening the chain.
			v = new RVList<int>(6);
			v = v.Add(-1).Tail.Add(5).Add(-1).Tail.Add(4).Add(-1).Tail.Add(3);
			v = v.Add(-1).Tail.Add(2).Add(-1).Tail.Add(1).Add(-1).Tail.Add(0);
			ExpectList(v, 6, 5, 4, 3, 2, 1, 0);
			// At this point, every block in the chain has only one item (it's 
			// a linked list!) and the capacity of each block is 2.
			Assert.AreEqual(7, v.BlockChainLength);

			w = v.ToRWList();
			w.AddRange(new int[] { 1, 2, 3, 4, 5 });
			Assert.AreEqual(w.Count, 12);
			ExpectList(w, 6, 5, 4, 3, 2, 1, 0, 1, 2, 3, 4, 5);
			// Indices:   0  1  2  3  4  5  6  7  8  9  10 11
			// Blocks:    H| G| F| E| D| C|  B  | block A (front of chain)
			Assert.AreEqual(8, w.BlockChainLength);
			Assert.AreEqual(4, w.LocalCount);

			w[3] = -3;
			ExpectList(w, 6, 5, 4, -3, 2, 1, 0, 1, 2, 3, 4, 5);
			// Indices:   0  1  2   3  4  5  6  7  8  9  10 11
			// Blocks:    H| G| F|  block I      | block A (front of chain)
			Assert.AreEqual(5, w.BlockChainLength);
		}

		[Test]
		public void TestInsertRemove()
		{
			RWList<int> list = new RWList<int>();
			for (int i = 0; i <= 12; i++)
				list.Insert(0, i);
			ExpectList(list, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1, 0);

			for (int i = 1; i <= 6; i++)
				list.RemoveAt(i);
			ExpectList(list, 12, 10, 8, 6, 4, 2, 0);

			Assert.AreEqual(0, list.Pop());
			list.Insert(1, -2);
			ExpectList(list, 12, -2, 10, 8, 6, 4, 2);
			list.Insert(2, -1);
			ExpectList(list, 12, -2, -1, 10, 8, 6, 4, 2);

			list.Remove(-1);
			list.Remove(12);
			list[0] = 12;
			ExpectList(list, 12, 10, 8, 6, 4, 2);

			// Make sure RWList.Clear doesn't disturb VList
			RVList<int> v = list.WithoutLast(4);
			list.Clear();
			ExpectList(list);
			ExpectList(v, 12, 10);

			// Some simple InsertRange calls where some immutable items must be
			// converted to mutable
			RVList<int> oneTwo = new RVList<int>(1, 2);
			RVList<int> threeFour = new RVList<int>(3, 4);
			list = oneTwo.ToRWList();
			list.InsertRange(1, threeFour);
			ExpectList(list, 1, 3, 4, 2);
			list = threeFour.ToRWList();
			list.InsertRange(0, oneTwo);
			ExpectList(list, 1, 2, 3, 4);

			// More tests...
			list.RemoveRange(2, 2);
			ExpectList(list, 1, 2);
			list.InsertRange(2, new int[] { 3, 3, 4, 4, 4, 5, 6, 7, 8, 9 });
			ExpectList(list, 1, 2, 3, 3, 4, 4, 4, 5, 6, 7, 8, 9);
			list.RemoveRange(3, 3);
			ExpectList(list, 1, 2, 3, 4, 5, 6, 7, 8, 9);
			v = list.ToRVList();
			list.RemoveRange(5, 4);
			ExpectList(list, 1, 2, 3, 4, 5);
			ExpectList(v,    1, 2, 3, 4, 5, 6, 7, 8, 9);
		}

		[Test]
		public void TestEmptyListOperations()
		{
			RWList<int> a = new RWList<int>();
			RWList<int> b = new RWList<int>();
			a.AddRange(b);
			a.InsertRange(0, b);
			a.RemoveRange(0, 0);
			Assert.That(!a.Remove(0));
			Assert.That(a.IsEmpty);
			Assert.That(a.WithoutLast(0).IsEmpty);

			a.Add(1);
			Assert.That(a.WithoutLast(1).IsEmpty);

			b.AddRange(a);
			ExpectList(b, 1);
			b.RemoveAt(0);
			Assert.That(b.IsEmpty);
			b.InsertRange(0, a);
			ExpectList(b, 1);
			b.RemoveRange(0, 1);
			Assert.That(b.IsEmpty);
			b.Insert(0, a[0]);
			ExpectList(b, 1);
			b.Remove(a.Back);
			Assert.That(b.IsEmpty);
		}
		[Test]
		public void TestFalseOwnership()
		{
			// This test tries to make sure a RWList doesn't get confused about what 
			// blocks it owns. It's possible for a RWList to share a partially-mutable 
			// block that contains mutable items with another RWList, but only one
			// RWList owns the items.

			// Case 1: two WLists point to the same block but only one owns it:
			//
			//        block 0
			//      owned by A
			//        |____3|    block 1
			//        |____2|    unowned
			// A,B--->|Imm_1|--->|Imm_1|
			//        |____0|    |____0|
			//
			// (The location of "Imm" in each block denotes the highest immutable 
			// item; this diagram shows there are two immutable items in each 
			// block)
			RWList<int> A = new RWList<int>(4);
			for (int i = 0; i < 4; i++)
				A[i] = i;
			RWList<int> B = A.Clone();
			
			// B can't add to the second block because it's not the owner, so a 
			// third block is created when we Add(1).
			B.Add(4);
			A.Add(-4);
			ExpectList(A, 0, 1, 2, 3, -4);
			ExpectList(B, 0, 1, 2, 3, 4);
			Assert.AreEqual(2, A.BlockChainLength);
			Assert.AreEqual(3, B.BlockChainLength);

			// Case 2: two WLists point to different blocks but they share a common
			// tail, where one list owns part of the tail and the other does not:
			//
			//      block 0
			//    owned by B
			//      |____8|
			//      |____7|
			//      |____6|   
			//      |____5|         block 1
			//      |____4|       owned by A
			//      |____3|   A     |____3|     block 2
			//      |____2|   |     |____2|     unowned
			//      |____1|---+---->|Imm_1|---->|Imm_1|
			// B--->|____0|         |____0|     |____0|
			//      mutable
			//
			// Actually the previous test puts us in just this state.
			//
			// I can't think of a test that uses the public interface to detect bugs
			// in this case. The most important thing is that B._block.PriorIsOwned 
			// returns false. 
			Assert.That(B.IsOwner && !B.Block.PriorIsOwned);
			Assert.That(A.IsOwner);
			Assert.That(B.Block.Prior.ToRVList() == A.WithoutLast(1));
		}
	}
}
