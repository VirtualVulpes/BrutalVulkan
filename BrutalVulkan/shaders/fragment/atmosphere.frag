#version 450

#define PI 3.14159265359

const int MAX_STEPS = 40;

layout(location = 0) out vec4 outColor;

layout(set = 0, binding = 1, std140) uniform CameraBuffer 
{
    mat4 view;
    mat4 proj;
    mat4 viewProj;
    mat4 invView;
    mat4 invProj;
    vec3 pos;
    vec2 invViewport;
} camera;

layout(set = 1, binding = 0) uniform sampler2D inputImage;
layout(set = 1, binding = 1) uniform sampler2D depthImage;

layout(push_constant) uniform PushConstants 
{
    vec3 center;
    float radius;
    vec3 lightDir;
    uint enabled;
    vec3 lightColor;
    float lightIntensity;
} pc;

vec2 intersectSphere(vec3 origin, vec3 dir, vec3 center, float radius) 
{
    vec3 oc = origin - center;
    
    float b = dot(oc, dir);
    float c = dot(oc, oc) - radius * radius;
    
    float h = b * b - c;
    
    if (h < 0.0) {
        return vec2(-1.0);
    }
    
    float sqrtH = sqrt(h);
    
    return vec2(-b - sqrtH, -b + sqrtH);
}

uint pcg_hash(uint v) {
    uint state = v * 747796405u + 2891336453u;
    uint word = ((state >> ((state >> 28u) + 4u)) ^ state) * 277803737u;
    return (word >> 22u) ^ word;
}

float rand(uvec2 p) {
    uint h = pcg_hash(p.x + pcg_hash(p.y));
    return float(h) * (1.0 / 4294967296.0);
}

float beer(float dist, float absorbCoeff, float scatterCoeff)
{
    return exp(-dist * (absorbCoeff + scatterCoeff));
}

// Henyey-Greenstein phase 
float phase(float g, float cos_theta)
{
    float denom = 1 + g * g - 2 * g * cos_theta;
    return (1 - g * g) / (4.0 * PI * denom * sqrt(denom));
}

void main()
{
    vec4 sceneColor = texture(inputImage, gl_FragCoord.xy * camera.invViewport);
    if (pc.enabled == 0) 
    {
        outColor = sceneColor;
        return;
    }

    float sceneDepth = texelFetch(depthImage, ivec2(gl_FragCoord.xy), 0).r;
    float sceneDist = 1e30;
    if (sceneDepth < 1.0) {
        vec2 ndc = 2.0 * gl_FragCoord.xy * camera.invViewport - 1.0;
        ndc.y *= -1.0;
        
        vec4 v = camera.invProj * vec4(ndc, sceneDepth, 1.0);
        v /= v.w;
        
        vec3 scenePos = (camera.invView * v).xyz;
        sceneDist = length(scenePos - camera.pos);
    }
    
    vec2 pixelPos = 2 * gl_FragCoord.xy * camera.invViewport - 1; // NDC
    pixelPos.y *= -1;

    vec4 clipPos = vec4(pixelPos, 1.0, 1.0); // clip
    
    vec4 viewPos = camera.invProj * clipPos; // view
    viewPos = viewPos / viewPos.w;
    
    vec3 worldPos = (camera.invView * viewPos).xyz; // world

    vec3 dir = normalize(worldPos - camera.pos);

    vec2 hit = intersectSphere(camera.pos, dir, pc.center, pc.radius);
    
    if (hit.y < 0.0) 
    {
        outColor = sceneColor;
        return;
    } 
       
    float transmission = 1;
    vec3 color = vec3(0.0);
    
    float absorbCoeff = 0.5;
    float scatterCoeff = 0.5;
    
    float density = 1.0;
    
    float tNear = max(hit.x, 0.0);
    float tFar = min(hit.y, sceneDist);
    
    if (tFar <= tNear) 
    {
        outColor = sceneColor;
        return;
    }

    float stepSize = (tFar - tNear) / float(MAX_STEPS);

    float g = 0.3;
    
    float jitter = rand(uvec2(gl_FragCoord.xy));
    float extinction = (absorbCoeff + scatterCoeff) * density;
    float stepT = exp(-extinction * stepSize);
    float halfT = exp(-extinction * stepSize * 0.5);

    for (int i = 0; i < MAX_STEPS; i++) 
    {
        vec3 pos = camera.pos + dir * (tNear + stepSize * (float(i) + jitter));
                       
        vec2 lightHit = intersectSphere(pos, -pc.lightDir, pc.center, pc.radius);
        float exitDist = max(lightHit.y, 0.0);
        float li = beer(exitDist * density, absorbCoeff, scatterCoeff);

        float cosTheta = clamp(dot(dir, -pc.lightDir), -1.0, 1.0);

        vec3 inscatter = pc.lightColor * pc.lightIntensity * scatterCoeff * density * phase(g, cosTheta) * li;

        color += transmission * halfT * inscatter * stepSize;
        transmission *= stepT;
        
        if (transmission < 1e-3) break;
    }
    
    //color = mix(sceneColor.rgb, color, color);

    outColor = vec4(sceneColor.rgb * transmission + color, 1.0);
}       