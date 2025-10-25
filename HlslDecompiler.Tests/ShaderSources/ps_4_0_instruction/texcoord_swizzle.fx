float4 main(float3 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	o.xyz = texcoord.yzx;
	o.w = 3;

	return o;
}
