float4 k;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float2 r0;
	r0.x = texcoord.y + k.z;
	o.x = -(r0.x) + texcoord.x;
	r0.x = dot(k.yz, texcoord.yz);
	o.w = k.x * texcoord.x + -(r0.x);
	r0 = texcoord.yy + -(k.zw);
	o.y = -(r0.x) + texcoord.x;
	o.z = r0.y + texcoord.x;

	return o;
}
