cmake_minimum_required(VERSION 3.16)
project(hacksaw C CXX)

set(CMAKE_C_STANDARD 11)
set(CMAKE_C_STANDARD_REQUIRED ON)
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

set(TP "${CMAKE_SOURCE_DIR}/hksrc/src/Thirdparty")

# bgfx/bx/bimg are built once with their own genie+makefiles (MinGW GCC):
#   cd hksrc/src/Thirdparty/bgfx
#   ../bx/tools/bin/windows/genie.exe --gcc=mingw-gcc gmake
#   mingw32-make -C .build/projects/gmake-mingw-gcc config=release64 -j
# then the static libs are linked here.
# genie (run from the bgfx dir) puts ALL libs in bgfx/.build, with a "Release" suffix
set(BGFX_LIBS
	"${TP}/bgfx/.build/win64_mingw-gcc/bin/libbgfxRelease.a"
	"${TP}/bgfx/.build/win64_mingw-gcc/bin/libbimgRelease.a"
	"${TP}/bgfx/.build/win64_mingw-gcc/bin/libbxRelease.a"
)

add_executable(hacksaw
	hksrc/src/Platform/main.c
	hksrc/src/Renderer/r_bgfx.c
	hksrc/src/Thirdparty/miniz/miniz.c
	hksrc/src/Thirdparty/miniz/miniz_tdef.c
	hksrc/src/Thirdparty/miniz/miniz_tinfl.c
	hksrc/src/Thirdparty/miniz/miniz_zip.c
)

# engine sources are C, but bgfx is C++: link with the C++ driver
set_target_properties(hacksaw PROPERTIES LINKER_LANGUAGE CXX)

target_include_directories(hacksaw PRIVATE
	hksrc/src
	"${TP}/sokol"
	"${TP}/stb"
	"${TP}/miniz"
	"${TP}/bgfx/include"
	"${TP}/bx/include"
)

target_link_libraries(hacksaw PRIVATE
	${BGFX_LIBS}
	opengl32
	gdi32
	user32
)
