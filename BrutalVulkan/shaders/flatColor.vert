#version 450

layout(location = 0) in vec3 position;

layout(push_constant) uniform VertexPushConstants 
{
    mat4 mvp;
    mat4 model;
} pc;

void main()
{
    gl_Position = pc.mvp * vec4(position, 1.0);
}