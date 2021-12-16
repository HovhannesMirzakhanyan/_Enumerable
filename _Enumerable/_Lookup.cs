using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;


namespace _EnumerableImplementation
{

    public interface ILookup<TKey, TElement> : IEnumerable<IGrouping<TKey, TElement>>
    {
        int Count { get; }
        IEnumerable<TElement> this[TKey key] { get; }
        bool Contains(TKey key);
    }

    public class _Lookup<TKey, TElement> : IEnumerable<IGrouping<TKey, TElement>>, ILookup<TKey, TElement>
    {
        IEqualityComparer<TKey> comparer;
        Grouping[] groupings;
        Grouping lastGrouping;
        int count;

        internal static _Lookup<TKey, TElement> Create<TSource>(IEnumerable<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, IEqualityComparer<TKey> comparer)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (keySelector == null) throw new ArgumentNullException("keySelector");
            if (elementSelector == null) throw new ArgumentNullException("elementSelector");
            _Lookup<TKey, TElement> lookup = new _Lookup<TKey, TElement>(comparer);
            foreach (TSource item in source)
            {
                lookup.GetGrouping(keySelector(item), true).Add(elementSelector(item));
            }
            return lookup;
        }

        public static _Lookup<TKey, TElement> CreateForJoin(IEnumerable<TElement> source, Func<TElement, TKey> keySelector, IEqualityComparer<TKey> comparer)
        {
            _Lookup<TKey, TElement> lookup = new _Lookup<TKey, TElement>(comparer);
            foreach (TElement item in source)
            {
                TKey key = keySelector(item);
                if (key != null) lookup.GetGrouping(key, true).Add(item);
            }
            return lookup;
        }

        _Lookup(IEqualityComparer<TKey> comparer)
        {
            if (comparer == null) comparer = EqualityComparer<TKey>.Default;
            this.comparer = comparer;
            groupings = new Grouping[7];
        }

        public int Count
        {
            get { return count; }
        }

        public IEnumerable<TElement> this[TKey key]
        {
            get
            {
                Grouping grouping = GetGrouping(key, false);
                if (grouping != null) return grouping;
                return EmptyEnumerable<TElement>.Instance;
            }
        }

        public bool Contains(TKey key)
        {
            return GetGrouping(key, false) != null;
        }

        public IEnumerator<IGrouping<TKey, TElement>> GetEnumerator()
        {
            Grouping g = lastGrouping;
            if (g != null)
            {
                do
                {
                    g = g.next;
                    yield return g;
                } while (g != lastGrouping);
            }
        }

        public IEnumerable<TResult> ApplyResultSelector<TResult>(Func<TKey, IEnumerable<TElement>, TResult> resultSelector)
        {
            Grouping g = lastGrouping;
            if (g != null)
            {
                do
                {
                    g = g.next;
                    if (g.count != g.elements.Length) { Array.Resize<TElement>(ref g.elements, g.count); }
                    yield return resultSelector(g.key, g.elements);
                } while (g != lastGrouping);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        internal int InternalGetHashCode(TKey key)
        {
      
            return (key == null) ? 0 : comparer.GetHashCode(key) & 0x7FFFFFFF;
        }

        internal Grouping GetGrouping(TKey key, bool create)
        {
            int hashCode = InternalGetHashCode(key);
            for (Grouping g = groupings[hashCode % groupings.Length]; g != null; g = g.hashNext)
                if (g.hashCode == hashCode && comparer.Equals(g.key, key)) return g;
            if (create)
            {
                if (count == groupings.Length) Resize();
                int index = hashCode % groupings.Length;
                Grouping g = new Grouping();
                g.key = key;
                g.hashCode = hashCode;
                g.elements = new TElement[1];
                g.hashNext = groupings[index];
                groupings[index] = g;
                if (lastGrouping == null)
                {
                    g.next = g;
                }
                else
                {
                    g.next = lastGrouping.next;
                    lastGrouping.next = g;
                }
                lastGrouping = g;
                count++;
                return g;
            }
            return null;
        }

        void Resize()
        {
            int newSize = checked(count * 2 + 1);
            Grouping[] newGroupings = new Grouping[newSize];
            Grouping g = lastGrouping;
            do
            {
                g = g.next;
                int index = g.hashCode % newSize;
                g.hashNext = newGroupings[index];
                newGroupings[index] = g;
            } while (g != lastGrouping);
            groupings = newGroupings;
        }

        internal class Grouping : IGrouping<TKey, TElement>, IList<TElement>
        {
            internal TKey key;
            internal int hashCode;
            internal TElement[] elements;
            internal int count;
            internal Grouping hashNext;
            internal Grouping next;

            internal void Add(TElement element)
            {
                if (elements.Length == count) Array.Resize(ref elements, checked(count * 2));
                elements[count] = element;
                count++;
            }

            public IEnumerator<TElement> GetEnumerator()
            {
                for (int i = 0; i < count; i++) yield return elements[i];
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            
            public TKey Key
            {
                get { return key; }
            }

            int ICollection<TElement>.Count
            {
                get { return count; }
            }

            bool ICollection<TElement>.IsReadOnly
            {
                get { return true; }
            }

            void ICollection<TElement>.Add(TElement item)
            {
                throw new NotSupportedException();
            }

            void ICollection<TElement>.Clear()
            {
                throw new NotSupportedException();
            }

            bool ICollection<TElement>.Contains(TElement item)
            {
                return Array.IndexOf(elements, item, 0, count) >= 0;
            }

            void ICollection<TElement>.CopyTo(TElement[] array, int arrayIndex)
            {
                Array.Copy(elements, 0, array, arrayIndex, count);
            }

            bool ICollection<TElement>.Remove(TElement item)
            {
                throw new NotSupportedException();
            }

            int IList<TElement>.IndexOf(TElement item)
            {
                return Array.IndexOf(elements, item, 0, count);
            }

            void IList<TElement>.Insert(int index, TElement item)
            {
                throw new NotSupportedException();
            }

            void IList<TElement>.RemoveAt(int index)
            {
                throw new NotSupportedException();
            }

            TElement IList<TElement>.this[int index]
            {
                get
                {
                    if (index < 0 || index >= count) throw new ArgumentOutOfRangeException("index");
                    return elements[index];
                }
                set
                {
                    throw new NotSupportedException();
                }
            }
        }
        internal class EmptyEnumerable<TElement>
        {
            public static readonly TElement[] Instance = new TElement[0];
        }
    }
}
