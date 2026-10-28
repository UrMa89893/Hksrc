#include <stdio.h>
#include <string.h>

#include "Renderer/r_bgfx.h"
#include "src/particle system/par.c"

void game::Init()
{
	[...]
	ResourceManager::LoadShader("Shaders/particle.vs", "shaders/particle.frag", nullptr, "particle");
	[...]
	ResourceManager::LoadTexture("texture/particle.png", true, "particle");
	[...]
	Particles = new ParticleGenerator(
		ResourceManager::GetShader("particle")
		ResourceManager::GetTexture("particle")
		500
	);
}

void Game::Update(float dt)
{
	[...]
	// update particles
	Particles->Update(dt, *ball 2, glm::vec2(Ball->Redius / 2.0f));
	[...]
}

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

#endif