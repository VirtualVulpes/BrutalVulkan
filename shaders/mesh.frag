#version 450

#extension GL_EXT_nonuniform_qualifier : require

layout(set = 0, binding = 0) uniform sampler2D textures[256];

layout(location = 0) in vec2 uv;
layout(location = 0) out vec4 outColor;

layout(push_constant) uniform FragmentPushConstants
{
    layout(offset = 64) uint textureIndex;
} pc;

void main()
{
    outColor = texture(textures[nonuniformEXT(pc.textureIndex)], uv);
}       