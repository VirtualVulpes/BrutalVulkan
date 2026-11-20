#version 450

layout(location = 0) out vec4 outColor;

layout(push_constant) uniform PushConstants 
{
    layout(offset = 128) vec3 cameraPos;
    vec3 color;
} pc;

void main()
{
    outColor = vec4(pc.color, 1.0);
}       