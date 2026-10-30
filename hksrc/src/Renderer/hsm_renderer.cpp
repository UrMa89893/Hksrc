#include "hsm_renderer.h"

#include <algorithm>
#include <cmath>
#include <cstdint>
#include <cctype>
#include <cstddef>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <fstream>
#include <sstream>
#include <string>
#include <unordered_map>
#include <vector>
#include <filesystem>

#include "sokol_app.h"

// The current Hacksaw tree intentionally keeps the renderer independent of
// bgfx. Sokol App creates the desktop GL context and owns the window/input
// lifecycle; this file uses that context for the renderer. This keeps all map
// rendering in one module and makes the eventual sokol_gfx backend swap local.
#if defined(_WIN32)
#  define WIN32_LEAN_AND_MEAN
#  include <windows.h>
#  include <GL/gl.h>
#elif defined(__APPLE__)
#  include <OpenGL/gl3.h>
#else
#  include <GL/gl.h>
#  include <GL/glx.h>
#endif

#define STB_IMAGE_IMPLEMENTATION
#include "stb_image.h"

namespace {

// -----------------------------------------------------------------------------
// Small OpenGL 3.x loader. Sokol App creates the context, but MinGW's OpenGL
// headers only expose the old 1.1 entry points, so modern entry points are
// loaded after the context exists.
// -----------------------------------------------------------------------------
#ifndef GL_ARRAY_BUFFER
#define GL_ARRAY_BUFFER 0x8892
#define GL_ELEMENT_ARRAY_BUFFER 0x8893
#define GL_STATIC_DRAW 0x88E4
#define GL_FLOAT 0x1406
#define GL_FALSE 0
#define GL_TRUE 1
#define GL_VERTEX_SHADER 0x8B31
#define GL_FRAGMENT_SHADER 0x8B30
#define GL_COMPILE_STATUS 0x8B81
#define GL_LINK_STATUS 0x8B82
#define GL_INFO_LOG_LENGTH 0x8B84
#define GL_TEXTURE0 0x84C0
#define GL_TEXTURE1 0x84C1
#define GL_TEXTURE_MIN_FILTER 0x2801
#define GL_TEXTURE_MAG_FILTER 0x2800
#define GL_TEXTURE_WRAP_S 0x2802
#define GL_TEXTURE_WRAP_T 0x2803
#define GL_LINEAR 0x2601
#define GL_REPEAT 0x2901
#define GL_RGB8 0x8051
#define GL_RGBA8 0x8058
#define GL_RGBA 0x1908
#define GL_RGB 0x1907
#define GL_BGRA 0x80E1
#define GL_BGR 0x80E0
#define GL_UNPACK_ALIGNMENT 0x0CF5
#define GL_DEPTH_TEST 0x0B71
#define GL_CULL_FACE 0x0B44
#define GL_BACK 0x0405
#define GL_LEQUAL 0x0203
#define GL_LESS 0x0201
#define GL_DEPTH_COMPONENT24 0x81A6
#endif

#ifndef GLchar
typedef char GLchar;
#endif
#ifndef GLsizeiptr
typedef std::ptrdiff_t GLsizeiptr;
#endif

using PFNGLGENVERTEXARRAYSPROC = void (*)(GLsizei, GLuint*);
using PFNGLBINDVERTEXARRAYPROC = void (*)(GLuint);
using PFNGLDELETEVERTEXARRAYSPROC = void (*)(GLsizei, const GLuint*);
using PFNGLGENBUFFERSPROC = void (*)(GLsizei, GLuint*);
using PFNGLBINDBUFFERPROC = void (*)(GLenum, GLuint);
using PFNGLDELETEBUFFERSPROC = void (*)(GLsizei, const GLuint*);
using PFNGLBUFFERDATAPROC = void (*)(GLenum, GLsizeiptr, const void*, GLenum);
using PFNGLVERTEXATTRIBPOINTERPROC = void (*)(GLuint, GLint, GLenum, GLboolean, GLsizei, const void*);
using PFNGLENABLEVERTEXATTRIBARRAYPROC = void (*)(GLuint);
using PFNGLCREATESHADERPROC = GLuint (*)(GLenum);
using PFNGLSHADERSOURCEPROC = void (*)(GLuint, GLsizei, const GLchar* const*, const GLint*);
using PFNGLCOMPILESHADERPROC = void (*)(GLuint);
using PFNGLGETSHADERIVPROC = void (*)(GLuint, GLenum, GLint*);
using PFNGLGETSHADERINFOLOGPROC = void (*)(GLuint, GLsizei, GLsizei*, GLchar*);
using PFNGLDELETESHADERPROC = void (*)(GLuint);
using PFNGLCREATEPROGRAMPROC = GLuint (*)(void);
using PFNGLATTACHSHADERPROC = void (*)(GLuint, GLuint);
using PFNGLLINKPROGRAMPROC = void (*)(GLuint);
using PFNGLGETPROGRAMIVPROC = void (*)(GLuint, GLenum, GLint*);
using PFNGLGETPROGRAMINFOLOGPROC = void (*)(GLuint, GLsizei, GLsizei*, GLchar*);
using PFNGLDELETEPROGRAMPROC = void (*)(GLuint);
using PFNGLUSEPROGRAMPROC = void (*)(GLuint);
using PFNGLGETUNIFORMLOCATIONPROC = GLint (*)(GLuint, const GLchar*);
using PFNGLUNIFORMMATRIX4FVPROC = void (*)(GLint, GLsizei, GLboolean, const GLfloat*);
using PFNGLUNIFORM1IPROC = void (*)(GLint, GLint);
using PFNGLUNIFORM1FPROC = void (*)(GLint, GLfloat);
using PFNGLUNIFORM2FPROC = void (*)(GLint, GLfloat, GLfloat);
using PFNGLACTIVETEXTUREPROC = void (*)(GLenum);

static PFNGLGENVERTEXARRAYSPROC pglGenVertexArrays;
static PFNGLBINDVERTEXARRAYPROC pglBindVertexArray;
static PFNGLDELETEVERTEXARRAYSPROC pglDeleteVertexArrays;
static PFNGLGENBUFFERSPROC pglGenBuffers;
static PFNGLBINDBUFFERPROC pglBindBuffer;
static PFNGLDELETEBUFFERSPROC pglDeleteBuffers;
static PFNGLBUFFERDATAPROC pglBufferData;
static PFNGLVERTEXATTRIBPOINTERPROC pglVertexAttribPointer;
static PFNGLENABLEVERTEXATTRIBARRAYPROC pglEnableVertexAttribArray;
static PFNGLCREATESHADERPROC pglCreateShader;
static PFNGLSHADERSOURCEPROC pglShaderSource;
static PFNGLCOMPILESHADERPROC pglCompileShader;
static PFNGLGETSHADERIVPROC pglGetShaderiv;
static PFNGLGETSHADERINFOLOGPROC pglGetShaderInfoLog;
static PFNGLDELETESHADERPROC pglDeleteShader;
static PFNGLCREATEPROGRAMPROC pglCreateProgram;
static PFNGLATTACHSHADERPROC pglAttachShader;
static PFNGLLINKPROGRAMPROC pglLinkProgram;
static PFNGLGETPROGRAMIVPROC pglGetProgramiv;
static PFNGLGETPROGRAMINFOLOGPROC pglGetProgramInfoLog;
static PFNGLDELETEPROGRAMPROC pglDeleteProgram;
static PFNGLUSEPROGRAMPROC pglUseProgram;
static PFNGLGETUNIFORMLOCATIONPROC pglGetUniformLocation;
static PFNGLUNIFORMMATRIX4FVPROC pglUniformMatrix4fv;
static PFNGLUNIFORM1IPROC pglUniform1i;
static PFNGLUNIFORM1FPROC pglUniform1f;
static PFNGLUNIFORM2FPROC pglUniform2f;
static PFNGLACTIVETEXTUREPROC pglActiveTexture;

static void* get_proc(const char* name) {
#if defined(_WIN32)
    void* p = reinterpret_cast<void*>(wglGetProcAddress(name));
    if (p && p != reinterpret_cast<void*>(0x1) && p != reinterpret_cast<void*>(0x2) &&
        p != reinterpret_cast<void*>(0x3) && p != reinterpret_cast<void*>(-1)) return p;
    static HMODULE gl = LoadLibraryA("opengl32.dll");
    return gl ? reinterpret_cast<void*>(GetProcAddress(gl, name)) : nullptr;
#elif defined(__APPLE__)
    return nullptr; // macOS branch can use the exported GL3 functions directly.
#else
    return reinterpret_cast<void*>(glXGetProcAddress(reinterpret_cast<const GLubyte*>(name)));
#endif
}

#define LOAD_GL(name) do { p##name = reinterpret_cast<decltype(p##name)>(get_proc(#name)); if (!p##name) { std::fprintf(stderr, "Hacksaw: missing OpenGL function %s\\n", #name); return false; } } while (0)

static bool load_gl3() {
    LOAD_GL(glGenVertexArrays); LOAD_GL(glBindVertexArray); LOAD_GL(glDeleteVertexArrays);
    LOAD_GL(glGenBuffers); LOAD_GL(glBindBuffer); LOAD_GL(glDeleteBuffers); LOAD_GL(glBufferData);
    LOAD_GL(glVertexAttribPointer); LOAD_GL(glEnableVertexAttribArray);
    LOAD_GL(glCreateShader); LOAD_GL(glShaderSource); LOAD_GL(glCompileShader);
    LOAD_GL(glGetShaderiv); LOAD_GL(glGetShaderInfoLog); LOAD_GL(glDeleteShader);
    LOAD_GL(glCreateProgram); LOAD_GL(glAttachShader); LOAD_GL(glLinkProgram);
    LOAD_GL(glGetProgramiv); LOAD_GL(glGetProgramInfoLog); LOAD_GL(glDeleteProgram);
    LOAD_GL(glUseProgram); LOAD_GL(glGetUniformLocation); LOAD_GL(glUniformMatrix4fv);
    LOAD_GL(glUniform1i); LOAD_GL(glUniform1f); LOAD_GL(glUniform2f); LOAD_GL(glActiveTexture);
    return true;
}

struct Vec2 { float x=0, y=0; };
struct Vec3 { float x=0, y=0, z=0; };
static Vec3 operator+(Vec3 a, Vec3 b){return {a.x+b.x,a.y+b.y,a.z+b.z};}
static Vec3 operator-(Vec3 a, Vec3 b){return {a.x-b.x,a.y-b.y,a.z-b.z};}
static Vec3 operator*(Vec3 a,float s){return {a.x*s,a.y*s,a.z*s};}
static float dot(Vec3 a,Vec3 b){return a.x*b.x+a.y*b.y+a.z*b.z;}
static Vec3 cross(Vec3 a,Vec3 b){return {a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x};}
static float len(Vec3 a){return std::sqrt(dot(a,a));}
static Vec3 norm(Vec3 a){float l=len(a); return l>1e-6f?a*(1.0f/l):Vec3{0,1,0};}

// HSM uses editor/world coordinates with Z up, matching the map editor. WinX
// used the same convention after converting to its engine coordinates.
static Vec3 engine_pos(Vec3 p) { return {p.x/32.0f, p.z/32.0f, -p.y/32.0f}; }
static Vec3 engine_dir(Vec3 p) { return norm({p.x,p.z,-p.y}); }

// -----------------------------------------------------------------------------
// Minimal JSON DOM/parser. HSM is deliberately human-readable JSON and the
// renderer only needs arrays/objects/numbers/strings/bools/null.
// -----------------------------------------------------------------------------
struct J {
    enum Type { NUL, BOOL, NUMBER, STRING, ARRAY, OBJECT } type=NUL;
    bool b=false; double n=0; std::string s; std::vector<J> a; std::unordered_map<std::string,J> o;
    const J* get(const char* k) const { auto it=o.find(k); return it==o.end()?nullptr:&it->second; }
    bool is(const char* k) const { return get(k)!=nullptr; }
    float number(float d=0) const { return type==NUMBER?(float)n:d; }
    std::string str(const std::string& d={}) const { return type==STRING?s:d; }
};

class JsonParser {
public:
    explicit JsonParser(const std::string& t):p(t.c_str()),e(t.c_str()+t.size()){}
    bool parse(J& out){ skip(); if(!value(out)) return false; skip(); return p==e; }
private:
    const char* p; const char* e;
    void skip(){while(p<e && (*p==' '||*p=='\n'||*p=='\r'||*p=='\t'))++p;}
    bool value(J& v){skip(); if(p>=e)return false; if(*p=='{')return object(v); if(*p=='[')return array(v); if(*p=='"') {v.type=J::STRING;return string(v.s);} if(*p=='t'){return literal(v,"true",J::BOOL,true);} if(*p=='f'){return literal(v,"false",J::BOOL,false);} if(*p=='n'){return literal(v,"null",J::NUL,false);} return number(v);}
    bool literal(J&v,const char*lit,J::Type t,bool b){size_t n=std::strlen(lit);if(e-p<(ptrdiff_t)n||std::strncmp(p,lit,n))return false;p+=n;v.type=t;v.b=b;return true;}
    bool string(std::string& out){if(*p!='"')return false;++p;out.clear();while(p<e){char c=*p++;if(c=='"')return true;if(c=='\\'){if(p>=e)return false;char q=*p++;switch(q){case '"':out+='"';break;case '\\':out+='\\';break;case '/':out+='/';break;case 'b':out+='\b';break;case 'f':out+='\f';break;case 'n':out+='\n';break;case 'r':out+='\r';break;case 't':out+='\t';break;default:out+=q;break;}}else out+=c;}return false;}
    bool number(J&v){const char*start=p; if(*p=='-')++p;while(p<e&&std::isdigit((unsigned char)*p))++p;if(p<e&&*p=='.'){++p;while(p<e&&std::isdigit((unsigned char)*p))++p;}if(p<e&&(*p=='e'||*p=='E')){++p;if(p<e&&(*p=='+'||*p=='-'))++p;while(p<e&&std::isdigit((unsigned char)*p))++p;}if(start==p)return false;v.type=J::NUMBER;v.n=std::strtod(start,nullptr);return true;}
    bool array(J&v){if(*p!='[')return false;++p;v.type=J::ARRAY;v.a.clear();skip();if(p<e&&*p==']'){++p;return true;}for(;;){J x;if(!value(x))return false;v.a.push_back(std::move(x));skip();if(p<e&&*p==']'){++p;return true;}if(p>=e||*p++!=',')return false;}}
    bool object(J&v){if(*p!='{')return false;++p;v.type=J::OBJECT;v.o.clear();skip();if(p<e&&*p=='}'){++p;return true;}for(;;){skip();std::string k;if(p>=e||!string(k))return false;skip();if(p>=e||*p++!=':')return false;J x;if(!value(x))return false;v.o.emplace(std::move(k),std::move(x));skip();if(p<e&&*p=='}'){++p;return true;}if(p>=e||*p++!=',')return false;}}
};

static Vec3 arr3(const J* j, Vec3 d={}) { if(!j||j->type!=J::ARRAY||j->a.size()<3)return d; return {(float)j->a[0].n,(float)j->a[1].n,(float)j->a[2].n}; }
static Vec2 arr2(const J* j, Vec2 d={}) { if(!j||j->type!=J::ARRAY||j->a.size()<2)return d; return {(float)j->a[0].n,(float)j->a[1].n}; }

struct Vertex { Vec3 p,n; Vec2 uv; };
struct Surface { std::string material; std::vector<Vertex> v; };

static void add_face(Surface& s,const std::vector<Vec3>& pts, Vec3 normal, Vec3 ua, Vec3 va, Vec2 shift, Vec2 scale) {
    if(pts.size()<3) return;
    float sx=std::fabs(scale.x)>1e-6f?scale.x:1.0f, sy=std::fabs(scale.y)>1e-6f?scale.y:1.0f;
    for(size_t k=1;k+1<pts.size();++k){
        size_t ids[3]={0,k,k+1};
        for(int q=0;q<3;++q){
            Vec3 p=pts[ids[q]];
            s.v.push_back({engine_pos(p),engine_dir(normal),{dot(p,ua)/sx+shift.x,dot(p,va)/sy+shift.y}});
        }
    }
}

static void collect_nodes(const J& node, std::unordered_map<std::string,Surface>& surfaces) {
    const J* type=node.get("type"); if(type&&type->type==J::STRING){
        std::string t=type->s;
        if(t=="brush"){
            const J* sides=node.get("sides"); if(sides&&sides->type==J::ARRAY){
                for(const J& side:sides->a){
                    const J* va=side.get("vertices"); if(!va||va->a.size()<3)continue;
                    std::vector<Vec3> pts; for(const J& x:va->a)pts.push_back(arr3(&x));
                    Vec3 n=norm(cross(pts[1]-pts[0],pts[2]-pts[0]));
                    std::string mat=side.get("material")?side.get("material")->str("textures/notex.png"):"textures/notex.png";
                    Vec3 ua=arr3(side.get("uAxis"),{1,0,0}); Vec3 vv=arr3(side.get("vAxis"),{0,0,-1});
                    Vec2 sh=arr2(side.get("shift")); Vec2 sc=arr2(side.get("scale"),{1,1});
                    add_face(surfaces[mat],pts,n,ua,vv,sh,sc); surfaces[mat].material=mat;
                }
            }
        } else if(t=="mesh"){
            const J* verts=node.get("vertices"); const J* polys=node.get("polygons");
            if(verts&&polys&&verts->type==J::ARRAY&&polys->type==J::ARRAY){
                std::vector<Vec3> pool; for(const J& x:verts->a)pool.push_back(arr3(&x));
                std::string mat=node.get("material")?node.get("material")->str("textures/notex.png"):"textures/notex.png";
                Surface& s=surfaces[mat]; s.material=mat;
                for(const J& poly:polys->a){if(poly.a.size()<3)continue; std::vector<Vec3> pts;for(const J& ix:poly.a){int i=(int)ix.n;if(i>=0&&i<(int)pool.size())pts.push_back(pool[i]);}if(pts.size()<3)continue;Vec3 n=norm(cross(pts[1]-pts[0],pts[2]-pts[0]));add_face(s,pts,n,{1,0,0},{0,1,0},{0,0},{1,1});}
            }
        }
    }
    if(const J* ch=node.get("children");ch&&ch->type==J::ARRAY)for(const J& c:ch->a)collect_nodes(c,surfaces);
}

static void collect_scene(const J& root,std::unordered_map<std::string,Surface>& surfaces) {
    if(const J* nodes=root.get("nodes");nodes&&nodes->type==J::ARRAY)for(const J& n:nodes->a)collect_nodes(n,surfaces);
    // Backward-compatible v1 HSM support.
    if(const J* meshes=root.get("meshes");meshes&&meshes->type==J::ARRAY){
        for(const J& m:meshes->a){std::string mat=m.get("material")?m.get("material")->str("textures/notex.png"):"textures/notex.png";Surface&s=surfaces[mat];s.material=mat;if(const J* faces=m.get("faces");faces&&faces->type==J::ARRAY)for(const J& f:faces->a){std::vector<Vec3> pts;for(const J& p:f.a)pts.push_back(arr3(&p));if(pts.size()>=3){Vec3 n=norm(cross(pts[1]-pts[0],pts[2]-pts[0]));add_face(s,pts,n,{1,0,0},{0,1,0},{0,0},{1,1});}}}
    }
}

struct Mat4 { float m[16]; };
static Mat4 identity(){Mat4 r{};r.m[0]=r.m[5]=r.m[10]=r.m[15]=1;return r;}
static Mat4 mul(const Mat4&a,const Mat4&b){Mat4 r{};for(int c=0;c<4;c++)for(int row=0;row<4;row++)for(int k=0;k<4;k++)r.m[c*4+row]+=a.m[k*4+row]*b.m[c*4+k];return r;}
static Mat4 perspective(float fovy,float aspect,float zn,float zf){Mat4 r{};float f=1/std::tan(fovy*0.5f);r.m[0]=f/aspect;r.m[5]=f;r.m[10]=(zf+zn)/(zn-zf);r.m[11]=-1;r.m[14]=(2*zf*zn)/(zn-zf);return r;}
static Mat4 look_at(Vec3 eye,Vec3 center,Vec3 up){Vec3 f=norm(center-eye),s=norm(cross(f,up)),u=cross(s,f);Mat4 r=identity();r.m[0]=s.x;r.m[1]=s.y;r.m[2]=s.z;r.m[4]=u.x;r.m[5]=u.y;r.m[6]=u.z;r.m[8]=-f.x;r.m[9]=-f.y;r.m[10]=-f.z;r.m[12]=-dot(s,eye);r.m[13]=-dot(u,eye);r.m[14]=dot(f,eye);return r;}

struct Camera { Vec3 pos{0,2,5}, front{0,0,-1}, up{0,1,0}; float yaw=-90,pitch=0; };
static Camera cam;
static bool keys[512]{}; static bool mouse_locked=false;
static double last_time=0;

struct GpuSurface { GLuint vao=0,vbo=0,tex=0; GLsizei count=0; float uvx=1.0f/64.0f, uvy=1.0f/64.0f; std::string material; };
static std::vector<GpuSurface> gpu;
static GLuint program=0;
static std::filesystem::path map_path;
static int fbw=0,fbh=0;

static bool compile_shader(GLuint type,const char*src,GLuint& out){out=pglCreateShader(type);pglShaderSource(out,1,&src,nullptr);pglCompileShader(out);GLint ok=0;pglGetShaderiv(out,GL_COMPILE_STATUS,&ok);if(!ok){char log[4096]{};pglGetShaderInfoLog(out,sizeof(log),nullptr,log);std::fprintf(stderr,"Hacksaw shader error: %s\\n",log);return false;}return true;}
static bool make_program(){
    const char* vs=R"GLSL(#version 330 core
layout(location=0) in vec3 aPos; layout(location=1) in vec3 aNormal; layout(location=2) in vec2 aUV;
uniform mat4 uViewProj; out vec3 vN; out vec2 vUV; void main(){vN=aNormal;vUV=aUV;gl_Position=uViewProj*vec4(aPos,1.0);})GLSL";
    const char* fs=R"GLSL(#version 330 core
in vec3 vN; in vec2 vUV; uniform sampler2D uTex; uniform vec2 uUvScale; out vec4 FragColor;
void main(){vec3 n=normalize(vN);float l=max(dot(n,normalize(vec3(-0.35,0.8,0.25))),0.0);vec3 c=texture(uTex,vUV*uUvScale).rgb;FragColor=vec4(c*(0.22+l*0.78),1.0);})GLSL";
    GLuint a=0,b=0;if(!compile_shader(GL_VERTEX_SHADER,vs,a)||!compile_shader(GL_FRAGMENT_SHADER,fs,b))return false;program=pglCreateProgram();pglAttachShader(program,a);pglAttachShader(program,b);pglLinkProgram(program);GLint ok=0;pglGetProgramiv(program,GL_LINK_STATUS,&ok);pglDeleteShader(a);pglDeleteShader(b);if(!ok){char log[4096]{};pglGetProgramInfoLog(program,sizeof(log),nullptr,log);std::fprintf(stderr,"Hacksaw program error: %s\\n",log);return false;}return true;
}

