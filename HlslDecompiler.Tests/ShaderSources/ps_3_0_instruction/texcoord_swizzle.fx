float4 main(float3 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	o = texcoord.yzxy * float4(1, 1, 1, 0) + float4(0, 0, 0, 3);

	return o;
}
