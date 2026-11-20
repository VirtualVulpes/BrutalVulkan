#version 450

const int LIGHT_POINT = 0;
const int LIGHT_DIRECTIONAL = 1;
const int LIGHT_SPOT = 2;

struct GpuLight {
    vec3 position;
    float intensity;
    vec3 color;
    float range;
    vec3 direction;
    int type;
    float innerCos;
    float outerCos;
};

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
    layout(offset = 128) vec3 cameraPosition;
    vec3 color;
} pc;

void main()
{
    vec3 N = normalize(worldNormal);
    vec3 V = normalize(pc.cameraPosition - worldPosition);
    
    vec3 albedo = pc.color;

    vec3 ambient = 0.1 * albedo;
    vec3 diffuse = vec3(0.0);
    vec3 specular = vec3(0.0);
    
    for (uint i = 0; i < lightBuffer.lightCount; i++) 
    {
        GpuLight light = lightBuffer.lights[i];
        
        vec3 L;
        float atten = 1.0;
        
        if (light.type == LIGHT_DIRECTIONAL) 
        {
            L = normalize(-light.direction);
        } else 
        {
            vec3 toLight = light.position - worldPosition;
            float distSq = dot(toLight, toLight);
            if (light.range * light.range <= distSq) continue;

            L = toLight * inversesqrt(distSq);

            float r2 = (distSq / (light.range * light.range));
            float window = clamp(1.0 - r2 * r2, 0.0, 1.0);
            atten = window * window / max(distSq, 0.0001);
            
            if (light.type == LIGHT_SPOT) 
            {
                float cd = dot(-L, normalize(light.direction));
                float t = clamp((cd - light.outerCos) / max(light.innerCos - light.outerCos, 1e-4), 0.0, 1.0);
                atten *= t * t * (3.0 - 2.0 * t);
            }
        }
                
        vec3 lightColor = light.color * light.intensity * atten;
                
        float diff = max(dot(N, L), 0.0);
        diffuse += diff * lightColor;
        
        if (diff > 0.0) 
        {
            vec3 H = normalize(L + V);
            float spec = pow(max(dot(N, H), 0.0), 64.0);
            
            specular += 0.5 * spec * lightColor;
        }
    }
   
    vec3 result = ambient + diffuse * albedo + specular;
    
    outColor = vec4(result, 1.0);
}       