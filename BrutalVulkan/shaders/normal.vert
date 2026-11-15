#version 450

layout(location = 0) in vec3 position;
layout(location = 1) in vec3 inNormal;
layout(location = 0) out vec3 normal;

layout(push_constant) uniform VertexPushConstants 
{
    mat4 mvp;
} pc;

void main()
{
    gl_Position = pc.mvp * vec4(position, 1.0);
    normal = inNormal;
}