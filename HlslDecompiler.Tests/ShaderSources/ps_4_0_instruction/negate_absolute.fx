float4 main(float3 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	o.x = -(abs(texcoord.z));
	o.y = texcoord.x;
	o.zw = float2(0, 0);

	return o;
}
