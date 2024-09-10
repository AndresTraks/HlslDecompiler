float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	o.w = abs(texcoord.w);
	o.xyz = texcoord.yxy * float3(-1, -1, 0) + float3(0, 0, 2);

	return o;
}
