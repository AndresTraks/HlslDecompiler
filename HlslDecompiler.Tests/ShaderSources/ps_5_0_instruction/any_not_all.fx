float4 k;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	int4 r0;
	float3 r1;
	r0.xyz = (k.xyz < texcoord.xyz) ? -1 : 0;
	r0.w = r0.y & r0.x;
	r0.w = r0.z & r0.w;
	r0.w = ~r0.w;
	r0.x = r0.y | r0.x;
	r0.x = r0.z | r0.x;
	r0.x = r0.w & r0.x;
	r0.yzw = asint(max(texcoord.xyz, k.xyz));
	r1 = asfloat(r0.yzw) + asfloat(r0.yzw);
	o.xyz = (r0.xxx != 0) ? r1.xyz : asfloat(r0.yzw);
	r0.x = asint(texcoord.w) & 2147483647;
	r0.x = (r0.x == 2139095040) ? -1 : 0;
	o.w = (r0.x != 0) ? 1 : texcoord.w;

	return o;
}
