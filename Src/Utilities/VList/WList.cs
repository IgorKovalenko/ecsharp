﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using System.Threading;
using Loyc.Runtime;

namespace Loyc.Utilities
{
	/// <summary>
	/// WList is the mutable variant of the VList data structure.
	/// </summary>
	/// <remarks>See the remarks of <see cref="VListBlock{T}"/> for more information
	/// about VLists and WLists. It is most efficient to add items to the front of
	/// a WList (at index 0) or the back of an RWList (at index Count-1).</remarks>
	public sealed class WList<T> : WListBase<T>, ICloneable
	{
		protected override int AdjustWListIndex(int index, int size) { return index; }

		#region Constructors

		internal WList(VListBlock<T> block, int localCount, bool isOwner)
			: base(block, localCount, isOwner) {}
		public WList() {} // empty list is all null
		public WList(int initialSize)
		{
			VListBlock<T>.MuAddEmpty(this, initialSize);
		}
		public WList(T itemZero, T itemOne)
		{
			// Reverse order when constructing block because the second argument is
			// conceptually added second, so it will be at index [0].
			Block = new VListBlockOfTwo<T>(itemOne, itemZero, true);
			LocalCount = 2;
		}
		
		#endregion
		
		#region AddRange, InsertRange, RemoveRange

		// Note: WList doesn't offer AddRange(IEnumerable<T>) because it would 
		// add the items in reverse order (the first item enumerated would have 
		// the highest index). AddRange(IList<T>) adds list[list.Count-1] first.

		public void AddRange(IList<T> list) { AddRangeBase(list, false); }
		public void InsertRange(int index, IList<T> list) { InsertRangeAtDff(index, list, false); }
		public void RemoveRange(int index, int count)     { RemoveRangeBase(index, count); }

		#endregion

		#region IList<T>/ICollection<T> Members

		public new T this[int index]
		{
			get {
				if ((uint)index >= (uint)Count)
					throw new IndexOutOfRangeException();
				return GetAtDff(index);
			}
			set {
				if ((uint)index >= (uint)Count)
					throw new IndexOutOfRangeException();
				VListBlock<T>.EnsureMutable(this, index + 1);
				SetAtDff(index, value);
			}
		}

		public new void Insert(int index, T item) { InsertAtDff(index, item); }
		public new void RemoveAt(int index) { RemoveAtDff(index); }

		/// <summary>Gets an item from the list at the specified index; returns 
		/// defaultValue if the index is not valid.</summary>
		public T this[int index, T defaultValue]
		{
			get {
				if ((uint)index >= (uint)Count)
					return defaultValue;
				return GetAtDff(index);
			}
		}

		#endregion

		#region IEnumerable<T> Members

		protected override IEnumerator<T> GetWListEnumerator() { return GetEnumerator(); }
		public new VList<T>.Enumerator GetEnumerator()
		{
			return new VList<T>.Enumerator(InternalVList);
		}
		public RVList<T>.Enumerator ReverseEnumerator()
		{
			return new RVList<T>.Enumerator(InternalVList);
		}

		#endregion

		#region ICloneable Members

		public WList<T> Clone() {
			VListBlock<T>.EnsureImmutable(Block, LocalCount);
			return new WList<T>(Block, LocalCount, false);
		}
		object ICloneable.Clone() { return Clone(); }

		#endregion

		#region Other stuff

		/// <summary>Returns the front item of the list (at index 0).</summary>
		public T Front
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
		/// <summary>Removes the front item (at index 0) from the list and returns it.</summary>
		public T Pop()
		{
			if (Block == null)
				throw new InvalidOperationException("Pop: The list is empty.");
			T item = Front;
			RemoveAtDff(0);
			return item;
		}

		public VList<T> WithoutFirst(int numToRemove)
		{
			return VListBlock<T>.EnsureImmutable(Block, LocalCount - numToRemove);
		}