static GLuint load_texture(const std::filesystem::path& p, int* out_w=nullptr, int* out_h=nullptr){
    int w=0,h=0,c=0;stbi_uc* px=stbi_load(p.string().c_str(),&w,&h,&c,4);if(!px){std::fprintf(stderr,"Hacksaw: texture not found: %s\\n",p.string().c_str());return 0;}GLuint t=0;glGenTextures(1,&t);glBindTexture(GL_TEXTURE_2D,t);glPixelStorei(GL_UNPACK_ALIGNMENT,1);glTexImage2D(GL_TEXTURE_2D,0,GL_RGBA8,w,h,0,GL_RGBA,GL_UNSIGNED_BYTE,px);glTexParameteri(GL_TEXTURE_2D,GL_TEXTURE_MIN_FILTER,GL_LINEAR);glTexParameteri(GL_TEXTURE_2D,GL_TEXTURE_MAG_FILTER,GL_LINEAR);glTexParameteri(GL_TEXTURE_2D,GL_TEXTURE_WRAP_S,GL_REPEAT);glTexParameteri(GL_TEXTURE_2D,GL_TEXTURE_WRAP_T,GL_REPEAT);stbi_image_free(px);return t;
}
static std::filesystem::path resolve_asset(const std::string& material){
    std::filesystem::path p=material; if(p.is_absolute()&&std::filesystem::exists(p))return p;
    std::vector<std::filesystem::path> c={map_path.parent_path()/p,std::filesystem::current_path()/p,std::filesystem::current_path()/"Working"/p,std::filesystem::current_path()/"Working"/"Assets"/p};
    for(auto& x:c)if(std::filesystem::exists(x))return x; return {};
}

