using Xunit;

namespace ThorVG.Tests
{
    // Test node
    public class TestNode : IInlistNode<TestNode>
    {
        public int Value;
        public TestNode? Prev { get; set; }
        public TestNode? Next { get; set; }

        public TestNode(int v) { Value = v; }
    }

    public class tvgInlistTests
    {
        [Fact]
        public void NewList_IsEmpty()
        {
            var list = new Inlist<TestNode>();
            Assert.True(list.Empty());
            Assert.Null(list.Head);
            Assert.Null(list.Tail);
        }

        [Fact]
        public void Back_AppendsElements()
        {
            var list = new Inlist<TestNode>();
            list.Back(new TestNode(1));
            list.Back(new TestNode(2));
            list.Back(new TestNode(3));

            Assert.False(list.Empty());
            Assert.Equal(1, list.Head!.Value);
            Assert.Equal(3, list.Tail!.Value);
        }

        [Fact]
        public void Front_PrependsElements()
        {
            var list = new Inlist<TestNode>();
            list.Front(new TestNode(1));
            list.Front(new TestNode(2));
            list.Front(new TestNode(3));

            Assert.Equal(3, list.Head!.Value);
            Assert.Equal(1, list.Tail!.Value);
        }

        [Fact]
        public void PopBack_ReturnsLastElement()
        {
            var list = new Inlist<TestNode>();
            list.Back(new TestNode(1));
            list.Back(new TestNode(2));

            var item = list.PopBack();
            Assert.NotNull(item);
            Assert.Equal(2, item!.Value);
            Assert.Equal(1, list.Head!.Value);
            Assert.Equal(1, list.Tail!.Value);
        }

        [Fact]
        public void PopFront_ReturnsFirstElement()
        {
            var list = new Inlist<TestNode>();
            list.Back(new TestNode(1));
            list.Back(new TestNode(2));

            var item = list.PopFront();
            Assert.NotNull(item);
            Assert.Equal(1, item!.Value);
            Assert.Equal(2, list.Head!.Value);
        }

        [Fact]
        public void PopBack_FromEmpty_ReturnsNull()
        {
            var list = new Inlist<TestNode>();
            Assert.Null(list.PopBack());
        }

        [Fact]
        public void PopFront_FromEmpty_ReturnsNull()
        {
            var list = new Inlist<TestNode>();
            Assert.Null(list.PopFront());
        }

        [Fact]
        public void Remove_MiddleElement()
        {
            var list = new Inlist<TestNode>();
            var n1 = new TestNode(1);
            var n2 = new TestNode(2);
            var n3 = new TestNode(3);
            list.Back(n1);
            list.Back(n2);
            list.Back(n3);

            list.Remove(n2);
            Assert.Equal(1, list.Head!.Value);
            Assert.Equal(3, list.Tail!.Value);
            Assert.Equal(n3, n1.Next);
            Assert.Equal(n1, n3.Prev);
        }

        [Fact]
        public void Remove_HeadElement()
        {
            var list = new Inlist<TestNode>();
            var n1 = new TestNode(1);
            var n2 = new TestNode(2);
            list.Back(n1);
            list.Back(n2);

            list.Remove(n1);
            Assert.Equal(2, list.Head!.Value);
            Assert.Equal(2, list.Tail!.Value);
        }

        [Fact]
        public void Remove_TailElement()
        {
            var list = new Inlist<TestNode>();
            var n1 = new TestNode(1);
            var n2 = new TestNode(2);
            list.Back(n1);
            list.Back(n2);

            list.Remove(n2);
            Assert.Equal(1, list.Head!.Value);
            Assert.Equal(1, list.Tail!.Value);
        }

        [Fact]
        public void Free_ClearsAll()
        {
            var list = new Inlist<TestNode>();
            list.Back(new TestNode(1));
            list.Back(new TestNode(2));
            list.Back(new TestNode(3));

            list.Free();
            Assert.True(list.Empty());
            Assert.Null(list.Head);
            Assert.Null(list.Tail);
        }

        [Fact]
        public void Traversal_ForwardAndBackward()
        {
            var list = new Inlist<TestNode>();
            list.Back(new TestNode(1));
            list.Back(new TestNode(2));
            list.Back(new TestNode(3));

            // Forward
            int sum = 0;
            for (var cur = list.Head; cur != null; cur = cur.Next) sum += cur.Value;
            Assert.Equal(6, sum);

            // Backward
            sum = 0;
            for (var cur = list.Tail; cur != null; cur = cur.Prev) sum += cur.Value;
            Assert.Equal(6, sum);
        }
    }
}
