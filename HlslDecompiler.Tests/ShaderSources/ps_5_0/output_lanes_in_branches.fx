struct PS_IN
{
	float4 texcoord : TEXCOORD;
	nointerpolation uint idx : IDX;
};

float4 main(PS_IN i) : SV_Target
{
	float t0;
	float3 t1;
	if (i.idx == 0) {
		t0 = i.texcoord.x;
		t1 = i.texcoord.yzw;
	} else {
		t0 = -i.texcoord.x;
		t1 = -i.texcoord.yzw;
	}
	return float4(t0 + i.texcoord.y, t1);
}
