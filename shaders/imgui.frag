#version 450

layout(set = 0, binding = 0) uniform sampler2D fontTexture;

layout(location = 0) in vec2 uv;
layout(location = 1) in vec4 inColor;
layout(location = 0) out vec4 color;

void main()
{
    color = inColor * texture(fontTexture, uv);
}       