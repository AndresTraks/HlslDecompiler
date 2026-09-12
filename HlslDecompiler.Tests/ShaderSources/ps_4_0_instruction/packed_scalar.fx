float3 lightDir;
float3 viewDir;
float power;
float4 diffuse;
float4 spec;

float4 main(float3 normal : NORMAL) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	r0.xyz = -(lightDir.xyz) + viewDir.xyz;
	r0.w = dot(r0.xyz, r0.xyz);
	r0.w = 1 / sqrt(r0.w);
	r0.xyz = r0.www * r0.xyz;
	r0.w = dot(normal.xyz, normal.xyz);
	r0.w = 1 / sqrt(r0.w);
	r1.xyz = r0.www * normal.xyz;
	r0.x = saturate(dot(r1.xyz, r0.xyz));
	r0.y = saturate(dot(r1.xyz, -(lightDir.xyz)));
	r0.x = log2(r0.x);
	r0.x = r0.x * power;
	r0.x = exp2(r0.x);
	r1 = r0.x * spec;
	o = diffuse * r0.y + r1;

	return o;
}
