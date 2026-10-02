cbuffer Overlay : register(b0)
{
	float2 cellSize;
	float lineWidth;
};

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	int4 r0;
	r0.xy = asint(abs(texcoord.xy) / cellSize.xy);
	r0.zw = (asfloat(r0.xy) >= -(asfloat(r0.xy))) ? -1 : 0;
	r0.xy = asint(frac(abs(asfloat(r0.xy))));
	r0.xy = (r0.zw != 0) ? r0.xy : asint(-(asfloat(r0.xy)));
	r0.zw = asint(asfloat(r0.xy) * cellSize.xy);
	r0.xy = asint(-(asfloat(r0.xy)) * cellSize.xy + cellSize.xy);
	r0.xy = asint(min(asfloat(r0.xy), asfloat(r0.zw)));
	r0.x = asint(min(asfloat(r0.y), asfloat(r0.x)));
	r0.x = asint(saturate(asfloat(r0.x) / lineWidth));
	o = -(asfloat(r0.x)) + float4(1, 1, 1, 1);

	return o;
}
