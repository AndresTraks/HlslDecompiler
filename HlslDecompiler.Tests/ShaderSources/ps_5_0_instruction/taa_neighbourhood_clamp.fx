cbuffer C : register(b0)
{
	float2 texel;
	float blend;
};

SamplerState lin;
Texture2D cur;
Texture2D hist;
Texture2D<float2> vel;

struct PS_IN
{
	noperspective float4 sv_position : SV_Position;
	float2 texcoord : TEXCOORD;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	int4 r0;
	int4 r1;
	int4 r2;
	int4 r3;
	int4 r4;
	int4 r5;
	int4 r6;
	float3 r7;
	float3 r8;
	r0.zw = int2(0, 0);
	r1.xy = (int2)i.sv_position.xy;
	r2 = r1.xyxy + int4(0, -1, -1, -1);
	r0.xy = r2.zw;
	r0.xyz = asint(cur.Load(r0.xyz).xyz);
	r3.x = asint(dot(asfloat(r0.xzy), float3(0.25, 0.25, 0.5)));
	r3.y = asint(dot(asfloat(r0.xz), float2(0.5, -0.5)));
	r3.z = asint(dot(asfloat(r0.xzy), float3(-0.25, -0.25, 0.5)));
	r0.xyz = asint(min(asfloat(r3.xyz), float3(1000000000, 1000000000, 1000000000)));
	r3.xyz = asint(max(asfloat(r3.xyz), float3(-1000000000, -1000000000, -1000000000)));
	r2.zw = int2(0, 0);
	r2.xyz = asint(cur.Load(r2.xyz).xyz);
	r4.x = asint(dot(asfloat(r2.xzy), float3(0.25, 0.25, 0.5)));
	r4.y = asint(dot(asfloat(r2.xz), float2(0.5, -0.5)));
	r4.z = asint(dot(asfloat(r2.xzy), float3(-0.25, -0.25, 0.5)));
	r0.xyz = asint(min(asfloat(r0.xyz), asfloat(r4.xyz)));
	r2.xyz = asint(max(asfloat(r3.xyz), asfloat(r4.xyz)));
	r3.zw = int2(0, 0);
	r4 = r1.xyxy + int4(-1, 0, 1, -1);
	r3.xy = r4.zw;
	r3.xyz = asint(cur.Load(r3.xyz).xyz);
	r5.x = asint(dot(asfloat(r3.xzy), float3(0.25, 0.25, 0.5)));
	r5.y = asint(dot(asfloat(r3.xz), float2(0.5, -0.5)));
	r5.z = asint(dot(asfloat(r3.xzy), float3(-0.25, -0.25, 0.5)));
	r0.xyz = asint(min(asfloat(r0.xyz), asfloat(r5.xyz)));
	r2.xyz = asint(max(asfloat(r2.xyz), asfloat(r5.xyz)));
	r4.zw = int2(0, 0);
	r3.xyz = asint(cur.Load(r4.xyz).xyz);
	r4.x = asint(dot(asfloat(r3.xzy), float3(0.25, 0.25, 0.5)));
	r4.y = asint(dot(asfloat(r3.xz), float2(0.5, -0.5)));
	r4.z = asint(dot(asfloat(r3.xzy), float3(-0.25, -0.25, 0.5)));
	r0.xyz = asint(min(asfloat(r0.xyz), asfloat(r4.xyz)));
	r2.xyz = asint(max(asfloat(r2.xyz), asfloat(r4.xyz)));
	r1.zw = int2(0, 0);
	r3.xyz = asint(cur.Load(r1.xyw).xyz);
	r1.zw = asint(vel.Load(r1.xyz).xy);
	r1.zw = asint(-(asfloat(r1.zw)) + i.texcoord.xy);
	r4.x = asint(dot(asfloat(r3.xzy), float3(0.25, 0.25, 0.5)));
	r4.y = asint(dot(asfloat(r3.xz), float2(0.5, -0.5)));
	r4.z = asint(dot(asfloat(r3.xzy), float3(-0.25, -0.25, 0.5)));
	r0.xyz = asint(min(asfloat(r0.xyz), asfloat(r4.xyz)));
	r3 = r1.xyxy + int4(-1, 1, 1, 0);
	r5 = r1.xyxy + int4(1, 1, 0, 1);
	r6.xy = r3.zw;
	r6.zw = int2(0, 0);
	r6.xyz = asint(cur.Load(r6.xyz).xyz);
	r7.x = dot(asfloat(r6.xzy), float3(0.25, 0.25, 0.5));
	r7.y = dot(asfloat(r6.xz), float2(0.5, -0.5));
	r7.z = dot(asfloat(r6.xzy), float3(-0.25, -0.25, 0.5));
	r0.xyz = asint(min(asfloat(r0.xyz), r7.xyz));
	r3.zw = int2(0, 0);
	r3.xyz = asint(cur.Load(r3.xyz).xyz);
	r6.x = asint(dot(asfloat(r3.xzy), float3(0.25, 0.25, 0.5)));
	r6.y = asint(dot(asfloat(r3.xz), float2(0.5, -0.5)));
	r6.z = asint(dot(asfloat(r3.xzy), float3(-0.25, -0.25, 0.5)));
	r0.xyz = asint(min(asfloat(r0.xyz), asfloat(r6.xyz)));
	r3.xy = r5.zw;
	r3.zw = int2(0, 0);
	r3.xyz = asint(cur.Load(r3.xyz).xyz);
	r8.x = dot(asfloat(r3.xzy), float3(0.25, 0.25, 0.5));
	r8.y = dot(asfloat(r3.xz), float2(0.5, -0.5));
	r8.z = dot(asfloat(r3.xzy), float3(-0.25, -0.25, 0.5));
	r0.xyz = asint(min(asfloat(r0.xyz), r8.xyz));
	r5.zw = int2(0, 0);
	r3.xyz = asint(cur.Load(r5.xyz).xyz);
	r5.x = asint(dot(asfloat(r3.xzy), float3(0.25, 0.25, 0.5)));
	r5.y = asint(dot(asfloat(r3.xz), float2(0.5, -0.5)));
	r5.z = asint(dot(asfloat(r3.xzy), float3(-0.25, -0.25, 0.5)));
	r0.xyz = asint(min(asfloat(r0.xyz), asfloat(r5.xyz)));
	r3.xyz = asint(hist.SampleLevel(lin, asfloat(r1.zw), 0).xyz);
	r1.xy = asint(asfloat(r1.zw) + float2(-0.5, -0.5));
	r1.xy = (float2(0.5, 0.5) < abs(asfloat(r1.xy))) ? -1 : 0;
	r0.w = r1.y | r1.x;
	r0.w = (r0.w != 0) ? 1065353216 : asint(blend);
	r1.x = asint(dot(asfloat(r3.xzy), float3(0.25, 0.25, 0.5)));
	r1.y = asint(dot(asfloat(r3.xz), float2(0.5, -0.5)));
	r1.z = asint(dot(asfloat(r3.xzy), float3(-0.25, -0.25, 0.5)));
	r0.xyz = asint(max(asfloat(r0.xyz), asfloat(r1.xyz)));
	r1.xyz = asint(max(asfloat(r2.xyz), asfloat(r4.xyz)));
	r1.xyz = asint(max(r7.xyz, asfloat(r1.xyz)));
	r1.xyz = asint(max(asfloat(r6.xyz), asfloat(r1.xyz)));
	r1.xyz = asint(max(r8.xyz, asfloat(r1.xyz)));
	r1.xyz = asint(max(asfloat(r5.xyz), asfloat(r1.xyz)));
	r0.xyz = asint(min(asfloat(r0.xyz), asfloat(r1.xyz)));
	r1.xyz = asint(-(asfloat(r0.xyz)) + asfloat(r4.xyz));
	r0.xyz = asint(asfloat(r0.www) * asfloat(r1.xyz) + asfloat(r0.xyz));
	r0.w = asint(-(asfloat(r0.y)) + asfloat(r0.x));
	r0.xy = asint(asfloat(r0.yz) + asfloat(r0.xx));
	o.xz = -(asfloat(r0.zz)) + asfloat(r0.xw);
	o.y = asfloat(r0.y);
	o.w = 1;

	return o;
}
