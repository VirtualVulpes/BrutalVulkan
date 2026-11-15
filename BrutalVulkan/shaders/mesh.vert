#version 450

layout(location = 0) in vec3 position;
layout(location = 1) in vec3 normal;
layout(location = 2) in vec2 inUv;
layout(location = 0) out vec2 uv;

layout(push_constant) uniform VertexPushConstants 
{
    mat4 mvp;
} pc;

void main()
{
    gl_Position = pc.mvp * vec4(position, 1.0);
    uv = inUv;
}