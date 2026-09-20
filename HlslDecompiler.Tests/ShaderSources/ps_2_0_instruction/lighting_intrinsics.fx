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
	float4 o;

	float4 r0;
	float4 r1;
	float3 r2;
	float3 r3;
	r0.z = 1;
	r1.xyz = normalize(i.texcoord.xyz);
	r2 = r1.zxy * i.texcoord1.yzx;
	r2 = r1.yzx * i.texcoord1.zxy + -r2.xyz;
	r1.w = dot(r2.xyz, r2.xyz);
	r0.x = rsqrt(r1.w);
	r0.y = r0.x * r1.w;
	r2 = lightDir.xyz;
	r2 = r2.xyz + eyeDir.xyz;
	r3 = normalize(r2.xyz);
	r1.w = dot(r1.xyz, r3.xyz);
	r0.x = dot(r1.xyz, lightDir.xyz);
	r0.w = dot(r1.xyz, eyeDir.xyz);
	r1.x = (-r0.x >= 0) ? 0 : 1;
	r1.y = r0.x * r1.x;
	r0.x = (-r1.w >= 0) ? 0 : r1.x;
	r2.x = pow(r1.w, shininess.x);
	r1.z = r0.x * r2.x;
	r0.x = r1.z * r1.z;
	r1.yz = r0.yz * r1.yz;
	r1.x = 1;
	r1.w = (-r0.w >= 0) ? 0 : 1;
	r0.y = (r0.w >= 0) ? -0 : -1;
	r1.w = r0.y + r1.w;
	r0.xyz = r0.xxx * r1.www + r1.xyz;
	r0.w = r0.x;
	o = r0;

	return o;
}
