struct PS_IN
{
	float4 texcoord : TEXCOORD;
	nointerpolation uint idx : IDX;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	int3 r0;
	float3 r1;
	r0.x = i.idx & 1;
	r0.y = i.idx * 3;
	r0.z = (7 < (uint)r0.y) ? -1 : 0;
	r1 = (r0.zzz != 0) ? i.texcoord.xyz : i.texcoord.wzy;
	o.x = (r0.x != 0) ? asfloat(r0.y) : r1.x;
	o.yz = r1.yz;
	o.w = (float)(uint)r0.y;

	return o;
}
