float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float3 r0;
	r0.x = (texcoord.x >= 0) ? -1 : 0;
	r0.y = texcoord.y * 3;
	r0.z = texcoord.z + texcoord.z;
	o.xyz = (r0.xxx != 0) ? r0.yyy : r0.zzz;
	o.w = texcoord.w;

	return o;
}
