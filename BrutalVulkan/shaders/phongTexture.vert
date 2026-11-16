#version 450

layout(location = 0) in vec3 position;
layout(location = 1) in vec3 inNormal;
layout(location = 2) in vec2 inUv;
layout(location = 0) out vec2 uv;
layout(location = 1) out vec3 worldNormal;
layout(location = 2) out vec3 worldPosition;

layout(push_constant) uniform VertexPushConstants 
{
    mat4 mvp;
    mat4 model;
} pc;

void main()
{
    gl_Position = pc.mvp * vec4(position, 1.0);
    worldPosition = (pc.model * vec4(position, 1.0)).xyz;
    uv = inUv;
    worldNormal = normalize(transpose(inverse(mat3(pc.model))) * inNormal);
}