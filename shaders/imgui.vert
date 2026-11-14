#version 450

layout(location = 0) in vec2 inPosition;
layout(location = 1) in vec2 inUv;
layout(location = 2) in vec4 inColor;

layout(location = 0) out vec2 uv;
layout(location = 1) out vec4 color;

layout(push_constant) uniform PushConstants
{
    vec2 scale;
    vec2 translate;
} pc;

void main()
{   
    gl_Position = vec4(inPosition * pc.scale + pc.translate, 0.0, 1.0);
    
    uv = inUv;
    color = inColor;
}