cbuffer Decode : register(b0)
{
	float3 lightDirection;
};

Texture2D<float2> encodedNormals;

float4 main(noperspective float4 sv_position : SV_Position) : SV_Target
{
	float4 o;

	int4 r0;
	int4 r1;
	r0.xy = (int2)sv_position.xy;
	r0.zw = int2(0, 0);
	r0.xy = asint(encodedNormals.Load(r0.xyz).xy);
	r0.xy = asint(asfloat(r0.xy) * float2(2, 2) + float2(-1, -1));
	r0.zw = (float2(0, 0) < asfloat(r0.xy)) ? -1 : 0;
	r1.xy = (asfloat(r0.xy) < float2(0, 0)) ? -1 : 0;
	r0.zw = r0.zw + -(r1.xy);
	r0.zw = asint((float2)r0.zw);
	r0.zw = asint(abs(asfloat(r0.yx)) * asfloat(r0.zw));
	r1.x = asint(-(abs(asfloat(r0.x))) + 1);
	r1.z = asint(-(abs(asfloat(r0.y))) + asfloat(r1.x));
	r1.w = (asfloat(r1.z) < 0) ? -1 : 0;
	r0.zw = r0.zw & r1.ww;
	r1.xy = asint(asfloat(r0.zw) + asfloat(r0.xy));
	r0.x = asint(dot(asfloat(r1.xyz), asfloat(r1.xyz)));
	r0.x = asint(rsqrt(asfloat(r0.x)));
	r0.xyz = asint(asfloat(r0.xxx) * asfloat(r1.xyz));
	o = saturate(dot(asfloat(r0.xyz), -(lightDirection.xyz)));

	return o;
}
