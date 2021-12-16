using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;



namespace _EnumerableImplementation
{
    public static partial class _Enumerable
    {
        public static IEnumerable<TSource> Where<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (predicate == null) throw new ArgumentNullException("predicate");
            if (source is Iterator<TSource>) return ((Iterator<TSource>)source).Where(predicate);
            if (source is TSource[]) return new WhereArrayIterator<TSource>((TSource[])source, predicate);
            if (source is List<TSource>) return new WhereListIterator<TSource>((List<TSource>)source, predicate);
            return new WhereEnumerableIterator<TSource>(source, predicate);
        }
        public static IEnumerable<TSource> Where<TSource>(this IEnumerable<TSource> source, Func<TSource, int, bool> predicate)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (predicate == null) throw new ArgumentNullException("predicate");
            return WhereIterator<TSource>(source, predicate);
        }
        static IEnumerable<TSource> WhereIterator<TSource>(IEnumerable<TSource> source, Func<TSource, int, bool> predicate)
        {
            int index = -1;
            foreach (TSource element in source)
            {
                checked { index++; }
                if (predicate(element, index)) yield return element;
            }
        }
        public static IEnumerable<TResult> Select<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TResult> selector)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (selector == null) throw new ArgumentNullException("selector");
            if (source is Iterator<TSource>) return ((Iterator<TSource>)source).Select(selector);
            if (source is TSource[]) return new WhereSelectArrayIterator<TSource, TResult>((TSource[])source, null, selector);
            if (source is List<TSource>) return new WhereSelectListIterator<TSource, TResult>((List<TSource>)source, null, selector);
            return new WhereSelectEnumerableIterator<TSource, TResult>(source, null, selector);
        }
        public static IEnumerable<TResult> Select<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, int, TResult> selector)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (selector == null) throw new ArgumentNullException("selector");
            return SelectIterator<TSource, TResult>(source, selector);
        }
        static IEnumerable<TResult> SelectIterator<TSource, TResult>(IEnumerable<TSource> source, Func<TSource, int, TResult> selector)
        {
            int index = -1;
            foreach (TSource element in source)
            {
                checked { index++; }
                yield return selector(element, index);
            }
        }
        static Func<TSource, bool> CombinePredicates<TSource>(Func<TSource, bool> predicate1, Func<TSource, bool> predicate2)
        {
            return x => predicate1(x) && predicate2(x);
        }
        static Func<TSource, TResult> CombineSelectors<TSource, TMiddle, TResult>(Func<TSource, TMiddle> selector1, Func<TMiddle, TResult> selector2)
        {
            return x => selector2(selector1(x));
        }
        abstract class Iterator<TSource> : IEnumerable<TSource>, IEnumerator<TSource>
        {
            int threadId;
            internal int state;
            internal TSource current;

            public Iterator()
            {
                threadId = Thread.CurrentThread.ManagedThreadId;
            }

            public TSource Current
            {
                get { return current; }
            }

            public abstract Iterator<TSource> Clone();

            public virtual void Dispose()
            {
                current = default(TSource);
                state = -1;
            }

            public IEnumerator<TSource> GetEnumerator()
            {
                if (threadId == Thread.CurrentThread.ManagedThreadId && state == 0)
                {
                    state = 1;
                    return this;
                }
                Iterator<TSource> duplicate = Clone();
                duplicate.state = 1;
                return duplicate;
            }

            public abstract bool MoveNext();

            public abstract IEnumerable<TResult> Select<TResult>(Func<TSource, TResult> selector);

            public abstract IEnumerable<TSource> Where(Func<TSource, bool> predicate);

            object IEnumerator.Current
            {
                get { return Current; }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            void IEnumerator.Reset()
            {
                throw new NotImplementedException();
            }
        }
        class WhereEnumerableIterator<TSource> : Iterator<TSource>
        {
            IEnumerable<TSource> _source;
            Func<TSource, bool> _predicate;
            IEnumerator<TSource> enumerator;

            public WhereEnumerableIterator(IEnumerable<TSource> source, Func<TSource, bool> predicate)
            {
                _source = source;
                _predicate = predicate;
            }

            public override Iterator<TSource> Clone()
            {
                return new WhereEnumerableIterator<TSource>(_source, _predicate);
            }

            public override void Dispose()
            {
                if (enumerator is IDisposable) ((IDisposable)enumerator).Dispose();
                enumerator = null;
                base.Dispose();
            }

            public override bool MoveNext()
            {
                switch (state)
                {
                    case 1:
                        enumerator = _source.GetEnumerator();
                        state = 2;
                        goto case 2;
                    case 2:
                        while (enumerator.MoveNext())
                        {
                            TSource item = enumerator.Current;
                            if (_predicate(item))
                            {
                                current = item;
                                return true;
                            }
                        }
                        Dispose();
                        break;
                }
                return false;
            }

            public override IEnumerable<TResult> Select<TResult>(Func<TSource, TResult> selector)
            {
                return new WhereSelectEnumerableIterator<TSource, TResult>(_source, _predicate, selector);
            }

            public override IEnumerable<TSource> Where(Func<TSource, bool> predicate)
            {
                return new WhereEnumerableIterator<TSource>(_source, CombinePredicates(this._predicate, predicate));
            }
        }
        class WhereArrayIterator<TSource> : Iterator<TSource>
        {
            TSource[] _source;
            Func<TSource, bool> _predicate;
            int index;

            public WhereArrayIterator(TSource[] source, Func<TSource, bool> predicate)
            {
                _source = source;
                _predicate = predicate;
            }

            public override Iterator<TSource> Clone()
            {
                return new WhereArrayIterator<TSource>(_source, _predicate);
            }

            public override bool MoveNext()
            {
                if (state == 1)
                {
                    while (index < _source.Length)
                    {
                        TSource item = _source[index];
                        index++;
                        if (_predicate(item))
                        {
                            current = item;
                            return true;
                        }
                    }
                    Dispose();
                }
                return false;
            }

            public override IEnumerable<TResult> Select<TResult>(Func<TSource, TResult> selector)
            {
                return new WhereSelectArrayIterator<TSource, TResult>(_source, _predicate, selector);
            }

            public override IEnumerable<TSource> Where(Func<TSource, bool> predicate)
            {
                return new WhereArrayIterator<TSource>(_source, CombinePredicates(this._predicate, predicate));
            }
        }
        class WhereListIterator<TSource> : Iterator<TSource>
        {
            List<TSource> source;
            Func<TSource, bool> predicate;
            List<TSource>.Enumerator enumerator;

            public WhereListIterator(List<TSource> source, Func<TSource, bool> predicate)
            {
                this.source = source;
                this.predicate = predicate;
            }

            public override Iterator<TSource> Clone()
            {
                return new WhereListIterator<TSource>(source, predicate);
            }

            public override bool MoveNext()
            {
                switch (state)
                {
                    case 1:
                        enumerator = source.GetEnumerator();
                        state = 2;
                        goto case 2;
                    case 2:
                        while (enumerator.MoveNext())
                        {
                            TSource item = enumerator.Current;
                            if (predicate(item))
                            {
                                current = item;
                                return true;
                            }
                        }
                        Dispose();
                        break;
                }
                return false;
            }

            public override IEnumerable<TResult> Select<TResult>(Func<TSource, TResult> selector)
            {
                return new WhereSelectListIterator<TSource, TResult>(source, predicate, selector);
            }

            public override IEnumerable<TSource> Where(Func<TSource, bool> predicate)
            {
                return new WhereListIterator<TSource>(source, CombinePredicates(this.predicate, predicate));
            }

        }


        class WhereSelectEnumerableIterator<TSource, TResult> : Iterator<TResult>
        {
            IEnumerable<TSource> source;
            Func<TSource, bool> predicate;
            Func<TSource, TResult> selector;
            IEnumerator<TSource> enumerator;

            public WhereSelectEnumerableIterator(IEnumerable<TSource> source, Func<TSource, bool> predicate, Func<TSource, TResult> selector)
            {
                this.source = source;
                this.predicate = predicate;
                this.selector = selector;
            }

            public override Iterator<TResult> Clone()
            {
                return new WhereSelectEnumerableIterator<TSource, TResult>(source, predicate, selector);
            }

            public override void Dispose()
            {
                if (enumerator is IDisposable) ((IDisposable)enumerator).Dispose();
                enumerator = null;
                base.Dispose();
            }

            public override bool MoveNext()
            {
                switch (state)
                {
                    case 1:
                        enumerator = source.GetEnumerator();
                        state = 2;
                        goto case 2;
                    case 2:
                        while (enumerator.MoveNext())
                        {
                            TSource item = enumerator.Current;
                            if (predicate == null || predicate(item))
                            {
                                current = selector(item);
                                return true;
                            }
                        }
                        Dispose();
                        break;
                }
                return false;
            }

            public override IEnumerable<TResult2> Select<TResult2>(Func<TResult, TResult2> selector)
            {
                return new WhereSelectEnumerableIterator<TSource, TResult2>(source, predicate, CombineSelectors(this.selector, selector));
            }

            public override IEnumerable<TResult> Where(Func<TResult, bool> predicate)
            {
                return new WhereEnumerableIterator<TResult>(this, predicate);
            }
        }
        class WhereSelectArrayIterator<TSource, TResult> : Iterator<TResult>
        {
            TSource[] source;
            Func<TSource, bool> predicate;
            Func<TSource, TResult> selector;
            int index;

            public WhereSelectArrayIterator(TSource[] source, Func<TSource, bool> predicate, Func<TSource, TResult> selector)
            {
                this.source = source;
                this.predicate = predicate;
                this.selector = selector;
            }

            public override Iterator<TResult> Clone()
            {
                return new WhereSelectArrayIterator<TSource, TResult>(source, predicate, selector);
            }

            public override bool MoveNext()
            {
                if (state == 1)
                {
                    while (index < source.Length)
                    {
                        TSource item = source[index];
                        index++;
                        if (predicate == null || predicate(item))
                        {
                            current = selector(item);
                            return true;
                        }
                    }
                    Dispose();
                }
                return false;
            }

            public override IEnumerable<TResult2> Select<TResult2>(Func<TResult, TResult2> selector)
            {
                return new WhereSelectArrayIterator<TSource, TResult2>(source, predicate, CombineSelectors(this.selector, selector));
            }

            public override IEnumerable<TResult> Where(Func<TResult, bool> predicate)
            {
                return new WhereEnumerableIterator<TResult>(this, predicate);
            }
        }
        class WhereSelectListIterator<TSource, TResult> : Iterator<TResult>
        {
            List<TSource> source;
            Func<TSource, bool> predicate;
            Func<TSource, TResult> selector;
            List<TSource>.Enumerator enumerator;

            public WhereSelectListIterator(List<TSource> source, Func<TSource, bool> predicate, Func<TSource, TResult> selector)
            {
                this.source = source;
                this.predicate = predicate;
                this.selector = selector;
            }

            public override Iterator<TResult> Clone()
            {
                return new WhereSelectListIterator<TSource, TResult>(source, predicate, selector);
            }

            public override bool MoveNext()
            {
                switch (state)
                {
                    case 1:
                        enumerator = source.GetEnumerator();
                        state = 2;
                        goto case 2;
                    case 2:
                        while (enumerator.MoveNext())
                        {
                            TSource item = enumerator.Current;
                            if (predicate == null || predicate(item))
                            {
                                current = selector(item);
                                return true;
                            }
                        }
                        Dispose();
                        break;
                }
                return false;
            }

            public override IEnumerable<TResult2> Select<TResult2>(Func<TResult, TResult2> selector)
            {
                return new WhereSelectListIterator<TSource, TResult2>(source, predicate, CombineSelectors(this.selector, selector));
            }

            public override IEnumerable<TResult> Where(Func<TResult, bool> predicate)
            {
                return new WhereEnumerableIterator<TResult>(this, predicate);
            }
        }

        public static IEnumerable<TResult> SelectMany<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, IEnumerable<TResult>> selector)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (selector == null) throw new ArgumentNullException("selector");
            return SelectManyIterator<TSource, TResult>(source, selector);
        }

        static IEnumerable<TResult> SelectManyIterator<TSource, TResult>(IEnumerable<TSource> source, Func<TSource, IEnumerable<TResult>> selector)
        {
            foreach (TSource element in source)
            {
                foreach (TResult subElement in selector(element))
                {
                    yield return subElement;
                }
            }
        }
        public static IEnumerable<TResult> SelectMany<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, int, IEnumerable<TResult>> selector)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (selector == null) throw new ArgumentNullException("selector");
            return SelectManyIterator<TSource, TResult>(source, selector);
        }

        static IEnumerable<TResult> SelectManyIterator<TSource, TResult>(IEnumerable<TSource> source, Func<TSource, int, IEnumerable<TResult>> selector)
        {
            int index = -1;
            foreach (TSource element in source)
            {
                checked { index++; }
                foreach (TResult subElement in selector(element, index))
                {
                    yield return subElement;
                }
            }
        }

        public static IEnumerable<TSource> Take<TSource>(this IEnumerable<TSource> source, int count)
        {
            if (source == null) throw new ArgumentNullException("source");
            return TakeIterator<TSource>(source, count);
        }

        static IEnumerable<TSource> TakeIterator<TSource>(IEnumerable<TSource> source, int count)
        {
            if (count > 0)
            {
                foreach (TSource element in source)
                {
                    yield return element;
                    if (--count == 0) break;
                }
            }
        }

        public static IEnumerable<TSource> TakeWhile<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (predicate == null) throw new ArgumentNullException("predicate");
            return TakeWhileIterator<TSource>(source, predicate);
        }

        static IEnumerable<TSource> TakeWhileIterator<TSource>(IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            foreach (TSource element in source)
            {
                if (!predicate(element)) break;
                yield return element;
            }
        }
        public static IEnumerable<TSource> TakeWhile<TSource>(this IEnumerable<TSource> source, Func<TSource, int, bool> predicate)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (predicate == null) throw new ArgumentNullException("predicate");
            return TakeWhileIterator<TSource>(source, predicate);
        }

        static IEnumerable<TSource> TakeWhileIterator<TSource>(IEnumerable<TSource> source, Func<TSource, int, bool> predicate)
        {
            int index = -1;
            foreach (TSource element in source)
            {
                checked { index++; }
                if (!predicate(element, index)) break;
                yield return element;
            }
        }

        public static IEnumerable<TSource> Skip<TSource>(this IEnumerable<TSource> source, int count)
        {
            if (source == null) throw new ArgumentNullException("source");
            return SkipIterator<TSource>(source, count);
        }

        static IEnumerable<TSource> SkipIterator<TSource>(IEnumerable<TSource> source, int count)
        {
            using (IEnumerator<TSource> e = source.GetEnumerator())
            {
                while (count > 0 && e.MoveNext()) count--;
                if (count <= 0)
                {
                    while (e.MoveNext()) yield return e.Current;
                }
            }
        }
        public static IEnumerable<TSource> SkipWhile<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (predicate == null) throw new ArgumentNullException("predicate");
            return SkipWhileIterator<TSource>(source, predicate);
        }

        static IEnumerable<TSource> SkipWhileIterator<TSource>(IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            bool yielding = false;
            foreach (TSource element in source)
            {
                if (!yielding && !predicate(element)) yielding = true;
                if (yielding) yield return element;
            }
        }

        public static IEnumerable<TSource> SkipWhile<TSource>(this IEnumerable<TSource> source, Func<TSource, int, bool> predicate)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (predicate == null) throw new ArgumentNullException("predicate");
            return SkipWhileIterator<TSource>(source, predicate);
        }

        static IEnumerable<TSource> SkipWhileIterator<TSource>(IEnumerable<TSource> source, Func<TSource, int, bool> predicate)
        {
            int index = -1;
            bool yielding = false;
            foreach (TSource element in source)
            {
                checked { index++; }
                if (!yielding && !predicate(element, index)) yielding = true;
                if (yielding) yield return element;
            }
        }
        public static IEnumerable<TResult> Join<TOuter, TInner, TKey, TResult>(this IEnumerable<TOuter> outer, IEnumerable<TInner> inner, Func<TOuter, TKey> outerKeySelector, Func<TInner, TKey> innerKeySelector, Func<TOuter, TInner, TResult> resultSelector)
        {
            if (outer == null) throw new ArgumentNullException("outer");
            if (inner == null) throw new ArgumentNullException("inner");
            if (outerKeySelector == null) throw new ArgumentNullException("outerKeySelector");
            if (innerKeySelector == null) throw new ArgumentNullException("innerKeySelector");
            if (resultSelector == null) throw new ArgumentNullException("resultSelector");
            return JoinIterator<TOuter, TInner, TKey, TResult>(outer, inner, outerKeySelector, innerKeySelector, resultSelector, null);
        }

        public static IEnumerable<TResult> Join<TOuter, TInner, TKey, TResult>(this IEnumerable<TOuter> outer, IEnumerable<TInner> inner, Func<TOuter, TKey> outerKeySelector, Func<TInner, TKey> innerKeySelector, Func<TOuter, TInner, TResult> resultSelector, IEqualityComparer<TKey> comparer)
        {
            if (outer == null) throw new ArgumentNullException("outer");
            if (inner == null) throw new ArgumentNullException("inner");
            if (outerKeySelector == null) throw new ArgumentNullException("outerKeySelector");
            if (innerKeySelector == null) throw new ArgumentNullException("innerKeySelector");
            if (resultSelector == null) throw new ArgumentNullException("resultSelector");
            return JoinIterator<TOuter, TInner, TKey, TResult>(outer, inner, outerKeySelector, innerKeySelector, resultSelector, comparer);
        }

        static IEnumerable<TResult> JoinIterator<TOuter, TInner, TKey, TResult>(IEnumerable<TOuter> outer, IEnumerable<TInner> inner, Func<TOuter, TKey> outerKeySelector, Func<TInner, TKey> innerKeySelector, Func<TOuter, TInner, TResult> resultSelector, IEqualityComparer<TKey> comparer)
        {
            _Lookup<TKey, TInner> lookup = _Lookup<TKey, TInner>.CreateForJoin(inner, innerKeySelector, comparer);
            foreach (TOuter item in outer)
            {
                _Lookup<TKey, TInner>.Grouping g = lookup.GetGrouping(outerKeySelector(item), false);
                if (g != null)
                {
                    for (int i = 0; i < g.count; i++)
                    {
                        yield return resultSelector(item, g.elements[i]);
                    }
                }
            }
        }

        public static IEnumerable<TResult> GroupJoin<TOuter, TInner, TKey, TResult>(this IEnumerable<TOuter> outer, IEnumerable<TInner> inner, Func<TOuter, TKey> outerKeySelector, Func<TInner, TKey> innerKeySelector, Func<TOuter, IEnumerable<TInner>, TResult> resultSelector)
        {
            if (outer == null) throw new ArgumentNullException("outer");
            if (inner == null) throw new ArgumentNullException("inner");
            if (outerKeySelector == null) throw new ArgumentNullException("outerKeySelector");
            if (innerKeySelector == null) throw new ArgumentNullException("innerKeySelector");
            if (resultSelector == null) throw new ArgumentNullException("resultSelector");
            return GroupJoinIterator<TOuter, TInner, TKey, TResult>(outer, inner, outerKeySelector, innerKeySelector, resultSelector, null);
        }

        public static IEnumerable<TResult> GroupJoin<TOuter, TInner, TKey, TResult>(this IEnumerable<TOuter> outer, IEnumerable<TInner> inner, Func<TOuter, TKey> outerKeySelector, Func<TInner, TKey> innerKeySelector, Func<TOuter, IEnumerable<TInner>, TResult> resultSelector, IEqualityComparer<TKey> comparer)
        {
            if (outer == null) throw new ArgumentNullException("outer");
            if (inner == null) throw new ArgumentNullException("inner");
            if (outerKeySelector == null) throw new ArgumentNullException("outerKeySelector");
            if (innerKeySelector == null) throw new ArgumentNullException("innerKeySelector");
            if (resultSelector == null) throw new ArgumentNullException("resultSelector");
            return GroupJoinIterator<TOuter, TInner, TKey, TResult>(outer, inner, outerKeySelector, innerKeySelector, resultSelector, comparer);
        }

        static IEnumerable<TResult> GroupJoinIterator<TOuter, TInner, TKey, TResult>(IEnumerable<TOuter> outer, IEnumerable<TInner> inner, Func<TOuter, TKey> outerKeySelector, Func<TInner, TKey> innerKeySelector, Func<TOuter, IEnumerable<TInner>, TResult> resultSelector, IEqualityComparer<TKey> comparer)
        {
            _Lookup<TKey, TInner> lookup = _Lookup<TKey, TInner>.CreateForJoin(inner, innerKeySelector, comparer);
            foreach (TOuter item in outer)
            {
                yield return resultSelector(item, lookup[outerKeySelector(item)]);
            }
        }

        public static IOrderedEnumerable<TSource> OrderBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            return new OrderedEnumerable<TSource, TKey>(source, keySelector, null, false);
        }

        public static IOrderedEnumerable<TSource> OrderBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, IComparer<TKey> comparer)
        {
            return new OrderedEnumerable<TSource, TKey>(source, keySelector, comparer, false);
        }

        public static IOrderedEnumerable<TSource> OrderByDescending<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            return new OrderedEnumerable<TSource, TKey>(source, keySelector, null, true);
        }

        public static IOrderedEnumerable<TSource> OrderByDescending<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, IComparer<TKey> comparer)
        {
            return new OrderedEnumerable<TSource, TKey>(source, keySelector, comparer, true);
        }

        public static IOrderedEnumerable<TSource> ThenBy<TSource, TKey>(this IOrderedEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            if (source == null) throw new ArgumentNullException("source");
            return source.CreateOrderedEnumerable<TKey>(keySelector, null, false);
        }

        public static IOrderedEnumerable<TSource> ThenBy<TSource, TKey>(this IOrderedEnumerable<TSource> source, Func<TSource, TKey> keySelector, IComparer<TKey> comparer)
        {
            if (source == null) throw new ArgumentNullException("source");
            return source.CreateOrderedEnumerable<TKey>(keySelector, comparer, false);
        }

        public static IOrderedEnumerable<TSource> ThenByDescending<TSource, TKey>(this IOrderedEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            if (source == null) throw new ArgumentNullException("source");
            return source.CreateOrderedEnumerable<TKey>(keySelector, null, true);
        }

        public static IOrderedEnumerable<TSource> ThenByDescending<TSource, TKey>(this IOrderedEnumerable<TSource> source, Func<TSource, TKey> keySelector, IComparer<TKey> comparer)
        {
            if (source == null) throw new ArgumentNullException("source");
            return source.CreateOrderedEnumerable<TKey>(keySelector, comparer, true);
        }

        public static IEnumerable<IGrouping<TKey, TSource>> GroupBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            return new GroupedEnumerable<TSource, TKey, TSource>(source, keySelector, IdentityFunction<TSource>.Instance, null);
        }

        public static IEnumerable<IGrouping<TKey, TSource>> GroupBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, IEqualityComparer<TKey> comparer)
        {
            return new GroupedEnumerable<TSource, TKey, TSource>(source, keySelector, IdentityFunction<TSource>.Instance, comparer);
        }

        public static IEnumerable<IGrouping<TKey, TElement>> GroupBy<TSource, TKey, TElement>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector)
        {
            return new GroupedEnumerable<TSource, TKey, TElement>(source, keySelector, elementSelector, null);
        }

        public static IEnumerable<IGrouping<TKey, TElement>> GroupBy<TSource, TKey, TElement>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, IEqualityComparer<TKey> comparer)
        {
            return new GroupedEnumerable<TSource, TKey, TElement>(source, keySelector, elementSelector, comparer);
        }

        public static IEnumerable<TResult> GroupBy<TSource, TKey, TResult>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, Func<TKey, IEnumerable<TSource>, TResult> resultSelector)
        {
            return new GroupedEnumerable<TSource, TKey, TSource, TResult>(source, keySelector, IdentityFunction<TSource>.Instance, resultSelector, null);
        }

        public static IEnumerable<TResult> GroupBy<TSource, TKey, TElement, TResult>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, Func<TKey, IEnumerable<TElement>, TResult> resultSelector)
        {
            return new GroupedEnumerable<TSource, TKey, TElement, TResult>(source, keySelector, elementSelector, resultSelector, null);
        }

        public static IEnumerable<TResult> GroupBy<TSource, TKey, TResult>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, Func<TKey, IEnumerable<TSource>, TResult> resultSelector, IEqualityComparer<TKey> comparer)
        {
            return new GroupedEnumerable<TSource, TKey, TSource, TResult>(source, keySelector, IdentityFunction<TSource>.Instance, resultSelector, comparer);
        }

        public static IEnumerable<TResult> GroupBy<TSource, TKey, TElement, TResult>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, Func<TKey, IEnumerable<TElement>, TResult> resultSelector, IEqualityComparer<TKey> comparer)
        {
            return new GroupedEnumerable<TSource, TKey, TElement, TResult>(source, keySelector, elementSelector, resultSelector, comparer);
        }

        internal abstract class OrderedEnumerable<TElement> : IOrderedEnumerable<TElement>
        {
            internal IEnumerable<TElement> _source;

            public IEnumerator<TElement> GetEnumerator()
            {
                Buffer<TElement> buffer = new Buffer<TElement>(_source);
                if (buffer.count > 0)
                {
                    EnumerableSorter<TElement> sorter = GetEnumerableSorter(null);
                    int[] map = sorter.Sort(buffer.items, buffer.count);
                    sorter = null;
                    for (int i = 0; i < buffer.count; i++) yield return buffer.items[map[i]];
                }
            }

            internal abstract EnumerableSorter<TElement> GetEnumerableSorter(EnumerableSorter<TElement> next);

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            IOrderedEnumerable<TElement> IOrderedEnumerable<TElement>.CreateOrderedEnumerable<TKey>(Func<TElement, TKey> keySelector, IComparer<TKey> comparer, bool descending)
            {
                OrderedEnumerable<TElement, TKey> result = new OrderedEnumerable<TElement, TKey>(_source, keySelector, comparer, descending);
                result._parent = this;
                return result;
            }
        }

        internal class GroupedEnumerable<TSource, TKey, TElement, TResult> : IEnumerable<TResult>
        {
            IEnumerable<TSource> source;
            Func<TSource, TKey> keySelector;
            Func<TSource, TElement> elementSelector;
            IEqualityComparer<TKey> comparer;
            Func<TKey, IEnumerable<TElement>, TResult> resultSelector;

            public GroupedEnumerable(IEnumerable<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, Func<TKey, IEnumerable<TElement>, TResult> resultSelector, IEqualityComparer<TKey> comparer)
            {
                if (source == null) throw new ArgumentNullException("source");
                if (keySelector == null) throw new ArgumentNullException("keySelector");
                if (elementSelector == null) throw new ArgumentNullException("elementSelector");
                if (resultSelector == null) throw new ArgumentNullException("resultSelector");
                this.source = source;
                this.keySelector = keySelector;
                this.elementSelector = elementSelector;
                this.comparer = comparer;
                this.resultSelector = resultSelector;
            }

            public IEnumerator<TResult> GetEnumerator()
            {
                _Lookup<TKey, TElement> lookup = _Lookup<TKey, TElement>.Create<TSource>(source, keySelector, elementSelector, comparer);
                return lookup.ApplyResultSelector(resultSelector).GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        internal class GroupedEnumerable<TSource, TKey, TElement> : IEnumerable<IGrouping<TKey, TElement>>
        {
            IEnumerable<TSource> _source;
            Func<TSource, TKey> _keySelector;
            Func<TSource, TElement> _elementSelector;
            IEqualityComparer<TKey> _comparer;

            public GroupedEnumerable(IEnumerable<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, IEqualityComparer<TKey> comparer)
            {
                if (source == null) throw new ArgumentNullException("source");
                if (keySelector == null) throw new ArgumentNullException("keySelector");
                if (elementSelector == null) throw new ArgumentNullException("elementSelector");
                _source = source;
                _keySelector = keySelector;
                _elementSelector = elementSelector;
                _comparer = comparer;
            }

            public IEnumerator<IGrouping<TKey, TElement>> GetEnumerator()
            {
                return _Lookup<TKey, TElement>.Create<TSource>(_source, _keySelector, _elementSelector, _comparer).GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        internal class OrderedEnumerable<TElement, TKey> : OrderedEnumerable<TElement>
        {
            internal OrderedEnumerable<TElement> _parent;
            internal Func<TElement, TKey> _keySelector;
            internal IComparer<TKey> _comparer;
            internal bool _descending;

            internal OrderedEnumerable(IEnumerable<TElement> source, Func<TElement, TKey> keySelector, IComparer<TKey> comparer, bool descending)
            {
                if (source == null) throw new ArgumentNullException("source");
                if (keySelector == null) throw new ArgumentNullException("keySelector");
                _source = source;
                _parent = null;
                _keySelector = keySelector;
                _comparer = comparer != null ? comparer : Comparer<TKey>.Default;
                _descending = descending;
            }

            internal override EnumerableSorter<TElement> GetEnumerableSorter(EnumerableSorter<TElement> next)
            {
                EnumerableSorter<TElement> sorter = new EnumerableSorter<TElement, TKey>(_keySelector, _comparer, _descending, next);
                if (_parent != null) sorter = _parent.GetEnumerableSorter(sorter);
                return sorter;
            }
        }

        internal abstract class EnumerableSorter<TElement>
        {
            internal abstract void ComputeKeys(TElement[] elements, int count);

            internal abstract int CompareKeys(int index1, int index2);

            internal int[] Sort(TElement[] elements, int count)
            {
                ComputeKeys(elements, count);
                int[] map = new int[count];
                for (int i = 0; i < count; i++) map[i] = i;
                QuickSort(map, 0, count - 1);
                return map;
            }

            void QuickSort(int[] map, int left, int right)
            {
                do
                {
                    int i = left;
                    int j = right;
                    int x = map[i + ((j - i) >> 1)];
                    do
                    {
                        while (i < map.Length && CompareKeys(x, map[i]) > 0) i++;
                        while (j >= 0 && CompareKeys(x, map[j]) < 0) j--;
                        if (i > j) break;
                        if (i < j)
                        {
                            int temp = map[i];
                            map[i] = map[j];
                            map[j] = temp;
                        }
                        i++;
                        j--;
                    } while (i <= j);
                    if (j - left <= right - i)
                    {
                        if (left < j) QuickSort(map, left, j);
                        left = i;
                    }
                    else
                    {
                        if (i < right) QuickSort(map, i, right);
                        right = j;
                    }
                } while (left < right);
            }
        }

        internal class EnumerableSorter<TElement, TKey> : EnumerableSorter<TElement>
        {
            internal Func<TElement, TKey> _keySelector;
            internal IComparer<TKey> _comparer;
            internal bool _descending;
            internal EnumerableSorter<TElement> _next;
            internal TKey[] keys;

            internal EnumerableSorter(Func<TElement, TKey> keySelector, IComparer<TKey> comparer, bool descending, EnumerableSorter<TElement> next)
            {
                _keySelector = keySelector;
                _comparer = comparer;
                _descending = descending;
                _next = next;
            }

            internal override void ComputeKeys(TElement[] elements, int count)
            {
                keys = new TKey[count];
                for (int i = 0; i < count; i++) keys[i] = _keySelector(elements[i]);
                if (_next != null) _next.ComputeKeys(elements, count);
            }

            internal override int CompareKeys(int index1, int index2)
            {
                int c = _comparer.Compare(keys[index1], keys[index2]);
                if (c == 0)
                {
                    if (_next == null) return index1 - index2;
                    return _next.CompareKeys(index1, index2);
                }
                return _descending ? -c : c;
            }
        }

        struct Buffer<TElement>
        {
            internal TElement[] items;
            internal int count;

            internal Buffer(IEnumerable<TElement> source)
            {
                TElement[] items = null;
                int count = 0;
                ICollection<TElement> collection = source as ICollection<TElement>;
                if (collection != null)
                {
                    count = collection.Count;
                    if (count > 0)
                    {
                        items = new TElement[count];
                        collection.CopyTo(items, 0);
                    }
                }
                else
                {
                    foreach (TElement item in source)
                    {
                        if (items == null)
                        {
                            items = new TElement[4];
                        }
                        else if (items.Length == count)
                        {
                            TElement[] newItems = new TElement[checked(count * 2)];
                            Array.Copy(items, 0, newItems, 0, count);
                            items = newItems;
                        }
                        items[count] = item;
                        count++;
                    }
                }
                this.items = items;
                this.count = count;
            }

            internal TElement[] ToArray()
            {
                if (count == 0) return new TElement[0];
                if (items.Length == count) return items;
                TElement[] result = new TElement[count];
                Array.Copy(items, 0, result, 0, count);
                return result;
            }
        }

        internal class IdentityFunction<TElement>
        {
            public static Func<TElement, TElement> Instance
            {
                get { return x => x; }
            }
        }

        public interface IOrderedEnumerable<TElement> : IEnumerable<TElement>
        {
            IOrderedEnumerable<TElement> CreateOrderedEnumerable<TKey>(Func<TElement, TKey> keySelector, IComparer<TKey> comparer, bool descending);
        }
    }
}
