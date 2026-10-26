/* Hacksaw: miniz 3.x expects a CMake-generated export header when vendored
 * as plain sources. Static linking into the exe: exports empty. */
#ifndef MINIZ_EXPORT_H
#define MINIZ_EXPORT_H

#define MINIZ_EXPORT
#define MINIZ_NO_EXPORT

#endif /* MINIZ_EXPORT_H */
