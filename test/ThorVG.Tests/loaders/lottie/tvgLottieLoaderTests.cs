// Tests for LottieLoader (loading and frame control)

using System.IO;
using System.Text;
using Xunit;

namespace ThorVG.Tests
{
    public class LottieLoaderTests
    {
        /// <summary>Minimal valid Lottie JSON.</summary>
        private const string MinimalLottieJson = @"{
            ""v"": ""5.7.0"",
            ""fr"": 30,
            ""ip"": 0,
            ""op"": 60,
            ""w"": 100,
            ""h"": 100,
            ""layers"": []
        }";

        /// <summary>Create a temporary Lottie file and return its path.</summary>
        private static string CreateTempLottieFile(string content)
        {
            var path = Path.GetTempFileName() + ".json";
            File.WriteAllText(path, content);
            return path;
        }

        // ---- Type ----

        [Fact]
        public void Type_IsLot()
        {
            var loader = new LottieLoader();
            Assert.Equal(FileType.Lot, loader.type);
        }

        // ---- Open from file path ----

        [Fact]
        public void Open_ValidFile_ReturnsTrue()
        {
            var path = CreateTempLottieFile(MinimalLottieJson);
            try
            {
                var loader = new LottieLoader();
                Assert.True(loader.Open(path));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void Open_NonExistentFile_ReturnsFalse()
        {
            var loader = new LottieLoader();
            Assert.False(loader.Open("/nonexistent/file.json"));
        }

        [Fact]
        public void Open_NonJsonFile_ReturnsFalse()
        {
            var path = Path.GetTempFileName();
            File.WriteAllText(path, "not json content");
            try
            {
                var loader = new LottieLoader();
                Assert.False(loader.Open(path));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void Open_SetsContentAndSize()
        {
            var path = CreateTempLottieFile(MinimalLottieJson);
            try
            {
                var loader = new LottieLoader();
                loader.Open(path);
                Assert.NotNull(loader.content);
                Assert.True(loader.size > 0);
            }
            finally
            {
                File.Delete(path);
            }
        }

        // ---- Open from bytes ----

        [Fact]
        public void Open_ValidBytes_ReturnsTrue()
        {
            var loader = new LottieLoader();
            var data = Encoding.UTF8.GetBytes(MinimalLottieJson);
            Assert.True(loader.Open(data, (uint)data.Length, null, true));
        }

        [Fact]
        public void Open_EmptyBytes_ReturnsFalse()
        {
            var loader = new LottieLoader();
            Assert.False(loader.Open(new byte[0], 0, null, true));
        }

        [Fact]
        public void Open_NullBytes_ReturnsFalse()
        {
            var loader = new LottieLoader();
            Assert.False(loader.Open(null!, 0, null, true));
        }

        // ---- Read (parsing) ----

        [Fact]
        public void Read_AfterOpen_ReturnsTrue()
        {
            var loader = new LottieLoader();
            var data = Encoding.UTF8.GetBytes(MinimalLottieJson);
            loader.Open(data, (uint)data.Length, null, true);
            Assert.True(loader.Read());
        }

        [Fact]
        public void Read_SetsDimensions()
        {
            var loader = new LottieLoader();
            var data = Encoding.UTF8.GetBytes(MinimalLottieJson);
            loader.Open(data, (uint)data.Length, null, true);
            loader.Read();
            Assert.Equal(100u, (uint)loader.w);
            Assert.Equal(100u, (uint)loader.h);
        }

        [Fact]
        public void Read_SetsFrameRate()
        {
            var loader = new LottieLoader();
            var data = Encoding.UTF8.GetBytes(MinimalLottieJson);
            loader.Open(data, (uint)data.Length, null, true);
            loader.Read();
            Assert.Equal(30f, loader.frameRate);
        }

        [Fact]
        public void Read_SetsFrameCnt()
        {
            var loader = new LottieLoader();
            var data = Encoding.UTF8.GetBytes(MinimalLottieJson);
            loader.Open(data, (uint)data.Length, null, true);
            loader.Read();
            Assert.Equal(60f, loader.frameCnt);
        }

        [Fact]
        public void Read_WithoutOpen_ReturnsFalse()
        {
            var loader = new LottieLoader();
            Assert.False(loader.Read());
        }

        // ---- Frame control ----

        [Fact]
        public void TotalFrame_AfterRead_ReturnsFrameCnt()
        {
            var loader = new LottieLoader();
            var data = Encoding.UTF8.GetBytes(MinimalLottieJson);
            loader.Open(data, (uint)data.Length, null, true);
            loader.Read();
            Assert.Equal(60f, loader.TotalFrame());
        }

        [Fact]
        public void Frame_SetsFrameNo()
        {
            var loader = new LottieLoader();
            var data = Encoding.UTF8.GetBytes(MinimalLottieJson);
            loader.Open(data, (uint)data.Length, null, true);
            loader.Read();
            Assert.True(loader.Frame(10f));
            Assert.Equal(10f, loader.CurFrame());
        }

        [Fact]
        public void Frame_SameValue_ReturnsFalse()
        {
            var loader = new LottieLoader();
            var data = Encoding.UTF8.GetBytes(MinimalLottieJson);
            loader.Open(data, (uint)data.Length, null, true);
            loader.Read();
            loader.Frame(10f);
            // Setting the same frame again returns false (no change)
            Assert.False(loader.Frame(10f));
        }

        [Fact]
        public void Frame_WithoutComp_ReturnsFalse()
        {
            var loader = new LottieLoader();
            Assert.False(loader.Frame(10f));
        }

        // ---- Duration ----

        [Fact]
        public void Duration_AfterRead_ReturnsCorrectDuration()
        {
            var loader = new LottieLoader();
            var data = Encoding.UTF8.GetBytes(MinimalLottieJson);
            loader.Open(data, (uint)data.Length, null, true);
            loader.Read();
            // Duration = 60 frames / 30 fps = 2.0 seconds
            Assert.Equal(2.0f, loader.Duration());
        }

        [Fact]
        public void Duration_WithoutComp_ReturnsZero()
        {
            var loader = new LottieLoader();
            Assert.Equal(0f, loader.Duration());
        }

        // ---- Segment ----

        [Fact]
        public void Segment_AfterRead_ReturnsSuccess()
        {
            var loader = new LottieLoader();
            var data = Encoding.UTF8.GetBytes(MinimalLottieJson);
            loader.Open(data, (uint)data.Length, null, true);
            loader.Read();
            Assert.Equal(Result.Success, loader.Segment(0f, 30f));
        }

        [Fact]
        public void Segment_WithoutComp_ReturnsInsufficientCondition()
        {
            var loader = new LottieLoader();
            Assert.Equal(Result.InsufficientCondition, loader.Segment(0f, 30f));
        }

        // ---- Markers ----

        [Fact]
        public void MarkersCnt_NoMarkers_ReturnsZero()
        {
            var loader = new LottieLoader();
            var data = Encoding.UTF8.GetBytes(MinimalLottieJson);
            loader.Open(data, (uint)data.Length, null, true);
            loader.Read();
            Assert.Equal(0u, loader.MarkersCnt());
        }

        [Fact]
        public void MarkersCnt_WithoutComp_ReturnsZero()
        {
            var loader = new LottieLoader();
            Assert.Equal(0u, loader.MarkersCnt());
        }

        // ---- Shorten ----

        [Fact]
        public void Shorten_ReducesPrecision()
        {
            var loader = new LottieLoader();
            // C++ shorten: nearbyintf((frameNo + startFrame()) * 10000) * 0.0001
            // With startFrame()=0: rounds to 4 decimal places
            var result = loader.Shorten(12.345f);
            Assert.Equal(12.345f, result, 3);
        }

        [Fact]
        public void Shorten_ExactDecimal_Unchanged()
        {
            var loader = new LottieLoader();
            Assert.Equal(5.0f, loader.Shorten(5.0f));
        }

        // ---- LoaderMgr integration ----

        [Fact]
        public void LoaderMgr_Find_Lot_ReturnsLottieLoader()
        {
            var loader = LoaderMgr.Find(FileType.Lot);
            Assert.NotNull(loader);
            Assert.IsType<LottieLoader>(loader);
        }

        [Fact]
        public void LoaderMgr_Loader_ByPath_LottieExtension()
        {
            var path = CreateTempLottieFile(MinimalLottieJson);
            try
            {
                var loader = LoaderMgr.Loader(path, out var invalid);
                Assert.NotNull(loader);
                Assert.False(invalid);
                Assert.IsType<LottieLoader>(loader);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void LoaderMgr_Loader_ByBytes_LotMime()
        {
            var data = Encoding.UTF8.GetBytes(MinimalLottieJson);
            var loader = LoaderMgr.Loader(data, (uint)data.Length, "lot", null, true);
            Assert.NotNull(loader);
            Assert.IsType<LottieLoader>(loader);
        }
    }
}
