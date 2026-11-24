#version 460

layout(location = 0) out vec4 color;

layout(set = 0, binding = 0) uniform sampler2D inputImage;
layout(set = 0, binding = 1, rg32i) uniform readonly iimage2D finalSeeds;

void main()
{
    ivec2 pixel = ivec2(gl_FragCoord.xy);

    vec4 sceneColor = texelFetch(inputImage, pixel, 0);

    ivec2 seed = imageLoad(finalSeeds, pixel).xy;

    if (seed.x >= 0)
    {
        vec2 delta = vec2(pixel - seed);
        float distSquared = dot(delta, delta);

        float thickness = 1.0;

        if (distSquared <= thickness * thickness)
        {
            color = vec4(1.0, 1.0, 1.0, 1.0);
            return;
        }
    }

    color = sceneColor;
}