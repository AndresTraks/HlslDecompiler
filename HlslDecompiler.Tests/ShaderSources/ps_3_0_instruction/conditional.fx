float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float2 r0;
	r0.x = 3 * texcoord.y;
	r0.y = texcoord.z + texcoord.z;
	o.xyz = (texcoord.xxx >= 0) ? r0.xxx : r0.yyy;
	o.w = texcoord.w;

	return o;
}
