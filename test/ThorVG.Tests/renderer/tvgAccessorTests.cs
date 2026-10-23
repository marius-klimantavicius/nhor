using Xunit;

namespace ThorVG.Tests
{
    public class tvgAccessorTests
    {
        [Fact]
        public void Accessor_Gen_ReturnsInstance()
        {
            var acc = Accessor.Gen();
            Assert.NotNull(acc);
        }

        [Fact]
        public void Accessor_Set_NullPaint_InvalidArgs()
        {
            var acc = Accessor.Gen();
            Assert.Equal(Result.InvalidArguments, acc.Set(null!, (p, d) => true));
        }

        [Fact]
        public void Accessor_Set_NullFunc_InvalidArgs()
        {
            var acc = Accessor.Gen();
            var shape = Shape.Gen();
            Assert.Equal(Result.InvalidArguments, acc.Set(shape, null!));
        }

        [Fact]
        public void Accessor_Set_TraversesSinglePaint()
        {
            var acc = Accessor.Gen();
            var shape = Shape.Gen();
            int count = 0;

            var result = acc.Set(shape, (paint, data) => { count++; return true; });
            Assert.Equal(Result.Success, result);
            Assert.Equal(1, count);
        }

        [Fact]
        public void Accessor_Set_TraversesSceneChildren()
        {
            var acc = Accessor.Gen();
            var scene = Scene.Gen();
            scene.Add(Shape.Gen());
            scene.Add(Shape.Gen());

            int count = 0;
            var result = acc.Set(scene, (paint, data) => { count++; return true; });
            Assert.Equal(Result.Success, result);
            Assert.Equal(3, count); // scene + 2 children
        }

        [Fact]
        public void Accessor_Set_StopsOnFalse()
        {
            var acc = Accessor.Gen();
            var scene = Scene.Gen();
            scene.Add(Shape.Gen());
            scene.Add(Shape.Gen());

            int count = 0;
            var result = acc.Set(scene, (paint, data) =>
            {
                count++;
                return false; // stop immediately
            });
            Assert.Equal(Result.Success, result);
            Assert.Equal(1, count); // only the scene itself visited
        }

        [Fact]
        public void Accessor_Set_NestedScenes()
        {
            var acc = Accessor.Gen();
            var outer = Scene.Gen();
            var inner = Scene.Gen();
            inner.Add(Shape.Gen());
            outer.Add(inner);

            int count = 0;
            acc.Set(outer, (paint, data) => { count++; return true; });
            Assert.Equal(3, count); // outer + inner + shape
        }

        [Fact]
        public void Accessor_Id_Djb2Encoding()
        {
            var id1 = Accessor.Id("test");
            var id2 = Accessor.Id("test");
            Assert.Equal(id1, id2);

            var id3 = Accessor.Id("different");
            Assert.NotEqual(id1, id3);
        }

        [Fact]
        public void Accessor_Id_NullReturns0()
        {
            var id = Accessor.Id(null);
            Assert.Equal(0u, id);
        }

        [Fact]
        public void Accessor_CallbackExceptionReleasesTemporaryOwner()
        {
            var accessor = Accessor.Gen();
            var scene = Scene.Gen();
            var child = Shape.Gen();
            scene.Add(child);
            try
            {
                var failure = new System.InvalidOperationException("callback failure");
                var caught = Assert.Throws<System.InvalidOperationException>(() => accessor.Set(scene, (paint, _) =>
                {
                    Assert.Equal((ushort)1, scene.RefCnt());
                    if (ReferenceEquals(paint, child)) throw failure;
                    return true;
                }));
                Assert.Same(failure, caught);
                Assert.Equal((ushort)0, scene.RefCnt());
                Assert.Equal((ushort)1, child.RefCnt());
                Assert.Null(accessor.Name(child.id));
                int visited = 0;
                Assert.Equal(Result.Success, accessor.Set(scene, (_, _) => { ++visited; return true; }));
                Assert.Equal(2, visited);
                Assert.Equal((ushort)0, scene.RefCnt());
            }
            finally { Paint.Rel(scene); }
        }

        [Theory]
        [InlineData(0, 3)]
        [InlineData(1, 2)]
        [InlineData(2, 4)]
        [InlineData(3, 4)]
        public void AccessorPreservesTheNativeSuccessorDuringChildMutation(int mode, int finalCount)
        {
            var scene = Scene.Gen(); scene.id = 1;
            var a = Shape.Gen(); a.id = 2; a.Ref();
            var b = Shape.Gen(); b.id = 3;
            var c = Shape.Gen(); c.id = 4;
            scene.Add(a); scene.Add(b); scene.Add(c);
            try
            {
                var order = new System.Collections.Generic.List<uint>();
                Assert.Equal(Result.Success, Accessor.Gen().Set(scene, (paint, _) =>
                {
                    uint id = paint.id;
                    order.Add(id);
                    if (id == 2 && mode == 1) Assert.Equal(Result.Success, scene.Remove(a));
                    if (id == 2 && mode == 2)
                    {
                        var added = Shape.Gen(); added.id = 5;
                        Assert.Equal(Result.Success, scene.Add(added, b));
                    }
                    if (id == 4 && mode == 3)
                    {
                        var added = Shape.Gen(); added.id = 5;
                        Assert.Equal(Result.Success, scene.Add(added));
                    }
                    return true;
                }));
                Assert.Equal(new uint[] { 1, 2, 3, 4 }, order);
                Assert.Equal(finalCount, scene.Paints().Count);
                Assert.Equal((ushort)0, scene.RefCnt());
            }
            finally { Paint.Rel(scene); a.Unref(); }
        }
    }
}
