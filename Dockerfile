FROM mcr.microsoft.com/dotnet/sdk:8.0
ENV KEYRINGS /usr/local/share/keyrings

RUN set -eux; \
    mkdir -p $KEYRINGS; \
    apt-get update && apt-get install -y gpg curl; \
    curl --fail https://apt.llvm.org/llvm-snapshot.gpg.key | gpg --dearmor > $KEYRINGS/llvm.gpg; \
    echo "deb [signed-by=$KEYRINGS/llvm.gpg] http://apt.llvm.org/bookworm/ llvm-toolchain-bookworm-18 main" > /etc/apt/sources.list.d/llvm.list;

RUN set -eux; \
    apt-get update && apt-get install --no-install-recommends -y \
        clang-18 \
        llvm-18 \
        lld-18 \
        tar; \
    ln -s clang-18 /usr/bin/clang && ln -s clang /usr/bin/clang++ && ln -s lld-18 /usr/bin/ld.lld; \
    ln -s clang-18 /usr/bin/clang-cl && ln -s llvm-ar-18 /usr/bin/llvm-lib && ln -s lld-link-18 /usr/bin/lld-link; \
    clang++ -v; \
    ld.lld -v; \
    llvm-lib -v; \
    clang-cl -v; \
    lld-link --version; \
    update-alternatives --install /usr/bin/cc cc /usr/bin/clang 100; \
    update-alternatives --install /usr/bin/c++ c++ /usr/bin/clang++ 100; \
    apt-get remove -y --auto-remove; \
    rm -rf /var/lib/apt/lists/*;

RUN set -eux; \
    xwin_version="0.7.0"; \
    xwin_prefix="xwin-$xwin_version-x86_64-unknown-linux-musl"; \
    mkdir -p /usr/local/cargo/bin; \
    curl --fail -L https://github.com/Jake-Shadle/xwin/releases/download/$xwin_version/$xwin_prefix.tar.gz | tar -xzv -C /usr/local/cargo/bin --strip-components=1 $xwin_prefix/xwin; \
    /usr/local/cargo/bin/xwin --accept-license -L trace splat --include-debug-libs --include-debug-symbols --output /xwin; \
    rm -rf .xwin-cache /usr/local/cargo/bin/xwin;

ENV LIB /xwin/crt/lib/x86_64/;/xwin/sdk/lib/um/x86_64/;/xwin/sdk/lib/ucrt/x86_64/

# Create stub libs for NativeAOT linker
RUN mkdir -p /sources; \
    echo "static int _812C67632677495096D50769C0FA5EDE = 1;" > /sources/empty.c; \
    clang-cl -c /sources/empty.c -o /sources/empty.o && llvm-lib /OUT:/NOEXP /sources/empty.o; \
    echo "static int _A562B229B5FD440B925917D02EEC72DC = 1;" > /sources/empty.c; \
    clang-cl -c /sources/empty.c -o /sources/empty.o && llvm-lib /OUT:/NOIMPLIB /sources/empty.o; \
    rm /sources/empty.c /sources/empty.o

RUN apt-get update && apt-get install -y zlib1g-dev;

WORKDIR /repo

# Copy project files for restore (layer caching)
COPY Directory.Build.props ./
COPY src/ThorVG/ThorVG.csproj src/ThorVG/
COPY src/Glfw/Glfw.csproj src/Glfw/
COPY examples/ExampleFramework/ExampleFramework.csproj examples/ExampleFramework/
COPY examples/FillRule/FillRule.csproj examples/FillRule/
COPY examples/LottieExpressions/LottieExpressions.csproj examples/LottieExpressions/

# Restore for win-x64 NativeAOT
RUN dotnet restore -r win-x64 -p:PublishAot=true examples/FillRule/FillRule.csproj \
 && dotnet restore -r win-x64 -p:PublishAot=true examples/LottieExpressions/LottieExpressions.csproj

# Copy all source files
COPY . .

COPY build/build.sh /build.sh
RUN chmod +x /build.sh
ENTRYPOINT ["/build.sh"]
