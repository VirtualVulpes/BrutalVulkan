#version 460

layout(push_constant) uniform FragmentPushConstants
{
    layout(offset = 128) uint id;
} pc;

layout(location = 0) out uint outId;

void main()
{
    outId = pc.id;
}