// Ported from ThorVG/src/common/tvgInlist.h

namespace ThorVG
{
    /// <summary>
    /// Interface that list items must implement to participate in an
    /// <see cref="Inlist{T}"/>.  Mirrors the C++ <c>INLIST_ITEM(T)</c>
    /// macro which adds <c>prev</c>/<c>next</c> pointers.
    /// </summary>
    public interface IInlistNode<T> where T : class, IInlistNode<T>
    {
        T? Prev { get; set; }
        T? Next { get; set; }
    }

    /// <summary>
    /// A doubly-linked intrusive list.  The nodes themselves store the
    /// prev/next pointers (via <see cref="IInlistNode{T}"/>).
    /// </summary>
    public class Inlist<T> where T : class, IInlistNode<T>
    {
        public T? Head;
        public T? Tail;

        /// <summary>Remove and dispose all elements.</summary>
        public void Free()
        {
            while (Head != null)
            {
                var t = Head;
                Head = t.Next;
                // In C++ the nodes are deleted; here we just unlink.
                // If T : IDisposable, callers can add their own cleanup.
                t.Prev = null;
                t.Next = null;
            }
            Head = Tail = null;
        }

        /// <summary>Append an element at the back of the list.</summary>
        public void Back(T element)
        {
            if (Tail != null)
            {
                Tail.Next = element;
                element.Prev = Tail;
                element.Next = null;
                Tail = element;
            }
            else
            {
                Head = Tail = element;
                element.Prev = null;
                element.Next = null;
            }
        }

        /// <summary>Prepend an element at the front of the list.</summary>
        public void Front(T element)
        {
            if (Head != null)
            {
                Head.Prev = element;
                element.Prev = null;
                element.Next = Head;
                Head = element;
            }
            else
            {
                Head = Tail = element;
                element.Prev = null;
                element.Next = null;
            }
        }

        /// <summary>Remove and return the last element (or null).
        /// Mirrors C++ Inlist::back() which does NOT clear removed node's links.</summary>
        public T? PopBack()
        {
            if (Tail == null) return null;
            var t = Tail;
            Tail = t.Prev;
            if (Tail == null) Head = null;
            return t;
        }

        /// <summary>Remove and return the first element (or null).
        /// Mirrors C++ Inlist::front() which does NOT clear removed node's links.</summary>
        public T? PopFront()
        {
            if (Head == null) return null;
            var t = Head;
            Head = t.Next;
            if (Head == null) Tail = null;
            return t;
        }

        /// <summary>Remove a specific element from the list.</summary>
        public void Remove(T element)
        {
            if (element.Prev != null) element.Prev.Next = element.Next;
            if (element.Next != null) element.Next.Prev = element.Prev;
            if (ReferenceEquals(element, Head)) Head = element.Next;
            if (ReferenceEquals(element, Tail)) Tail = element.Prev;
            element.Prev = null;
            element.Next = null;
        }

        public bool Empty() => Head == null;
    }
}
