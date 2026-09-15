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
	float t1 = dot(eyeDir, normalize(i.texcoord).xyz);
	float t2 = (t1 >= 0 ? 0 : -1) + (-t1 >= 0 ? 0 : 1);
	float t3 = dot(lightDir, normalize(i.texcoord).xyz);
	float t4 = -t3 >= 0 ? 0 : 1;
	float t5 = dot(normalize(i.texcoord).xyz, normalize(lightDir + eyeDir).xyz);
	float t6 = (-t5 >= 0 ? 0 : t4) * pow(t5, shininess);
	float t7 = t6 * t6;
	return t7 * t2 + float4(1, dot(t0, t0) / length(t0) * t3 * t4, t6, 1);
}
