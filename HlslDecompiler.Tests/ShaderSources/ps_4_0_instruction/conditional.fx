float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	int3 r0;
	r0.x = (texcoord.x >= 0) ? -1 : 0;
	r0.y = asint(texcoord.y * 3);
	r0.z = asint(texcoord.z + texcoord.z);
	o.xyz = (r0.xxx != 0) ? asfloat(r0.yyy) : asfloat(r0.zzz);
	o.w = texcoord.w;

	return o;
}
