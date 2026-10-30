cmake_minimum_required(VERSION 3.16)
project(hacksaw C CXX)

set(CMAKE_C_STANDARD 11)
set(CMAKE_C_STANDARD_REQUIRED ON)
set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

set(TP "${CMAKE_SOURCE_DIR}/hksrc/src/Thirdparty")

add_executable(hacksaw
    hksrc/src/Platform/main.cpp
    hksrc/src/Renderer/hsm_renderer.cpp
    hksrc/src/Thirdparty/miniz/miniz.c
    hksrc/src/Thirdparty/miniz/miniz_tdef.c
    hksrc/src/Thirdparty/miniz/miniz_tinfl.c
    hksrc/src/Thirdparty/miniz/miniz_zip.c
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
)

if (WIN32)
    target_link_libraries(hacksaw PRIVATE opengl32 gdi32 user32 shell32)
elseif(UNIX AND NOT APPLE)
    target_link_libraries(hacksaw PRIVATE GL X11 Xi Xcursor Xfixes dl m pthread)
endif()