static void update_camera(float dt){
    float speed=6.0f*(keys[SAPP_KEYCODE_LEFT_SHIFT]||keys[SAPP_KEYCODE_RIGHT_SHIFT]?4.0f:1.0f);Vec3 f=cam.front;Vec3 r=norm(cross(f,cam.up));Vec3 u=cam.up;if(keys[SAPP_KEYCODE_W])cam.pos=cam.pos+f*speed*dt;if(keys[SAPP_KEYCODE_S])cam.pos=cam.pos-f*speed*dt;if(keys[SAPP_KEYCODE_A])cam.pos=cam.pos-r*speed*dt;if(keys[SAPP_KEYCODE_D])cam.pos=cam.pos+r*speed*dt;if(keys[SAPP_KEYCODE_E])cam.pos=cam.pos+u*speed*dt;if(keys[SAPP_KEYCODE_Q])cam.pos=cam.pos-u*speed*dt;
}
static void mouse_look(float dx,float dy){cam.yaw+=(float)dx*0.12f;cam.pitch-=(float)dy*0.12f;cam.pitch=std::clamp(cam.pitch,-89.0f,89.0f);float cy=std::cos(cam.yaw*3.14159265f/180),sy=std::sin(cam.yaw*3.14159265f/180),cp=std::cos(cam.pitch*3.14159265f/180),sp=std::sin(cam.pitch*3.14159265f/180);cam.front=norm({cy*cp,sp,sy*cp});}

} // namespace

