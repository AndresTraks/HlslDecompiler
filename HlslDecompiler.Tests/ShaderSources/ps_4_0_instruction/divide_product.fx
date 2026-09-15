float4 k;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float2 r0;
	r0.x = texcoord.y * k.x;
	o.x = texcoord.x / r0.x;
	r0.x = texcoord.y / k.y;
	o.y = texcoord.x / r0.x;
	r0.x = texcoord.z / texcoord.w;
	o.z = r0.x * k.z;
	r0 = texcoord.yw * texcoord.xz;
	o.w = r0.x / r0.y;

	return o;
}
