float3 eyeDir : register(c1);
float3 lightDir;
float shininess : register(c2);

struct PS_IN
{
	float3 texcoord : TEXCOORD;
	float3 texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : COLOR
{
	float3 t0 = normalize(i.texcoord);
	float3 t1 = cross(t0, i.texcoord1);
	float t2 = dot(eyeDir, t0);
	float t3 = (t2 >= 0 ? 0 : -1) + (-t2 >= 0 ? 0 : 1);
	float t4 = dot(lightDir, t0);
	float t5 = -t4 >= 0 ? 0 : 1;
	float t6 = dot(t0, normalize(lightDir + eyeDir));
	float t7 = (-t6 >= 0 ? 0 : t5) * pow(t6, shininess);
	float t8 = t7 * t7;
	return t8 * t3 + float4(1, dot(t1, t1) / length(t1) * t4 * t5, t7, 1);
}
