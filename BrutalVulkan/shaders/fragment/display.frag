#version 460

layout(location = 0) out vec4 color;

layout(set = 0, binding = 0) uniform sampler2D inputImage;

void main()
{
    vec2 uv = gl_FragCoord.xy / vec2(textureSize(inputImage, 0));
    color = texture(inputImage, uv);
}       