bool renderer_init(const char* hsm_path) {
    if(!load_gl3()) return false;

    // Resolve the HSM from the working directory first, then from the
    // executable/project layout. This makes both `Working\\hacksaw.exe Main.hsm`
    // and double-clicking Working\\hacksaw.exe behave sensibly.
    std::filesystem::path requested = hsm_path ? std::filesystem::path(hsm_path) : std::filesystem::path("Main.hsm");
    std::vector<std::filesystem::path> candidates;
    if(requested.is_absolute()) {
        candidates.push_back(requested);
    } else {
        candidates.push_back(std::filesystem::current_path() / requested);
        candidates.push_back(std::filesystem::current_path() / ".." / requested);
        candidates.push_back(std::filesystem::current_path() / ".." / ".." / requested);
    }
    map_path.clear();
    for(const auto& c : candidates) {
        std::error_code ec;
        if(std::filesystem::exists(c, ec) && std::filesystem::is_regular_file(c, ec)) {
            map_path = std::filesystem::absolute(c);
            break;
        }
    }
    if(map_path.empty()) {
        std::fprintf(stderr,"Hacksaw: couldn't open HSM '%s' (cwd: %s)\\n",
            hsm_path ? hsm_path : "Main.hsm", std::filesystem::current_path().string().c_str());
        return false;
    }

    std::ifstream f(map_path);
    if(!f){std::fprintf(stderr,"Hacksaw: couldn't open HSM %s\\n",map_path.string().c_str());return false;}
    std::stringstream ss;ss<<f.rdbuf();std::string json_text=ss.str();J root;JsonParser parser(json_text);
    if(!parser.parse(root)){std::fprintf(stderr,"Hacksaw: invalid HSM JSON: %s\\n",map_path.string().c_str());return false;}
    std::unordered_map<std::string,Surface> surfaces;collect_scene(root,surfaces);if(surfaces.empty()){std::fprintf(stderr,"Hacksaw: HSM contains no renderable geometry\\n");}
    if(!make_program())return false;
    for(auto& [mat,s]:surfaces){GpuSurface g;g.material=mat;g.count=(GLsizei)(s.v.size());pglGenVertexArrays(1,&g.vao);pglGenBuffers(1,&g.vbo);pglBindVertexArray(g.vao);pglBindBuffer(GL_ARRAY_BUFFER,g.vbo);pglBufferData(GL_ARRAY_BUFFER,s.v.size()*sizeof(Vertex),s.v.data(),GL_STATIC_DRAW);pglVertexAttribPointer(0,3,GL_FLOAT,GL_FALSE,sizeof(Vertex),(void*)offsetof(Vertex,p));pglEnableVertexAttribArray(0);pglVertexAttribPointer(1,3,GL_FLOAT,GL_FALSE,sizeof(Vertex),(void*)offsetof(Vertex,n));pglEnableVertexAttribArray(1);pglVertexAttribPointer(2,2,GL_FLOAT,GL_FALSE,sizeof(Vertex),(void*)offsetof(Vertex,uv));pglEnableVertexAttribArray(2);auto ap=resolve_asset(mat);int tw=64,th=64;g.tex=ap.empty()?0:load_texture(ap,&tw,&th);g.uvx=1.0f/(float)std::max(1,tw);g.uvy=1.0f/(float)std::max(1,th);gpu.push_back(g);}
    // Center the initial camera on the complete scene bounds rather than on a
    // player/entity. This is deliberately a renderer-only fly-through viewer.
    Vec3 mn{1e9f,1e9f,1e9f},mx{-1e9f,-1e9f,-1e9f};
    for(const auto& s:surfaces)for(const auto& v:s.second.v){mn.x=std::min(mn.x,v.p.x);mn.y=std::min(mn.y,v.p.y);mn.z=std::min(mn.z,v.p.z);mx.x=std::max(mx.x,v.p.x);mx.y=std::max(mx.y,v.p.y);mx.z=std::max(mx.z,v.p.z);} 
    if(mn.x<mx.x){Vec3 c=(mn+mx)*0.5f;float rad=len(mx-mn)*0.5f;cam.pos=c+Vec3{0,rad*0.15f,rad*1.25f};cam.front=norm(c-cam.pos);cam.yaw=std::atan2(cam.front.z,cam.front.x)*180.0f/3.14159265f;cam.pitch=std::asin(cam.front.y)*180.0f/3.14159265f;
        std::fprintf(stderr,"Hacksaw: scene bounds min=(%.2f %.2f %.2f) max=(%.2f %.2f %.2f), camera=(%.2f %.2f %.2f)\n",mn.x,mn.y,mn.z,mx.x,mx.y,mx.z,cam.pos.x,cam.pos.y,cam.pos.z);
    }
    fbw = sapp_width();
    fbh = sapp_height();
    if (fbw <= 0) fbw = 1;
    if (fbh <= 0) fbh = 1;
    glEnable(GL_DEPTH_TEST);glDisable(GL_CULL_FACE);glClearColor(0.08f,0.10f,0.13f,1.0f);return true;
}

