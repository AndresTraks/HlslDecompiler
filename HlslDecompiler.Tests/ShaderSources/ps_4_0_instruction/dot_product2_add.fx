float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float r0;
	r0 = dot(texcoord.yz, texcoord.zw);
	o.x = r0.x + 1;
	o.yzw = float3(0, 2, 3);

	return o;
}