		/// <summary>Returns this list as an RWList, which effectively reverses 
		/// the order of the elements.</summary>
		/// <remarks>This operation marks the items of the list as immutable.
		/// You can modify either list afterward, but some or all of the list 
		/// may have to be copied.</remarks>
		public static explicit operator RWList<T>(WList<T> list) { return list.ToRWList(); }
		/// <summary>Returns this list as an RWList, which effectively reverses 
		/// the order of the elements.</summary>
		/// <remarks>This operation marks the items of the list as immutable.
		/// You can modify either list afterward, but some or all of the list 
		/// may have to be copied.</remarks>
		public RWList<T> ToRWList()
		{
			VListBlock<T>.EnsureImmutable(Block, LocalCount);
			return new RWList<T>(Block, LocalCount, false);
		}

		/// <summary>Returns the WList converted to an array.</summary>
		public T[] ToArray()
		{
			return VListBlock<T>.ToArray(Block, LocalCount, false);
		}

		#endregion
	}
	
	[TestFixture]
	public class WListTests
	{
		[Test]
		public void SimpleTests()
		{
			// Tests simple adds and removes from the front of the list. It
			// makes part of its tail immutable, but doesn't make it mutable
			// again. Also, we test operations that don't modify the list.

			WList<int> list = new WList<int>();
			Assert.That(list.IsEmpty);
			
			// create VListBlockOfTwo
			list = new WList<int>(10, 20);
			ExpectList(list, 10, 20);

			// Add()
			list.Clear();
			list.Add(1);
			Assert.That(!list.IsEmpty);
			list.Add(2);
			Assert.AreEqual(1, list.BlockChainLength);
			list.Add(3);
			Assert.AreEqual(2, list.BlockChainLength);

			ExpectList(list, 3, 2, 1);
			VList<int> snap = list.ToVList();
			ExpectList(snap, 3, 2, 1);
			
			// AddRange(), Push(), Pop()
			list.Push(4);
			list.AddRange(new int[] { 6, 5 });
			ExpectList(list, 6, 5, 4, 3, 2, 1);
			Assert.AreEqual(list.Pop(), 6);
			ExpectList(list, 5, 4, 3, 2, 1);
			list.RemoveRange(0, 2);
			ExpectList(list, 3, 2, 1);

			// Double the list
			list.AddRange(list);
			ExpectList(list, 3, 2, 1, 3, 2, 1);
			list.RemoveRange(0, 3);

			// Fill a third block
			list.AddRange(new int[] { 9, 8, 7, 6, 5, 4 });
			list.AddRange(new int[] { 14, 13, 12, 11, 10 });
			ExpectList(list, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1);
			
			// Remove(), enumerator
			list.Remove(14);
			list.Remove(13);
			list.Remove(12);
			list.Remove(11);
			ExpectListByEnumerator(list, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1);

			// IndexOutOfRangeException
			AssertThrows<IndexOutOfRangeException>(delegate() { int i = list[-1]; });
			AssertThrows<IndexOutOfRangeException>(delegate() { int i = list[10]; });
			AssertThrows<IndexOutOfRangeException>(delegate() { list.Insert(-1, -1); });
			AssertThrows<IndexOutOfRangeException>(delegate() { list.Insert(list.Count+1, -1); });
			AssertThrows<IndexOutOfRangeException>(delegate() { list.RemoveAt(-1); });
			AssertThrows<IndexOutOfRangeException>(delegate() { list.RemoveAt(list.Count); });

			// Front, Contains, IndexOf
			Assert.That(list.Front == 10);
			Assert.That(list.Contains(9));
			Assert.That(list[list.IndexOf(2)] == 2);
			Assert.That(list[list.IndexOf(9)] == 9);
			Assert.That(list[list.IndexOf(7)] == 7);
			Assert.That(list.IndexOf(-1) == -1);

			// snap is still the same
			ExpectList(snap, 3, 2, 1);
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
			WList<int> A = new WList<int>();
			A.AddRange(new int[] { 5, 6, 7 });
			WList<int> B = A.Clone();
			
			A.Push(4);
			ExpectList(B, 5, 6, 7);
			ExpectList(A, 4, 5, 6, 7);
			B.Push(-4);
			ExpectList(B, -4, 5, 6, 7);
			
			Assert.That(A.WithoutFirst(2) == B.WithoutFirst(2));
		}