void renderer_frame(float dt){
    if(!program) {
        // Initialization failed; leave the window alive for diagnostics.
        glViewport(0,0,sapp_width(),sapp_height());
        glClearColor(0.18f,0.03f,0.03f,1.0f);
        glClear(GL_COLOR_BUFFER_BIT|GL_DEPTH_BUFFER_BIT);
        return;
    }
    if(fbw<=0||fbh<=0){fbw=sapp_width();fbh=sapp_height();if(fbw<=0)fbw=1;if(fbh<=0)fbh=1;}
    update_camera(dt);glViewport(0,0,fbw,fbh);glClear(GL_COLOR_BUFFER_BIT|GL_DEPTH_BUFFER_BIT);Mat4 p=perspective(70.0f*3.14159265f/180.0f,(float)fbw/(float)fbh,0.03f,4096.0f);Mat4 v=look_at(cam.pos,cam.pos+cam.front,cam.up);Mat4 vp=mul(p,v);pglUseProgram(program);GLint loc=pglGetUniformLocation(program,"uViewProj");pglUniformMatrix4fv(loc,1,GL_FALSE,vp.m);pglUniform1i(pglGetUniformLocation(program,"uTex"),0);GLint uvLoc=pglGetUniformLocation(program,"uUvScale");for(auto&g:gpu){pglActiveTexture(GL_TEXTURE0);glBindTexture(GL_TEXTURE_2D,g.tex);pglUniform2f(uvLoc,g.uvx,g.uvy);pglBindVertexArray(g.vao);glDrawArrays(GL_TRIANGLES,0,g.count);}pglBindVertexArray(0);
}

