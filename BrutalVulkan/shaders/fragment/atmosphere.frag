#version 450

const int MAX_STEPS = 20;

layout(location = 0) out vec4 outColor;

layout(set = 0, binding = 2, std140) uniform CameraBuffer 
{
    mat4 view;
    mat4 proj;
    mat4 invView;
    mat4 invProj;
    
    vec3 pos;
    vec2 invViewport;
} camera;

layout(set = 1, binding = 0) uniform sampler2D inputImage;

layout(push_constant) uniform PushConstants 
{
    vec3 center;
    float radius;
    vec3 lightPos;
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

vec3 sampleAt(vec3 pos, vec3 center, float radius) 
{
    return vec3(0.2);
}

void main()
{
    vec2 pixelPos = 2 * gl_FragCoord.xy * camera.invViewport - 1; // NDC
    pixelPos.y *= -1;

    vec4 clipPos = vec4(pixelPos, 1.0, 1.0); // clip
    
    vec4 viewPos = camera.invProj * clipPos; // view
    viewPos.xyz = viewPos.xyz / viewPos.w;
    
    vec3 worldPos = (camera.invView * viewPos).xyz; // world

    vec3 dir = normalize(worldPos - camera.pos);

    vec2 hit = intersectSphere(camera.pos, dir, pc.center, pc.radius);

    vec4 sceneColor = texture(inputImage, gl_FragCoord.xy * camera.invViewport);
    if (hit.y < 0.0) 
    {
        outColor = sceneColor;
        return;
    } 
       
    vec3 color = vec3(0.0);
    
    float tNear = max(hit.x, 0.0);
    float stepSize = (hit.y - tNear) / float(MAX_STEPS);

    for (int i = 0; i < MAX_STEPS; i++) 
    {
        vec3 pos = camera.pos + dir * (tNear + stepSize * (float(i) + 0.5));
                
        color += sampleAt(pos, pc.center, pc.radius) * stepSize;
    }
    
    color = mix(sceneColor.rgb, color, color);

    outColor = vec4(color, 1.0);
}       