		[Test]
		public void TestMutabilification()
		{
			// Make a single block mutable
			VList<int> v = new VList<int>(0, 1);
			WList<int> w = v.ToWList();
			ExpectList(w, 0, 1);
			w[0] = 2;
			ExpectList(w, 2, 1);
			ExpectList(v, 0, 1);

			// Make another block, make the front block mutable, then the block-of-2
			v.Push(-1);
			w = v.ToWList();
			w[0] = 3;
			ExpectList(w, 3, 0, 1);
			Assert.That(w.WithoutFirst(1) == v.WithoutFirst(1));
			w[1] = 2;
			ExpectList(w, 3, 2, 1);
			Assert.That(w.WithoutFirst(1) != v.WithoutFirst(1));

			// Now for a more complicated case: create a long immutable chain by
			// using a nasty access pattern, add a mutable block in front, then 
			// make some of the immutable blocks mutable. This will cause several
			// immutable blocks to be consolidated into one mutable block, 
			// shortening the chain.
			v = new VList<int>(6);
			v = v.Add(-1).Tail.Add(5).Add(-1).Tail.Add(4).Add(-1).Tail.Add(3);
			v = v.Add(-1).Tail.Add(2).Add(-1).Tail.Add(1).Add(-1).Tail.Add(0);
			ExpectList(v, 0, 1, 2, 3, 4, 5, 6);
			// At this point, every block in the chain has only one item (it's 
			// a linked list!) and the capacity of each block is 2.
			Assert.AreEqual(7, v.BlockChainLength);

			w = v.ToWList();
			w.AddRange(new int[] { 5, 4, 3, 2, 1 });
			Assert.AreEqual(w.Count, 12);
			ExpectList(w, 5, 4, 3, 2, 1, 0, 1, 2, 3, 4, 5, 6);
			// Indices:   0  1  2  3  4  5  6  7  8  9  10 11
			// Blocks:    block A   | B   | C| D| E| F| G| H
			Assert.AreEqual(8, w.BlockChainLength);
			Assert.AreEqual(4, w.LocalCount);

			w[8] = -3;
			ExpectList(w, 5, 4, 3, 2, 1, 0, 1, 2, -3, 4, 5, 6);
			// Blocks:    block A   | block I       | F| G| H
			Assert.AreEqual(5, w.BlockChainLength);
		}

		[Test]
		public void TestInsertRemove()
		{
			WList<int> list = new WList<int>();
			for (int i = 0; i <= 12; i++)
				list.Insert(i, i);
			ExpectList(list, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12);

			for (int i = 1; i <= 6; i++)
				list.RemoveAt(i);
			ExpectList(list, 0, 2, 4, 6, 8, 10, 12);

			Assert.AreEqual(0, list.Pop());
			list.Insert(5, -2);
			ExpectList(list, 2, 4, 6, 8, 10, -2, 12);
			list.Insert(5, -1);
			ExpectList(list, 2, 4, 6, 8, 10, -1, -2, 12);

			list.Remove(-1);
			list.Remove(12);
			list[list.Count - 1] = 12;
			ExpectList(list, 2, 4, 6, 8, 10, 12);

			// Make sure WList.Clear doesn't disturb VList
			VList<int> v = list.WithoutFirst(4);
			list.Clear();
			ExpectList(list);
			ExpectList(v, 10, 12);

			// Some simple InsertRange calls where some immutable items must be
			// converted to mutable
			VList<int> oneTwo = new VList<int>(1, 2);
			VList<int> threeFour = new VList<int>(3, 4);
			list = oneTwo.ToWList();
			list.InsertRange(1, threeFour);
			ExpectList(list, 1, 3, 4, 2);
			list = threeFour.ToWList();
			list.InsertRange(2, oneTwo);
			ExpectList(list, 3, 4, 1, 2);

			// More tests...
			list.RemoveRange(0, 2);
			ExpectList(list, 1, 2);
			list.InsertRange(2, new int[] { 3, 3, 4, 4, 4, 5, 6, 7, 8, 9 });
			ExpectList(list, 1, 2, 3, 3, 4, 4, 4, 5, 6, 7, 8, 9);
			list.RemoveRange(3, 3);
			ExpectList(list, 1, 2, 3, 4, 5, 6, 7, 8, 9);
			v = list.ToVList();
			list.RemoveRange(5, 4);
			ExpectList(list, 1, 2, 3, 4, 5);
			ExpectList(v,    1, 2, 3, 4, 5, 6, 7, 8, 9);
		}