void renderer_event(const void* raw){
    const sapp_event* e=(const sapp_event*)raw;if(!e)return;
    if(e->type==SAPP_EVENTTYPE_RESIZED){fbw=e->framebuffer_width;fbh=e->framebuffer_height;}
    if(e->type==SAPP_EVENTTYPE_KEY_DOWN){int k=(int)e->key_code;if(k>=0&&k<512)keys[k]=true;if(k==SAPP_KEYCODE_ESCAPE){mouse_locked=false;sapp_lock_mouse(false);}if(k==SAPP_KEYCODE_F1){mouse_locked=!mouse_locked;sapp_lock_mouse(mouse_locked);}}
    if(e->type==SAPP_EVENTTYPE_KEY_UP){int k=(int)e->key_code;if(k>=0&&k<512)keys[k]=false;}
    if(e->type==SAPP_EVENTTYPE_MOUSE_MOVE&&mouse_locked)mouse_look((float)e->mouse_dx,(float)e->mouse_dy);
}

void renderer_shutdown(){for(auto&g:gpu){if(g.tex)glDeleteTextures(1,&g.tex);if(g.vbo)pglDeleteBuffers(1,&g.vbo);if(g.vao)pglDeleteVertexArrays(1,&g.vao);}gpu.clear();if(program)pglDeleteProgram(program);program=0;}
