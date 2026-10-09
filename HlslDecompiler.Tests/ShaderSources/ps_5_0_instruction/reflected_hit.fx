cbuffer Params : register(b0)
{
	float4x4 viewProjection;
	float3 cameraPos;
	float maxDistance;
	float thickness;
	int stepCount;
};

SamplerState linearSampler;
Texture2D sceneColour;
Texture2D<float> depthBuffer;

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float3 texcoord1 : TEXCOORD1;
	float3 normal : NORMAL;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	float4 r0;
	int4 r1;
	float4 r2;
	float4 r3;
	int3 r4;
	r0.xyz = i.texcoord1.xyz + -(cameraPos.xyz);
	r0.w = dot(r0.xyz, r0.xyz);
	r0.w = rsqrt(r0.w);
	r0.xyz = r0.www * r0.xyz;
	r0.w = dot(i.normal.xyz, i.normal.xyz);
	r0.w = rsqrt(r0.w);
	r1.xyz = asint(r0.www * i.normal.xyz);
	r0.w = dot(r0.xyz, asfloat(r1.xyz));
	r0.w = r0.w + r0.w;
	r0.xyz = asfloat(r1.xyz) * -(r0.www) + r0.xyz;
	r0.w = (float)stepCount;
	r1.w = 1065353216;
	r2 = float4(0, 0, 0, 0);
	r3.x = 1;
	while (true) {
		r3.y = (stepCount < r3.x) ? -1 : 0;
		if (asint(r3.y) != 0) break;
		r3.y = (float)(int)r3.x;
		r3.y = r3.y * maxDistance;
		r3.y = r3.y / r0.w;
		r1.xyz = asint(r0.xyz * r3.yyy + i.texcoord1.xyz);
		r4.x = asint(dot(asfloat(r1), transpose(viewProjection)[0]));
		r4.y = asint(dot(asfloat(r1), transpose(viewProjection)[1]));
		r3.y = dot(asfloat(r1), transpose(viewProjection)[3]);
		r3.zw = asfloat(r4.xy) / r3.yy;
		r3.zw = r3.zw * float2(0.5, -0.5) + float2(0.5, 0.5);
		r4.xy = (r3.zw < float2(0, 0)) ? -1 : 0;
		r4.x = r4.y | r4.x;
		r4.yz = (float2(1, 1) < r3.zw) ? -1 : 0;
		r4.y = r4.z | r4.y;
		r4.x = r4.y | r4.x;
		if (r4.x != 0) {
			break;
		}
		r1.x = asint(dot(asfloat(r1), transpose(viewProjection)[2]));
		r1.y = asint(depthBuffer.SampleLevel(linearSampler, r3.zw, 0).x);
		r1.x = asint(asfloat(r1.x) / r3.y);
		r1.z = (asfloat(r1.y) < asfloat(r1.x)) ? -1 : 0;
		r1.x = asint(-(asfloat(r1.y)) + asfloat(r1.x));
		r1.x = (asfloat(r1.x) < thickness) ? -1 : 0;
		r1.x = r1.x & r1.z;
		if (r1.x != 0) {
			r2.xyz = sceneColour.SampleLevel(linearSampler, r3.zw, 0).xyz;
			r2.w = 1;
			break;
		}
		r3.x = r3.x + 1;
		r2 = float4(0, 0, 0, 0);
	}
	o.xyz = r2.www * r2.xyz;
	o.w = r2.w;

	return o;
}
