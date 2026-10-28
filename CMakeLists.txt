cmake_minimum_required(VERSION 3.16)
project(hacksaw C CXX)

set(CMAKE_C_STANDARD 11)
set(CMAKE_C_STANDARD_REQUIRED ON)
set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

set(TP "${CMAKE_SOURCE_DIR}/hksrc/src/Thirdparty")
set(EFK "${TP}/Effekseer/Dev/Cpp")
set(EFKBGFX "${TP}/efkbgfx")

# bgfx/bx/bimg are built once with their own genie+makefiles (MinGW GCC):
#   cd hksrc/src/Thirdparty/bgfx
#   ../bx/tools/bin/windows/genie.exe --gcc=mingw-gcc gmake
#   mingw32-make -C .build/projects/gmake-mingw-gcc config=release64 -j
# then the static libs are linked here.
set(BGFX_LIBS
	"${TP}/bgfx/.build/win64_mingw-gcc/bin/libbgfxRelease.a"
	"${TP}/bgfx/.build/win64_mingw-gcc/bin/libbimgRelease.a"
	"${TP}/bgfx/.build/win64_mingw-gcc/bin/libbxRelease.a"
)

# Collect Effekseer sources
file(GLOB_RECURSE EFK_EFFEKSEER_SRC "${EFK}/Effekseer/*.cpp")
file(GLOB_RECURSE EFK_MATERIAL_SRC "${EFK}/EffekseerMaterial/*.cpp")
file(GLOB_RECURSE EFK_RENDERER_COMMON_SRC "${EFK}/EffekseerRendererCommon/*.cpp")

add_executable(hacksaw
	hksrc/src/Platform/main.c
	hksrc/src/Renderer/r_bgfx.c
	hksrc/src/Thirdparty/miniz/miniz.c
	hksrc/src/Thirdparty/miniz/miniz_tdef.c
	hksrc/src/Thirdparty/miniz/miniz_tinfl.c
	hksrc/src/Thirdparty/miniz/miniz_zip.c
	${EFK_EFFEKSEER_SRC}
	${EFK_MATERIAL_SRC}
	${EFK_RENDERER_COMMON_SRC}
	${EFKBGFX}/renderer/bgfxrenderer.cpp
)

set_target_properties(hacksaw PROPERTIES
	LINKER_LANGUAGE CXX
	RUNTIME_OUTPUT_DIRECTORY "${CMAKE_SOURCE_DIR}/Working"
)

target_include_directories(hacksaw PRIVATE
	hksrc/src
	"${TP}/sokol"
	"${TP}/stb"
	"${TP}/miniz"
	"${TP}/bgfx/include"
	"${TP}/bx/include"
	"${EFK}"
	"${EFK}/Effekseer"
	"${EFK}/EffekseerRendererCommon"
	"${EFK}/EffekseerMaterial"
	"${EFK}/3rdParty/LLGI/src"
	"${EFKBGFX}"
	"${EFKBGFX}/renderer"
)

target_compile_definitions(hacksaw PRIVATE
	__EFFEKSEER_RENDERER_BGFX__
	MaxInstanced=20
	BX_CONFIG_DEBUG=0
)

target_link_libraries(hacksaw PRIVATE
	${BGFX_LIBS}
	opengl32
	gdi32
	user32
)
