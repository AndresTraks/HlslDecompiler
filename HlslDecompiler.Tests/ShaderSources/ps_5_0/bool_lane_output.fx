cbuffer c : register(b0)
{
	float4 v;
	float4 d;
};

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 t0 = texcoord / d;
	bool4 t1 = isinf(t0) || isnan(t0);
	float4 t2 = t1 ? v : t0;
	return float4(t1.w || t1.y || t1.z || t1.x, 1, t2.y + t2.x, max(t2.w, t2.z));
}
