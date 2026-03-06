glfw_dep = dependency('glfw3', required: true)

gif_encoder_src = files('../../src/savers/gif/tvgGifEncoder.cpp')

lottie2gif_gl_src = files('lottie2gif-gl.cpp')

executable('lottie2gif-gl',
           lottie2gif_gl_src + gif_encoder_src,
           include_directories : [headers, include_directories('../../src/savers/gif'), include_directories('../../src/common'), include_directories('../../src/renderer')],
           cpp_args : compiler_flags,
           dependencies : [glfw_dep],
           install : true,
           link_with : thorvg_lib)
