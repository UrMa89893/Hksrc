#include <stdio.h>
#include <string.h>

#include "Renderer/r_bgfx.h"

#version 330 core
layout (location = 0) in vec4 vertex; // < vec2 position, vec2 texCoords > 
/*
*	vec2 = is the position of the particles 
*	vec4 = the colour of the particle 
*	(note) should add support for images and test weather this works or not :|
*/ 

void int Test() {
	printf("Particle system script running(Maybe)");
	return -1;
}

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

unsigned int nr_particles = 500;
std::vertex<Particle> particles;

for (unsigned int i = 0; i < nr_particles; ++i)
	particles.push_back(Particle());

unsigned int nr_new_particles = 2;
// add new particles
for (unsigned int i = 0; i < nr_new_particles; ++i)
{ // for assignment for new particle 
	int unusedParticle = FirstUnusedParticle();
	ResspawnParticle(particles[unusedParticle], object, offset);
}
for (unsigned int i = 0; i < nr_particles; ++i)
{
	Particle &p = particles[i];
	p.Life -= dt; // reduce life 
	if (p.Life >  0.0f)
	{	// particle is alive thus this should work :)
		p.Position -= p.Velocity * dt;
		p.Color.a -= dt * 2.5f;
	}
}
