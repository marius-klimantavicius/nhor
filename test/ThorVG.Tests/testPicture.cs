using System;
using System.Text;
using Xunit;

namespace ThorVG.Tests
{
    public class testPicture
    {
        private static readonly string TEST_DIR = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "ref", "ThorVG", "test", "resources"));

        [Fact]
        public void PictureCreation()
        {
            var picture = Picture.Gen();
            Assert.NotNull(picture);

            Assert.Equal(Type.Picture, picture.PaintType());

            Paint.Rel(picture);
        }

        [Fact]
        public void LoadRawData()
        {
            var picture = Picture.Gen();
            Assert.NotNull(picture);

            var rawPath = System.IO.Path.Combine(TEST_DIR, "rawimage_200x300.raw");
            if (!System.IO.File.Exists(rawPath)) return;
            var bytes = System.IO.File.ReadAllBytes(rawPath);
            var data = new uint[bytes.Length / 4];
            System.Buffer.BlockCopy(bytes, 0, data, 0, bytes.Length);

            // Negative cases
            Assert.Equal(Result.InvalidArguments, picture.Load((uint[]?)null!, 200, 300, ColorSpace.ARGB8888, false));
            Assert.Equal(Result.InvalidArguments, picture.Load(data, 0, 0, ColorSpace.ARGB8888, false));
            Assert.Equal(Result.InvalidArguments, picture.Load(data, 200, 0, ColorSpace.ARGB8888, false));
            Assert.Equal(Result.InvalidArguments, picture.Load(data, 0, 300, ColorSpace.ARGB8888, false));

            // Positive cases
            Assert.Equal(Result.Success, picture.Load(data, 200, 300, ColorSpace.ARGB8888, false));
            Assert.Equal(Result.Success, picture.Load(data, 200, 300, ColorSpace.ARGB8888, true));

            Assert.Equal(Result.Success, picture.GetSize(out var w, out var h));

            Assert.Equal(200, w);
            Assert.Equal(300, h);

            Paint.Rel(picture);
        }

        [Fact]
        public void PictureSize()
        {
            var picture = Picture.Gen();
            Assert.NotNull(picture);

            Assert.Equal(Result.InsufficientCondition, picture.GetSize(out _, out _));

            // Primary
            var rawPath = System.IO.Path.Combine(TEST_DIR, "rawimage_200x300.raw");
            if (!System.IO.File.Exists(rawPath)) return;
            var bytes = System.IO.File.ReadAllBytes(rawPath);
            var data = new uint[bytes.Length / 4];
            System.Buffer.BlockCopy(bytes, 0, data, 0, bytes.Length);

            Assert.Equal(Result.Success, picture.Load(data, 200, 300, ColorSpace.ARGB8888, false));

            Assert.Equal(Result.Success, picture.SetSize(100, 100));
            Assert.Equal(Result.Success, picture.GetSize(out var w, out var h));
            Assert.Equal(100, w);
            Assert.Equal(100, h);

            // Secondary
            var rawPath2 = System.IO.Path.Combine(TEST_DIR, "rawimage_250x375.raw");
            if (!System.IO.File.Exists(rawPath2)) return;
            var bytes2 = System.IO.File.ReadAllBytes(rawPath2);
            var data2 = new uint[bytes2.Length / 4];
            System.Buffer.BlockCopy(bytes2, 0, data2, 0, bytes2.Length);

            Assert.Equal(Result.Success, picture.Load(data2, 250, 375, ColorSpace.ARGB8888, false));

            Assert.Equal(Result.Success, picture.GetSize(out w, out h));
            Assert.Equal(Result.Success, picture.SetSize(w, h));

            Paint.Rel(picture);
        }

        [Fact]
        public void PictureOrigin()
        {
            var picture = Picture.Gen();
            Assert.NotNull(picture);

            Assert.Equal(Result.InsufficientCondition, picture.GetSize(out _, out _));

            // Primary
            var rawPath = System.IO.Path.Combine(TEST_DIR, "rawimage_200x300.raw");
            if (!System.IO.File.Exists(rawPath)) return;
            var bytes = System.IO.File.ReadAllBytes(rawPath);
            var data = new uint[bytes.Length / 4];
            System.Buffer.BlockCopy(bytes, 0, data, 0, bytes.Length);

            Assert.Equal(Result.Success, picture.Load(data, 200, 300, ColorSpace.ARGB8888, false));
            Assert.Equal(Result.Success, picture.SetOrigin(0.0f, 0.0f));
            Assert.Equal(Result.Success, picture.SetOrigin(0.5f, 0.5f));
            Assert.Equal(Result.Success, picture.SetOrigin(1.0f, 1.0f));
            Assert.Equal(Result.Success, picture.SetOrigin(-1.0f, -1.0f));

            Paint.Rel(picture);
        }

        [Fact]
        public void PictureResolver()
        {
            var picture = Picture.Gen();
            Assert.NotNull(picture);

            Assert.Equal(Result.InsufficientCondition, picture.GetSize(out _, out _));

            // Primary
            var rawPath = System.IO.Path.Combine(TEST_DIR, "rawimage_200x300.raw");
            if (!System.IO.File.Exists(rawPath)) return;
            var bytes = System.IO.File.ReadAllBytes(rawPath);
            var data = new uint[bytes.Length / 4];
            System.Buffer.BlockCopy(bytes, 0, data, 0, bytes.Length);

            Func<Paint, string, object?, bool> resolver = (Paint paint, string src, object? userData) =>
            {
                return false;
            };

            Assert.Equal(Result.Success, picture.Resolver(resolver, null));

            Assert.Equal(Result.Success, picture.Load(data, 200, 300, ColorSpace.ARGB8888, false));

            Paint.Rel(picture);
        }

        [Fact]
        public void PictureDuplication()
        {
            var picture = Picture.Gen();
            Assert.NotNull(picture);

            // Primary
            var rawPath = System.IO.Path.Combine(TEST_DIR, "rawimage_200x300.raw");
            if (!System.IO.File.Exists(rawPath)) return;
            var bytes = System.IO.File.ReadAllBytes(rawPath);
            var data = new uint[bytes.Length / 4];
            System.Buffer.BlockCopy(bytes, 0, data, 0, bytes.Length);

            Assert.Equal(Result.Success, picture.Load(data, 200, 300, ColorSpace.ARGB8888, false));
            Assert.Equal(Result.Success, picture.SetSize(100, 100));

            var dup = (Picture)picture.Duplicate();
            Assert.NotNull(dup);

            Assert.Equal(Result.Success, picture.GetSize(out var w, out var h));
            Assert.Equal(100, w);
            Assert.Equal(100, h);

            Paint.Rel(dup);
            Paint.Rel(picture);
        }

        [Fact]
        public void LoadSvgFile()
        {
            var picture = Picture.Gen();
            Assert.NotNull(picture);

            // Invalid file
            Assert.Equal(Result.InvalidArguments, picture.Load("invalid.svg"));

            // Load SVG files
            Assert.Equal(Result.Success, picture.Load(System.IO.Path.Combine(TEST_DIR, "test1.svg")));
            Assert.Equal(Result.Success, picture.Load(System.IO.Path.Combine(TEST_DIR, "test2.svg")));
            Assert.Equal(Result.Success, picture.Load(System.IO.Path.Combine(TEST_DIR, "test3.svg")));

            Assert.Equal(Result.Success, picture.GetSize(out var w, out var h));

            Paint.Rel(picture);
        }

        [Fact]
        public void LoadSvgData()
        {
            const string svg = "<svg height=\"1000\" viewBox=\"0 0 1000 1000\" width=\"1000\" xmlns=\"http://www.w3.org/2000/svg\"><path d=\"M.10681413.09784845 1000.0527.01592069V1000.0851L.06005738 999.9983Z\" fill=\"#ffffff\" stroke-width=\"3.910218\"/><g fill=\"#252f35\"><g stroke-width=\"3.864492\"><path d=\"M256.61221 100.51736H752.8963V386.99554H256.61221Z\"/><path d=\"M201.875 100.51736H238.366478V386.99554H201.875Z\"/><path d=\"M771.14203 100.51736H807.633508V386.99554H771.14203Z\"/></g><path d=\"M420.82388 380H588.68467V422.805317H420.82388Z\" stroke-width=\"3.227\"/><path d=\"m420.82403 440.7101v63.94623l167.86079 25.5782V440.7101Z\"/><path d=\"M420.82403 523.07258V673.47362L588.68482 612.59701V548.13942Z\"/></g><g fill=\"#222f35\"><path d=\"M420.82403 691.37851 588.68482 630.5019 589 834H421Z\"/><path d=\"m420.82403 852.52249h167.86079v28.64782H420.82403v-28.64782 0 0\"/><path d=\"m439.06977 879.17031c0 0-14.90282 8.49429-18.24574 15.8161-4.3792 9.59153 0 31.63185 0 31.63185h167.86079c0 0 4.3792-22.04032 0-31.63185-3.34292-7.32181-18.24574-15.8161-18.24574-15.8161z\"/></g><g fill=\"#ffffff\"><path d=\"m280 140h15v55l8 10 8-10v-55h15v60l-23 25-23-25z\"/><path d=\"m335 140v80h45v-50h-25v10h10v30h-15v-57h18v-13z\"/></g></svg>";

            var picture = Picture.Gen();
            Assert.NotNull(picture);

            // Negative cases
            Assert.Equal(Result.InvalidArguments, picture.Load((byte[]?)null!, 100, ""));
            Assert.Equal(Result.InvalidArguments, picture.Load(Encoding.UTF8.GetBytes(svg), 0, ""));

            // Positive cases
            var svgBytes = Encoding.UTF8.GetBytes(svg);
            Assert.Equal(Result.Success, picture.Load(svgBytes, (uint)svgBytes.Length, "svg"));

            Assert.Equal(Result.Success, picture.GetSize(out var w, out var h));
            Assert.Equal(1000, w);
            Assert.Equal(1000, h);

            Paint.Rel(picture);
        }

        [Fact]
        public void LoadPngFileFromPath()
        {
            var picture = Picture.Gen();
            Assert.NotNull(picture);

            // Invalid file
            Assert.Equal(Result.InvalidArguments, picture.Load("invalid.png"));

            var pngPath = System.IO.Path.Combine(TEST_DIR, "test.png");
            if (!System.IO.File.Exists(pngPath)) return;

            Assert.Equal(Result.Success, picture.Load(pngPath));

            Assert.Equal(Result.Success, picture.GetSize(out var w, out var h));

            Assert.Equal(512, w);
            Assert.Equal(512, h);

            Paint.Rel(picture);
        }

        [Fact]
        public void LoadPngFileFromData()
        {
            var picture = Picture.Gen();
            Assert.NotNull(picture);

            var pngPath = System.IO.Path.Combine(TEST_DIR, "test.png");
            if (!System.IO.File.Exists(pngPath)) return;

            var data = System.IO.File.ReadAllBytes(pngPath);

            Assert.Equal(Result.Success, picture.Load(data, (uint)data.Length, ""));
            Assert.Equal(Result.Success, picture.Load(data, (uint)data.Length, "png", "", true));

            Assert.Equal(Result.Success, picture.GetSize(out var w, out var h));
            Assert.Equal(512, w);
            Assert.Equal(512, h);

            Paint.Rel(picture);
        }

        [Fact]
        public void LoadPngFileAndRender()
        {
            Assert.Equal(Result.Success, Initializer.Init());

            var canvas = SwCanvas.Gen();
            Assert.NotNull(canvas);

            var buffer = new uint[100 * 100];
            Assert.Equal(Result.Success, canvas.Target(buffer, 100, 100, 100, ColorSpace.ARGB8888));

            var picture = Picture.Gen();
            Assert.NotNull(picture);

            var pngPath = System.IO.Path.Combine(TEST_DIR, "test.png");
            if (!System.IO.File.Exists(pngPath))
            {
                Initializer.Term();
                return;
            }

            Assert.Equal(Result.Success, picture.Load(pngPath));
            Assert.Equal(Result.Success, picture.Opacity(192));
            Assert.Equal(Result.Success, picture.Scale(5.0f));

            Assert.Equal(Result.Success, canvas.Add(picture));

            Assert.Equal(Result.Success, Initializer.Term());
        }

        [Fact]
        public void LoadJpgFileFromPath()
        {
            var picture = Picture.Gen();
            Assert.NotNull(picture);

            // Invalid file
            Assert.Equal(Result.InvalidArguments, picture.Load("invalid.jpg"));

            var jpgPath = System.IO.Path.Combine(TEST_DIR, "test.jpg");
            if (!System.IO.File.Exists(jpgPath)) return;

            Assert.Equal(Result.Success, picture.Load(jpgPath));

            Assert.Equal(Result.Success, picture.GetSize(out var w, out var h));

            Assert.Equal(512, w);
            Assert.Equal(512, h);

            Paint.Rel(picture);
        }

        [Fact]
        public void LoadJpgFileFromData()
        {
            var picture = Picture.Gen();
            Assert.NotNull(picture);

            var jpgPath = System.IO.Path.Combine(TEST_DIR, "test.jpg");
            if (!System.IO.File.Exists(jpgPath)) return;

            var data = System.IO.File.ReadAllBytes(jpgPath);

            Assert.Equal(Result.Success, picture.Load(data, (uint)data.Length, ""));
            Assert.Equal(Result.Success, picture.Load(data, (uint)data.Length, "jpg", "", true));

            Assert.Equal(Result.Success, picture.GetSize(out var w, out var h));
            Assert.Equal(512, w);
            Assert.Equal(512, h);

            Paint.Rel(picture);
        }

        [Fact]
        public void LoadJpgFileAndRender()
        {
            Assert.Equal(Result.Success, Initializer.Init());

            var canvas = SwCanvas.Gen();
            Assert.NotNull(canvas);

            var buffer = new uint[100 * 100];
            Assert.Equal(Result.Success, canvas.Target(buffer, 100, 100, 100, ColorSpace.ARGB8888));

            var picture = Picture.Gen();
            Assert.NotNull(picture);

            var jpgPath = System.IO.Path.Combine(TEST_DIR, "test.jpg");
            if (!System.IO.File.Exists(jpgPath))
            {
                Initializer.Term();
                return;
            }

            Assert.Equal(Result.Success, picture.Load(jpgPath));

            Assert.Equal(Result.Success, canvas.Add(picture));

            Assert.Equal(Result.Success, Initializer.Term());
        }
    }
}
