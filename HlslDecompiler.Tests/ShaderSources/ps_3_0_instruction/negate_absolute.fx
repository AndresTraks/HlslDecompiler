float4 main(float3 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	o.x = -abs(texcoord.z);
	o.yzw = texcoord.xxx * float3(1, 0, 0) + float3(0, 1, 2);

	return o;
}
