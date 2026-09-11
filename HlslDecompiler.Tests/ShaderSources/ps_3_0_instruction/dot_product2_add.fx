float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	o.x = dot(texcoord.yz, texcoord.zw) + 1;
	o.yzw = float3(2, 3, 4);

	return o;
}
