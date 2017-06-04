using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace GameScriptCompiler
{
	namespace Text
	{
		public class BackingObject
		{
			public Boolean isStore = false;
		}

		/// <summary>
		/// A backing store stream allows a class to provide a dynamic fallback type
		/// stream of BackObject objects.
		/// 
		/// For example, suppose you want to read from a list of 10 integers.
		/// For each integer you want to print out its number, but when the
		/// pattern "2,3,4" shows up, you want to print the sum of all three integers.
		/// 
		/// Using a backing stream, while reading each character, you can choose the option
		/// to "store" a read result. You can use these special reads to find the three
		/// integers, and if the next three read characters are not the suspected pattern,
		/// you can call read the next three times without the store option to read all the
		/// stored reads back, handling them differently and continuing on gracefully.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		public abstract class BackingObjectStream<T> where T : BackingObject
		{
			protected class Backing
			{
				public Queue<T> Store = new Queue<T>();
				public String ID;
			}
			protected Stack<Backing> Stores = new Stack<Backing>();
			protected T PrevBeforeStore;
			protected Boolean isStored = false;
			public T Current, Prev, Next;

			protected abstract T ReadNext();
			protected abstract Boolean EndOfStream_Internal();

			public Boolean EndOfStream
			{
				get
				{
					return Stores.Count == 0 && this.EndOfStream_Internal();
				}
			}

			public void NewBacking(String id = null)
			{
				Stores.Push(new Backing() { ID = id });
			}

			protected Boolean StoresEmpty
			{
				get
				{
					return Stores.Count == 0;
				}
			}

			protected Boolean BackingEmpty
			{
				get
				{
					return QPk.Count == 0;
				}
			}

			protected Queue<T> QPk
			{
				get
				{
					return Stores.Peek().Store;
				}
			}

			protected String IDPk
			{
				get
				{
					return Stores.Peek().ID;
				}
			}

			public T Read(Boolean store = false)
			{
				if (!store && !StoresEmpty)
				{
					isStored = true;
					//Returning a backing store version requires that prev be valid
					if (PrevBeforeStore != null)
					{
						Prev = PrevBeforeStore;
						PrevBeforeStore = default(T);
					}
					else
						Prev = Current;
					Current = QPk.Dequeue();
					Current.isStore = true;
					if (BackingEmpty)
						Stores.Pop();
					return Current;
				}
				else
					isStored = false;

				Prev = Current;

				Current = ReadNext();

				if (store)
				{
					/*if (Dirty)
						throw new Exception("Cannot store the read object because backing store was not empty.");*/
					if (StoresEmpty)
						NewBacking(null);
					if (BackingEmpty)
						PrevBeforeStore = Prev;
					QPk.Enqueue(Current);
				}

				return Current;
			}

			public void DiscardBackingStore(int count)
			{
				if (!StoresEmpty && count > QPk.Count)
					throw new Exception("Attempted to pop off more than the size of the current backing!");
				while (!StoresEmpty && !BackingEmpty && count-- > 0)
					QPk.Dequeue();
				if (!StoresEmpty && BackingEmpty)
					Stores.Pop();
			}

			/// <summary>
			/// Manually enqueues the current item into a new backing, all other stored reads will be stored
			/// in that backing from now on.
			/// </summary>
			/// <param name="id">ID of the new backing.</param>
			public void StartStore(String id = null)
			{
				NewBacking(id);
				QPk.Enqueue(Current);
				PrevBeforeStore = Prev;
			}


			/// <summary>
			/// Stored the current item into the current backing. Creates a new backing
			/// if one doesn't already exist.
			/// </summary>
			/// <param name="id">ID of the new backing.</param>
			public void Enqueue(String id = null)
			{
				if (StoresEmpty)
					NewBacking(id);
				QPk.Enqueue(Current);
				PrevBeforeStore = Prev;
			}

			/// <summary>
			/// Empties queue into current backing, a new one
			/// if it doesn't already exist.
			/// </summary>
			/// <param name="newQueue"></param>
			public void EnqueueNew(Queue<T> newQueue, String id = null)
			{
				if (StoresEmpty)
					NewBacking(id);
				while (newQueue.Count > 0)
					QPk.Enqueue(newQueue.Dequeue());
				PrevBeforeStore = Prev;
			}

			/// <summary>
			/// Returns whether the current token is a stored one
			/// that was taken from the a backing with the
			/// matching ID.
			/// </summary>
			/// <param name="id"></param>
			/// <returns></returns>
			public Boolean On(String id)
			{
				if (StoresEmpty || !isStored)
					return false;
				return IDPk == id && isStored;
			}
		}

		public struct DocLocation
		{
			public int Line, Column;
			public long Offset;
			public override string ToString()
			{
				return "(Line: {0}, Column: {1}, Offset: {2})".Fmt(Line, Column, Offset);
			}
			public void Reset()
			{
				Line = 0;
				Column = 0;
				Offset = 0;
			}
		}

		public class TextChar : BackingObject
		{
			public Char Character;
			public DocLocation Location = new DocLocation();
		}

		class TextCharReader : BackingObjectStream<TextChar>
		{
			StreamReader CurrentStream;
			public DocLocation CurrentLocation = new DocLocation();

			public float Progress
			{
				get
				{
					return (float)CurrentStream.BaseStream.Position / (float)CurrentStream.BaseStream.Length;
				}
			}

			public TextCharReader(StreamReader stream)
			{
				this.CurrentLocation.Line = 1;
				this.CurrentStream = stream;
			}

			protected override bool EndOfStream_Internal()
			{
				return CurrentStream.EndOfStream;
			}

			protected override TextChar ReadNext()
			{
				TextChar tc;
				if (!EndOfStream)
				{
					tc = new TextChar() { Character = (Char)this.CurrentStream.Read() };
					CurrentLocation.Column++;
					tc.Location = CurrentLocation;
					CurrentLocation.Offset++;
					if (tc.Character == '\n')
					{
						CurrentLocation.Column = 0;
						CurrentLocation.Line++;
					}
				}
				else
				{
					CurrentLocation.Column++;
					CurrentLocation.Offset = this.CurrentStream.BaseStream.Length;
					tc = null;
				}
				return tc;
			}
		}
	}
}
