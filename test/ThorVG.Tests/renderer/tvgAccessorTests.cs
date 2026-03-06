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
    }
}
