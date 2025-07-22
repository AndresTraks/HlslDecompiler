float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 o;
	
	float r0;
	r0.x = 3 * texcoord.x;
	o.w = abs(r0.x);
	o.xyz = texcoord.xwx * float3(3, 3, 0) + float3(-1, -1, 8);
	
	return o;
}
