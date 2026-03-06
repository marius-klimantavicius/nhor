using Xunit;

namespace ThorVG.Tests
{
    public class tvgArrayTests
    {
        [Fact]
        public void NewArray_IsEmpty()
        {
            var arr = new Array<int>();
            Assert.True(arr.Empty());
            Assert.Equal(0u, arr.count);
            arr.Dispose();
        }

        [Fact]
        public void Push_And_Indexer()
        {
            var arr = new Array<int>();
            arr.Push(10);
            arr.Push(20);
            arr.Push(30);
            Assert.Equal(3u, arr.count);
            Assert.Equal(10, arr[0]);
            Assert.Equal(20, arr[1]);
            Assert.Equal(30, arr[2]);
            arr.Dispose();
        }

        [Fact]
        public void Reserve_Allocates()
        {
            var arr = new Array<int>();
            arr.Reserve(100);
            Assert.True(arr.reserved >= 100);
            Assert.Equal(0u, arr.count);
            arr.Dispose();
        }

        [Fact]
        public void Pop_DecrementsCount()
        {
            var arr = new Array<int>();
            arr.Push(1);
            arr.Push(2);
            arr.Pop();
            Assert.Equal(1u, arr.count);
            Assert.Equal(1, arr[0]);
            arr.Dispose();
        }

        [Fact]
        public void Clear_SetsCountToZero()
        {
            var arr = new Array<int>();
            arr.Push(1);
            arr.Push(2);
            arr.Clear();
            Assert.Equal(0u, arr.count);
            Assert.True(arr.Empty());
            arr.Dispose();
        }

        [Fact]
        public void Reset_FreesData()
        {
            var arr = new Array<int>();
            arr.Push(1);
            arr.Reset();
            Assert.True(arr.Empty());
            Assert.Equal(0u, arr.reserved);
            arr.Dispose();
        }

        [Fact]
        public void Last_And_First()
        {
            var arr = new Array<int>();
            arr.Push(10);
            arr.Push(20);
            arr.Push(30);
            Assert.Equal(10, arr.First());
            Assert.Equal(30, arr.Last());
            arr.Dispose();
        }

        [Fact]
        public void CopyFrom_DuplicatesContent()
        {
            var a = new Array<int>();
            a.Push(1);
            a.Push(2);
            a.Push(3);

            var b = new Array<int>();
            b.CopyFrom(a);
            Assert.Equal(a.count, b.count);
            Assert.Equal(a[0], b[0]);
            Assert.Equal(a[2], b[2]);
            a.Dispose();
            b.Dispose();
        }

        [Fact]
        public void MoveTo_TransfersOwnership()
        {
            var a = new Array<int>();
            a.Push(42);
            var b = new Array<int>();
            a.MoveTo(ref b);
            Assert.True(a.Empty());
            Assert.Equal(1u, b.count);
            Assert.Equal(42, b[0]);
            b.Dispose();
        }

        [Fact]
        public void Push_Array_AppendsAll()
        {
            var a = new Array<int>();
            a.Push(1);
            a.Push(2);
            var b = new Array<int>();
            b.Push(3);
            b.Push(4);
            a.Push(b);
            Assert.Equal(4u, a.count);
            Assert.Equal(3, a[2]);
            Assert.Equal(4, a[3]);
            a.Dispose();
            b.Dispose();
        }

        [Fact]
        public void Grow_IncreasesReserved()
        {
            var arr = new Array<int>();
            arr.Reserve(5);
            arr.Grow(10);
            Assert.True(arr.reserved >= 10);
            arr.Dispose();
        }

        [Fact]
        public void Next_ExpandsAndReturnsRef()
        {
            var arr = new Array<int>();
            ref int slot = ref arr.Next();
            slot = 99;
            Assert.Equal(1u, arr.count);
            Assert.Equal(99, arr[0]);
            arr.Dispose();
        }

        [Fact]
        public void Full_WhenCountEqualsReserved()
        {
            var arr = new Array<int>(2);
            arr.Push(1);
            arr.Push(2);
            Assert.True(arr.Full());
            arr.Dispose();
        }

        [Fact]
        public unsafe void Begin_And_End_Pointers()
        {
            var arr = new Array<int>();
            arr.Push(10);
            arr.Push(20);
            int* begin = arr.Begin();
            int* end = arr.End();
            Assert.Equal(10, *begin);
            Assert.Equal(2, (int)(end - begin));
            arr.Dispose();
        }

        [Fact]
        public void Constructor_WithSize_Reserves()
        {
            var arr = new Array<int>(16);
            Assert.True(arr.reserved >= 16);
            Assert.True(arr.Empty());
            arr.Dispose();
        }
    }
}
