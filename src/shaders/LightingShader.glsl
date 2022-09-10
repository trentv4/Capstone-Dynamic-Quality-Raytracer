#version 430 core
out vec2 uv;

void main() {
	float x = -1.0 + float((gl_VertexID & 1) << 2);
	float y = -1.0 + float((gl_VertexID & 2) << 1);
	uv.x = (x + 1.0) * 0.5;
	uv.y = (y + 1.0) * 0.5;
	gl_Position = vec4(x, y, 0, 1);
}

<split>

#version 430 core
in vec2 uv;

uniform sampler2D gPosition; 
uniform sampler2D gNormal; 
uniform sampler2D gAlbedoSpec; 
uniform vec3 cameraPosition;

layout (std430, binding = 0) buffer Lights {
	float count;
	float rawBuffer[];
};

out vec4 FragColor;

void main() {
	vec4 gPositionVec = texture(gPosition, uv);
	vec4 gNormalVec = texture(gNormal, uv);
	vec4 gAlbedoSpecVec = texture(gAlbedoSpec, uv);

	vec3 xyz = gPositionVec.xyz;
	vec3 normal = gNormalVec.xyz;
	vec3 albedo = gAlbedoSpecVec.xyz;
	float ao = gPositionVec.w;
	float height = gNormalVec.w;
	float gloss = gAlbedoSpecVec.w;

	vec3 HDR = vec3(0);
	// Ambient
	HDR += 0.1 * albedo;

	for(int i = 0; i < count * 10; i += 10) {
		vec3 position = vec3(rawBuffer[i+0], rawBuffer[i+1], rawBuffer[i+2]);
		vec3 color = vec3(rawBuffer[i+3], rawBuffer[i+4], rawBuffer[i+5]);
		vec3 direction = vec3(rawBuffer[i+6], rawBuffer[i+7], rawBuffer[i+8]);
		float strength = rawBuffer[i+9];
		
		float dist = distance(position, xyz) / 4;

		float attenuation = 1 / (1 + (-0.5 * dist) + (8 * pow(dist, 5)));

		vec3 lightDirection = normalize(position - xyz);
		vec3 viewDirection = normalize(cameraPosition - xyz);
		vec3 halfwayDirection = normalize(lightDirection + viewDirection);

		vec3 result = vec3(0);
		// Diffuse
		result += max(dot(normal, lightDirection), 0.0) * albedo * ao * color;
		// Specularity
		result += gloss * color * pow(max(dot(normal, halfwayDirection), 0.0), 32);
		// Strength
		result *= strength;
		// Attenuation
		result *= attenuation;
		// Spotlight attenuation
		if(direction != vec3(0)) {
		//	result *= max(pow(dot(lightDirection, normalize(direction)), 8), 0);
		}

		HDR += result;
	}

	FragColor = vec4(HDR, 1.0);
}