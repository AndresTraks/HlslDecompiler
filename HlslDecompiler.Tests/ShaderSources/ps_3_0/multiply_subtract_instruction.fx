float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	o.w = abs(texcoord.w);
	o.xyz = texcoord.xxy * float3(0, 2, 2) + float3(3, -1, -1);

	return o;
}
