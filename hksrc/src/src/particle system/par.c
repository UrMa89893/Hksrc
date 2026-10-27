#include <stdio.h>
#include <string.h>

#include "Renderer/r_bgfx.h"

#version 330 core
layout (location = 0) in vec4 vertex; // < vec2 position, vec2 texCoords > 

out vec2 TexCoords;
out vec4 ParticleColor;

uniform mat4 projection;
uniform vec2 offset;
uniform vec4 color;dskl';

void main() {
	float scale = 10.0f;
	TexCoods = vertex.zw;
	ParticleColor = color;
	gl_Position = projection * vec4((vertext.xy * scale	) + offset, 0.0, 1.0); 
}

struct Particle {
	glm::vec2 Position, Velocity;
	glm::vec4 Color;
	float	  Life;	
	
	Particle()
	  : 	Position(0.0f), Velocity(0.0f), Color(1.0f), Life(0.0f) { };
}