		[Test]
		public void TestEmptyListOperations()
		{
			WList<int> a = new WList<int>();
			WList<int> b = new WList<int>();
			a.AddRange(b);
			a.InsertRange(0, b);
			a.RemoveRange(0, 0);
			Assert.That(!a.Remove(0));
			Assert.That(a.IsEmpty);
			Assert.That(a.WithoutFirst(0).IsEmpty);

			a.Add(1);
			Assert.That(a.WithoutFirst(1).IsEmpty);

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
			b.Remove(a.Front);
			Assert.That(b.IsEmpty);
		}
		[Test]
		public void TestFalseOwnership()
		{
			// This test tries to make sure a WList doesn't get confused about what 
			// blocks it owns. It's possible for a WList to share a partially-mutable 
			// block that contains mutable items with another WList, but only one
			// WList owns the items.

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
			WList<int> A = new WList<int>(4);
			for (int i = 0; i < 4; i++)
				A[i] = i;
			WList<int> B = A.Clone();
			
			// B can't add to the second block because it's not the owner, so a 
			// third block is created when we Add(1).
			B.Add(1);
			A.Add(-1);
			ExpectList(A, -1, 0, 1, 2, 3);
			ExpectList(B, 1, 0, 1, 2, 3);
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
			Assert.That(B.Block.Prior == A.WithoutFirst(1));
		}
		[Test]
		public void RandomTest()
		{
			int seed = Environment.TickCount;
			Random r = new Random(seed);
			int action, index, iteration = 0, i = -1;
			WList<int> wlist = new WList<int>();
			List<int> list = new List<int>();

			// Perform a series of random operations on WList:
			// - Calling the setter
			// - RemoveAt
			// - Add to front
			// - Insert
			// - Making part of the list immutable
			try {
				for (iteration = 0; iteration < 100; iteration++) {
					action = r.Next(5);
					int range = list.Count + (action >= 2 ? 1 : 0);
					if (range == 0) { iteration--; continue; }
					index = r.Next(range);
					
					switch (action) {
						case 0:
							list.RemoveAt(index);
							wlist.RemoveAt(index);
							break;
						case 1:
							list[index] = iteration;
							wlist[index] = iteration;
							break;
						case 2:
							list.Insert(0, iteration);
							wlist.Add(iteration);
							break;
						case 3:
							list.Insert(index, iteration);
							wlist.Insert(index, iteration);
							break;
						case 4:
							VList<int> v = wlist.WithoutFirst(index);
							if (r.Next(2) == 0)
								v.Add(index);
							break;
					}
					Assert.AreEqual(list.Count, wlist.Count);
					for (i = 0; i < list.Count; i++)
						Assert.AreEqual(list[i], wlist[i]);
				}
			} catch (Exception e) {
				Console.WriteLine("{0} with seed {1} at iteration {2}, i={3}", e.GetType().Name, seed, iteration, i);
				throw;
			}
		}
	}
}
