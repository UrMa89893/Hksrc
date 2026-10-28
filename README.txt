For the editor we can use, we can use trenchbroom if we want a more Quake style
https://netradiant.gitlab.io/

Okay to build the engine and stuff here

Configure CMake (only needed once, or after CMakeLists.txt changes)
cmake -B build -G "MinGW Makefiles"

Build the engine
cmake --build build -j

This compiles all the C code and links Working\hacksaw.exe.
Run this every time you change engine code — it's incremental, so only changed files recompile (takes seconds).