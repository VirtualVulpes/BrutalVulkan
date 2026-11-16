#version 450

#extension GL_EXT_nonuniform_qualifier : require

struct GpuLight {
    vec3 position;
    vec3 color;
    float intensity;
};

layout(set = 0, binding = 0) uniform sampler2D textures[256];
layout(std430, set = 0, binding = 1) readonly buffer LightBuffer 
{ 
    uint lightCount;
    GpuLight lights[]; 
} lightBuffer;

layout(location = 0) in vec2 uv;
layout(location = 1) in vec3 worldNormal;
layout(location = 2) in vec3 worldPosition;
layout(location = 0) out vec4 outColor;


layout(push_constant) uniform FragmentPushConstants
{
    layout(offset = 128) uint textureIndex;
    vec3 cameraPosition;
} pc;

void main()
{
    vec3 N = normalize(worldNormal);
    vec3 V = normalize(pc.cameraPosition - worldPosition);
    
    vec3 albedo = texture(textures[nonuniformEXT(pc.textureIndex)], uv).rgb;

    vec3 ambient = 0.1 * albedo;
    vec3 diffuse = vec3(0.0);
    vec3 specular = vec3(0.0);
    
    for (uint i = 0; i < lightBuffer.lightCount; i++) {
        vec3 lightColor = lightBuffer.lights[i].color.rgb * lightBuffer.lights[i].intensity;
        vec3 L = normalize(lightBuffer.lights[i].position.xyz - worldPosition);
                
        float diff = max(dot(N, L), 0.0);
        diffuse += diff * lightColor;
        
        if (diff > 0.0) {
            vec3 R = reflect(-L, N);
            float spec = pow(max(dot(V, R), 0.0), 64.0);
            
            specular += 0.5 * spec * lightColor;
        }
    }
   
    vec3 result = (ambient + diffuse + specular) * albedo;
    
    outColor = vec4(result, 1.0);
}       