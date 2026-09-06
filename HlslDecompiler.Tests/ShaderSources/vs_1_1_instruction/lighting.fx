float4 ambient : register(c3);
float3 halfVector : register(c1);
float3 lightDir;
float specularPower : register(c2);

float4 main(float4 normal : NORMAL) : POSITION
{
	float4 o;

	float4 r0;
	r0.x = dot(normal.xyz, lightDir.xyz);
	r0.y = dot(normal.xyz, halfVector.xyz);
	r0.zw = specularPower.xx;
	r0 = lit(r0.x, r0.y, r0.w);
	o = ambient * r0.y + r0.z;

	return o;
}
