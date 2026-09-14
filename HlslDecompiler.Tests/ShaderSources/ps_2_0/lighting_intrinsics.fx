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
	float3 t0 = normalize(i.texcoord).yzx * i.texcoord1.zxy - normalize(i.texcoord).zxy * i.texcoord1.yzx;
	float t2 = dot(eyeDir, normalize(i.texcoord).xyz);
	float t3 = (t2 >= 0 ? 0 : -1) + (-t2 >= 0 ? 0 : 1);
	float t4 = dot(lightDir, normalize(i.texcoord).xyz);
	float t5 = -t4 >= 0 ? 0 : 1;
	float t6 = dot(normalize(i.texcoord).xyz, normalize(lightDir + eyeDir).xyz);
	float t7 = (-t6 >= 0 ? 0 : t5) * pow(t6, shininess);
	float t1 = t7 * t7;
	return float4(t1 * t3 + 1, t1 * t3 + dot(t0, t0) / length(t0) * t4 * t5, t1 * t3 + t7, t1 * t3 + 1);
}
